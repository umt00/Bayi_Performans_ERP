using BayiPerformansAppWeb.DataAccess.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BayiPerformansAppWeb.Api.HealthChecks;

public sealed class RedisHealthCheck(
    IServiceProvider serviceProvider,
    IOptions<RedisOptions> options) : IHealthCheck
{
    private readonly RedisOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var redis = serviceProvider.GetService<IConnectionMultiplexer>();

        if (redis is null)
        {
            return _options.AllowInMemoryFallback
                ? HealthCheckResult.Degraded(
                    "Redis is unavailable; the development-only in-memory OTP store is active.")
                : HealthCheckResult.Unhealthy(
                    "Redis is required but no connection is registered.");
        }

        try
        {
            if (!redis.IsConnected)
            {
                return HealthCheckResult.Unhealthy(
                    "Redis connection is not active.");
            }

            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis is reachable.");
        }
        catch (RedisException)
        {
            return HealthCheckResult.Unhealthy(
                "Redis health check failed.");
        }
    }
}
