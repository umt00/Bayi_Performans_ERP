using BayiPerformansAppWeb.Api.HealthChecks;
using BayiPerformansAppWeb.DataAccess;
using BayiPerformansAppWeb.DataAccess.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.Tests.Infrastructure;

public sealed class OperationalHealthCheckTests
{
    [Fact]
    public async Task DatabaseCanConnect_ReturnsHealthy()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PentaDbContext>(options =>
            options.UseInMemoryDatabase(nameof(DatabaseCanConnect_ReturnsHealthy)));
        await using var provider = services.BuildServiceProvider();
        var check = new DatabaseHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DatabaseServiceIsMissing_ReturnsUnhealthy()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();
        var check = new DatabaseHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task RedisIsMissingWithDevelopmentFallback_ReturnsDegraded()
    {
        await using var provider = new ServiceCollection()
            .BuildServiceProvider();
        var check = new RedisHealthCheck(
            provider,
            Options.Create(new RedisOptions
            {
                AllowInMemoryFallback = true
            }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task RedisIsMissingWithoutFallback_ReturnsUnhealthy()
    {
        await using var provider = new ServiceCollection()
            .BuildServiceProvider();
        var check = new RedisHealthCheck(
            provider,
            Options.Create(new RedisOptions
            {
                AllowInMemoryFallback = false
            }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
