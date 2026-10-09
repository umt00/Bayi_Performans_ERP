using BayiPerformansAppWeb.Business.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BayiPerformansAppWeb.Api.Integration;

public static class ApiIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessIntegrationFallbacks(
        this IServiceCollection services)
    {
        services.TryAddScoped<PendingBusinessServices>();
        services.TryAddScoped<IBayiPerformansService>(
            provider => provider.GetRequiredService<PendingBusinessServices>());
        services.TryAddScoped<IRiskListesiService>(
            provider => provider.GetRequiredService<PendingBusinessServices>());
        services.TryAddScoped<IKritikStokService>(
            provider => provider.GetRequiredService<PendingBusinessServices>());
        services.TryAddScoped<ITeklifDegerlendirmeService>(
            provider => provider.GetRequiredService<PendingBusinessServices>());
        services.TryAddScoped<ILoginOtpDogrulamaService, PendingLoginOtpDogrulamaService>();

        return services;
    }
}
