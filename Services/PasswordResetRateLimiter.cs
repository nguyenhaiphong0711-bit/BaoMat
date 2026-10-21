using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed record PasswordResetRequestLease(PasswordResetRequestLimit? Previous, PasswordResetRequestLimit Current);

public sealed class PasswordResetRateLimiter(MongoContext db)
{
    public async Task<PasswordResetRequestLease?> TryReserveAsync(
        ObjectId userId, PasswordResetToken? legacyToken, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var previous = await db.PasswordResetRequestLimits.Find(x => x.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
        // Carry forward the old counter on first deployment rather than resetting its window.
        var startedAt = previous?.WindowStartedAt ?? legacyToken?.RequestWindowStartedAt ?? now;
        var count = previous?.Count ?? legacyToken?.RequestCount ?? 0;
        var lastRequestedAt = previous?.LastRequestedAt ?? legacyToken?.LastSentAt ?? DateTime.MinValue;
        if (now - lastRequestedAt < TimeSpan.FromSeconds(60))
            return null;
        if (now - startedAt >= TimeSpan.FromHours(1))
        {
            startedAt = now;
            count = 0;
        }
        if (count >= 5)
            return null;
        var current = new PasswordResetRequestLimit
        {
            UserId = userId, WindowStartedAt = startedAt, LastRequestedAt = now,
            Count = count + 1, ExpiresAt = startedAt.AddHours(1)
        };
        if (previous is null)
        {
            try { await db.PasswordResetRequestLimits.InsertOneAsync(current, cancellationToken: cancellationToken); }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            { return null; }
        }
        else
        {
            var changed = await db.PasswordResetRequestLimits.ReplaceOneAsync(
                x => x.UserId == userId && x.Count == previous.Count && x.LastRequestedAt == previous.LastRequestedAt &&
                    x.WindowStartedAt == previous.WindowStartedAt, current, cancellationToken: cancellationToken);
            if (changed.ModifiedCount != 1)
                return null;
        }
        return new(previous, current);
    }

    public async Task ReleaseAsync(PasswordResetRequestLease lease)
    {
        var current = lease.Current;
        var filter = Builders<PasswordResetRequestLimit>.Filter.Where(x =>
            x.UserId == current.UserId && x.Count == current.Count && x.LastRequestedAt == current.LastRequestedAt);
        if (lease.Previous is null)
            await db.PasswordResetRequestLimits.DeleteOneAsync(filter, CancellationToken.None);
        else
            await db.PasswordResetRequestLimits.ReplaceOneAsync(filter, lease.Previous, cancellationToken: CancellationToken.None);
    }
}
