using BayiPerformansAppWeb.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BayiPerformansAppWeb.Api.Configuration;

public static class OperationalHealthCheckExtensions
{
    public static IServiceCollection AddOperationalHealthChecks(
        this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(
                "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "database"])
            .AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "otp"]);

        return services;
    }
}
