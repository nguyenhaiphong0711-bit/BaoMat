using LMS.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LMS.Services;

public sealed class MongoHealthCheck(MongoContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try { await db.PingAsync(cancellationToken); return HealthCheckResult.Healthy(); }
        catch (Exception error) { return HealthCheckResult.Unhealthy("MongoDB is unavailable.", error); }
    }
}
