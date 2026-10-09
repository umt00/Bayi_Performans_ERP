using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class ScheduledScoreJobServiceTests
{
    private sealed class FakeBayiRepo : IBayiReadRepository
    {
        public Task<IReadOnlyList<BayiReadModel>> ListAsync(string? segment, string? bolge, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BayiReadModel>>(new List<BayiReadModel>
            {
                new("B001", "Bayi 1", "Istanbul", "Marmara", "Kurumsal"),
                new("B002", "Bayi 2", "Ankara", "Ic Anadolu", "KOBI")
            });

        public Task<BayiReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default) =>
            Task.FromResult<BayiReadModel?>(null);
    }

    private sealed class FakeRiskService : IRiskListesiService
    {
        public Task<ServiceResult<RiskListesiOutput>> ListeleAsync(RiskListesiQuery query, CancellationToken ct = default)
        {
            var output = new RiskListesiOutput(
                Donem: new DateOnly(2025, 5, 1),
                Bayiler: Array.Empty<RiskListesiItemOutput>(),
                ToplamKayitSayisi: 2,
                Sayfa: 1,
                SayfaBoyutu: 10);

            return Task.FromResult(ServiceResult<RiskListesiOutput>.Success(output));
        }
    }

    private sealed class FakeBayiOzetService : IBayiOzetService
    {
        public Task<ServiceResult<BayilerOzetOutput>> GetirOzetAsync(BayilerOzetQuery query, CancellationToken ct = default)
        {
            var output = new BayilerOzetOutput(
                Donem: new DateOnly(2025, 5, 1),
                ToplamBayiSayisi: 2,
                AktifSatisYapanBayiSayisi: 2,
                ToplamGerceklesenCiro: 1800000m,
                ToplamHedefCiro: 2000000m,
                GenelHedefeUlasmaPct: 0.9m,
                GenelOrtalamaMarjPct: 0.20m,
                GenelOrtalamaIskontoPct: 0.05m,
                YuksekRiskliBayiSayisi: 0,
                OrtaRiskliBayiSayisi: 1,
                DusukRiskliBayiSayisi: 1,
                KritikStokEtkilenenBayiSayisi: 0,
                GerekceKoduDagilimi: new Dictionary<string, int>());

            return Task.FromResult(ServiceResult<BayilerOzetOutput>.Success(output));
        }
    }

    [Fact]
    public async Task SkorlariHesaplaVeOnbellekleAsync_BasariliCalisirVeDurumGunceller()
    {
        var service = new ScheduledScoreJobService(
            new FakeBayiRepo(),
            new FakeRiskService(),
            new FakeBayiOzetService(),
            timeProvider: TimeProvider.System);

        var result = await service.SkorlariHesaplaVeOnbellekleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("BASARILI", result.Value.Durum);
        Assert.Equal(2, result.Value.IslenenBayiSayisi);
        Assert.NotNull(result.Value.SonCalismaZamaniUtc);

        var durumResult = await service.GetirJobDurumuAsync(CancellationToken.None);
        Assert.True(durumResult.IsSuccess);
        Assert.Equal("BASARILI", durumResult.Value!.Durum);
    }
}
