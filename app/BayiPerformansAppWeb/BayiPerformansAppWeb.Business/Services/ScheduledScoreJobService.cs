using System;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities;
using Microsoft.Extensions.Logging;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Bayi performans ve risk skorlarını periyodik olarak toplu hesaplayan ve durumunu takip eden servis.
/// </summary>
public sealed class ScheduledScoreJobService : IScheduledScoreJobService
{
    private readonly IBayiReadRepository _bayiReadRepository;
    private readonly IRiskListesiService _riskListesiService;
    private readonly IBayiOzetService _bayiOzetService;
    private readonly ILogger<ScheduledScoreJobService>? _logger;
    private readonly TimeProvider _timeProvider;

    private static JobDurumBilgisi _sonDurum = new(
        IslemAdi: "BAYI_SKOR_HESAPLAMA_JOB",
        Durum: "HAZIR",
        SonCalismaZamaniUtc: null,
        SonrakiCalismaZamaniUtc: null,
        IslenenBayiSayisi: 0,
        SonHataMesaji: null);

    private static readonly object LockObject = new();

    public ScheduledScoreJobService(
        IBayiReadRepository bayiReadRepository,
        IRiskListesiService riskListesiService,
        IBayiOzetService bayiOzetService,
        ILogger<ScheduledScoreJobService>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _bayiReadRepository = bayiReadRepository ?? throw new ArgumentNullException(nameof(bayiReadRepository));
        _riskListesiService = riskListesiService ?? throw new ArgumentNullException(nameof(riskListesiService));
        _bayiOzetService = bayiOzetService ?? throw new ArgumentNullException(nameof(bayiOzetService));
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<JobDurumBilgisi>> SkorlariHesaplaVeOnbellekleAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Scheduled skor hesaplama işlemi başlatılıyor...");
        var baslangicZamani = _timeProvider.GetUtcNow();

        lock (LockObject)
        {
            _sonDurum = _sonDurum with
            {
                Durum = "CALISIYOR",
                SonHataMesaji = null
            };
        }

        try
        {
            var bayiler = await _bayiReadRepository.ListAsync(null, null, ct);
            int toplamBayi = bayiler.Count;

            // Risk listesini ve bayi özetlerini çalıştırıp cache/hafızaya al
            var riskQuery = new RiskListesiQuery(
                Donem: null,
                Segment: null,
                Bolge: null,
                Sehir: null,
                RiskSeviyesi: null,
                MinRiskSkoru: null,
                MaxRiskSkoru: null,
                GerekceKodu: null,
                Sayfa: 1,
                SayfaBoyutu: 1000);

            await _riskListesiService.ListeleAsync(riskQuery, ct);
            await _bayiOzetService.GetirOzetAsync(new BayilerOzetQuery(), ct);

            var bitisZamani = _timeProvider.GetUtcNow();
            var sonrakiZaman = bitisZamani.AddHours(24); // Günlük periyot

            lock (LockObject)
            {
                _sonDurum = new JobDurumBilgisi(
                    IslemAdi: "BAYI_SKOR_HESAPLAMA_JOB",
                    Durum: "BASARILI",
                    SonCalismaZamaniUtc: bitisZamani,
                    SonrakiCalismaZamaniUtc: sonrakiZaman,
                    IslenenBayiSayisi: toplamBayi,
                    SonHataMesaji: null);
            }

            _logger?.LogInformation("Scheduled skor hesaplama tamamlandı. İşlenen bayi sayısı: {Sayi}", toplamBayi);
            return ServiceResult<JobDurumBilgisi>.Success(_sonDurum);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Scheduled skor hesaplama sırasında hata oluştu.");
            lock (LockObject)
            {
                _sonDurum = _sonDurum with
                {
                    Durum = "HATALI",
                    SonHataMesaji = ex.Message
                };
            }

            return ServiceResult<JobDurumBilgisi>.Fail(
                ServiceErrorKind.Unavailable,
                "JOB_CALISTIRILAMADI",
                $"Arka plan işi yürütülürken hata oluştu: {ex.Message}");
        }
    }

    public Task<ServiceResult<JobDurumBilgisi>> GetirJobDurumuAsync(CancellationToken ct = default)
    {
        lock (LockObject)
        {
            return Task.FromResult(ServiceResult<JobDurumBilgisi>.Success(_sonDurum));
        }
    }
}
