using System;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BayiPerformansAppWeb.Api.BackgroundServices;

/// <summary>
/// Bayi performans ve risk metriklerini belirli periyotlarla arka planda otomatik hesaplayan Hosted Service.
/// </summary>
public sealed class ScoreCalculationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScoreCalculationBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public ScoreCalculationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScoreCalculationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScoreCalculationBackgroundService başlatıldı.");

        // Uygulama ayağa kalktığında ilk hesaplama için kısa bir bekleme
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var jobService = scope.ServiceProvider.GetRequiredService<IScheduledScoreJobService>();

                _logger.LogInformation("ScoreCalculationBackgroundService: Periyodik hesaplama tetikleniyor...");
                var result = await jobService.SkorlariHesaplaVeOnbellekleAsync(stoppingToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("ScoreCalculationBackgroundService: Hesaplama başarıyla tamamlandı. İşlenen bayi: {Sayi}",
                        result.Value?.IslenenBayiSayisi);
                }
                else
                {
                    _logger.LogWarning("ScoreCalculationBackgroundService: Hesaplama başarısız oldu: {Hata}",
                        result.ErrorDetail);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScoreCalculationBackgroundService sırasında beklenmeyen bir hata oluştu.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("ScoreCalculationBackgroundService durduruluyor.");
    }
}
