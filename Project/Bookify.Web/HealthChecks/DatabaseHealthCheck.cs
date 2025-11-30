using Bookify.Data.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
          
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
                return HealthCheckResult.Healthy("Database is reachable.");
            else
                return HealthCheckResult.Unhealthy("Database is unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
