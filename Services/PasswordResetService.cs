using System.Globalization;
using System.Security.Cryptography;
using LMS.Data;
using LMS.Models;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class PasswordResetService(
    MongoContext db,
    GmailEmailSender emailSender,
    ILogger<PasswordResetService> logger)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RequestWindow = TimeSpan.FromHours(1);
    private const int MaximumRequestsPerWindow = 5;
    private const int MaximumFailedAttempts = 5;

    public async Task RequestCodeAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(x => x.Email == normalizedEmail && x.IsActive).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return;

        var now = DateTime.UtcNow;
        var token = await db.PasswordResetTokens.Find(x => x.UserId == user.Id).FirstOrDefaultAsync(cancellationToken);
        if (token is not null && now - token.LastSentAt < RequestCooldown)
            return;
        if (token is not null &&
            now - token.RequestWindowStartedAt < RequestWindow &&
            token.RequestCount >= MaximumRequestsPerWindow)
            return;

        var windowStartedAt = token is not null && now - token.RequestWindowStartedAt < RequestWindow
            ? token.RequestWindowStartedAt
            : now;
        var requestCount = token is not null && windowStartedAt == token.RequestWindowStartedAt
            ? token.RequestCount + 1
            : 1;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var replacement = new PasswordResetToken
        {
            Id = token?.Id ?? MongoDB.Bson.ObjectId.GenerateNewId(),
            UserId = user.Id,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime),
            LastSentAt = now,
            RequestWindowStartedAt = windowStartedAt,
            RequestCount = requestCount,
            FailedAttempts = 0
        };

        await db.PasswordResetTokens.ReplaceOneAsync(
            x => x.UserId == user.Id,
            replacement,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        try
        {
            await emailSender.SendPasswordResetCodeAsync(user.Email, code, cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            logger.LogError(exception, "Password reset code could not be sent for account {UserId}.", user.Id);
        }
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
        if (token is null || token.ExpiresAt <= now || token.FailedAttempts >= MaximumFailedAttempts)
            return false;

        if (!BCrypt.Net.BCrypt.Verify(code, token.CodeHash))
        {
            await db.PasswordResetTokens.UpdateOneAsync(
                x => x.Id == token.Id && x.ExpiresAt > now && x.FailedAttempts < MaximumFailedAttempts,
                Builders<PasswordResetToken>.Update.Inc(x => x.FailedAttempts, 1),
                cancellationToken: cancellationToken);
            return false;
        }

        var consumed = await db.PasswordResetTokens.FindOneAndDeleteAsync(
            x => x.Id == token.Id && x.ExpiresAt > now && x.FailedAttempts < MaximumFailedAttempts,
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
