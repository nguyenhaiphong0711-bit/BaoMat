using System.Globalization;
using System.Security.Cryptography;
using LMS.Data;
using LMS.Models;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class PasswordResetService(
    MongoContext db,
    GmailEmailSender emailSender,
    PasswordResetRateLimiter rateLimiter,
    ILogger<PasswordResetService> logger)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(60);
    private const int MaximumFailedAttempts = 5;

    public async Task RequestCodeAsync(string email, CancellationToken cancellationToken)
    {
        try { emailSender.EnsureConfigured(); }
        catch (EmailDeliveryException exception)
        {
            logger.LogError("{ConfigurationError}", exception.Message);
            throw;
        }
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(x => x.Email == normalizedEmail && x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return;

        var now = DateTime.UtcNow;
        var token = await db.PasswordResetTokens.Find(x => x.UserId == user.Id).FirstOrDefaultAsync(cancellationToken);
        if (token is { DeliveryPending: true } && token.ExpiresAt > now)
            return;
        if (token is not null && now - token.LastSentAt < RequestCooldown)
            return;
        var lease = await rateLimiter.TryReserveAsync(user.Id, token, cancellationToken);
        if (lease is null)
            return;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var replacement = new PasswordResetToken
        {
            Id = token?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId(),
            UserId = user.Id,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
            LastSentAt = now,
            RequestWindowStartedAt = lease.Current.WindowStartedAt,
            RequestCount = lease.Current.Count,
            DeliveryPending = true,
            FailedAttempts = 0
        };

        // Reserve the send atomically so concurrent requests cannot bypass the cooldown.
        if (token is null)
        {
            try { await db.PasswordResetTokens.InsertOneAsync(replacement, cancellationToken: cancellationToken); }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            { await rateLimiter.ReleaseAsync(lease); return; }
        }
        else
        {
            var reserved = await db.PasswordResetTokens.ReplaceOneAsync(
                x => x.Id == token.Id && x.CodeHash == token.CodeHash && x.LastSentAt == token.LastSentAt,
                replacement, cancellationToken: cancellationToken);
            if (reserved.ModifiedCount != 1)
            {
                await rateLimiter.ReleaseAsync(lease);
                return;
            }
        }

        try
        {
            await emailSender.SendPasswordResetCodeAsync(user.Email, code, cancellationToken);
        }
        catch (Exception exception) when (exception is EmailDeliveryException or OperationCanceledException)
        {
            // Restore the previous usable code, without overwriting a newer request.
            if (token is null)
                await db.PasswordResetTokens.DeleteOneAsync(
                    x => x.Id == replacement.Id && x.CodeHash == replacement.CodeHash,
                    CancellationToken.None);
            else
                await db.PasswordResetTokens.ReplaceOneAsync(
                    x => x.Id == replacement.Id && x.CodeHash == replacement.CodeHash,
                    token, cancellationToken: CancellationToken.None);
            await rateLimiter.ReleaseAsync(lease);
            logger.LogError("Password reset delivery failed for account {UserId}. Failure type: {FailureType}.",
                user.Id, exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            throw;
        }

        await db.PasswordResetTokens.UpdateOneAsync(
            x => x.Id == replacement.Id && x.CodeHash == replacement.CodeHash,
            Builders<PasswordResetToken>.Update.Set(x => x.DeliveryPending, false),
            cancellationToken: CancellationToken.None);
    }

    public async Task<bool> ResetPasswordAsync(
        string email,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(x => x.Email == normalizedEmail && x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return false;

        var token = await db.PasswordResetTokens.Find(x => x.UserId == user.Id).FirstOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (token is null || token.DeliveryPending || token.ExpiresAt <= now || token.FailedAttempts >= MaximumFailedAttempts)
            return false;

        if (!BCrypt.Net.BCrypt.Verify(code, token.CodeHash))
        {
            await db.PasswordResetTokens.UpdateOneAsync(
                x => x.Id == token.Id && x.CodeHash == token.CodeHash && !x.DeliveryPending && x.ExpiresAt > now && x.FailedAttempts < MaximumFailedAttempts,
                Builders<PasswordResetToken>.Update.Inc(x => x.FailedAttempts, 1),
                cancellationToken: cancellationToken);
            return false;
        }

        var consumed = await db.PasswordResetTokens.FindOneAndDeleteAsync(
            x => x.Id == token.Id && x.CodeHash == token.CodeHash && !x.DeliveryPending && x.ExpiresAt > now && x.FailedAttempts < MaximumFailedAttempts,
            cancellationToken: cancellationToken);
        if (consumed is null)
            return false;

        var updated = await db.Users.UpdateOneAsync(
            x => x.Id == user.Id && x.IsActive,
            Builders<User>.Update
                .Set(x => x.PasswordHash, BCrypt.Net.BCrypt.HashPassword(newPassword))
                .Set(x => x.PasswordChangedAt, now)
                .Set(x => x.FailedLoginAttempts, 0)
                .Set(x => x.LockoutUntil, (DateTime?)null),
            cancellationToken: cancellationToken);
        if (updated.ModifiedCount != 1)
        {
            logger.LogWarning("Password reset code was consumed but account {UserId} was not updated.", user.Id);
            return false;
        }

        return true;
    }
}
