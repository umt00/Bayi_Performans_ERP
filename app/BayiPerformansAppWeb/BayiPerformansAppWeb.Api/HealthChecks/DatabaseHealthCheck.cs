using BayiPerformansAppWeb.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BayiPerformansAppWeb.Api.HealthChecks;

public sealed class DatabaseHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<PentaDbContext>();

            bool canConnect = await dbContext.Database.CanConnectAsync(
                cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy(
                    "PostgreSQL connection could not be established.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL health check timed out.");
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL health check failed.");
        }
    }
}
