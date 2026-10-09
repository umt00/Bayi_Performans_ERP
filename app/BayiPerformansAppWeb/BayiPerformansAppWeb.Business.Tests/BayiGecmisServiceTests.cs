using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;
using Xunit;

namespace BayiPerformansAppWeb.Tests.Business.Services;

public sealed class BayiGecmisServiceTests
{
    private static BusinessRulesOptions CreateOptions()
    {
        return new BusinessRulesOptions
        {
            MarjEsigiPct = 0.15m,
            MaxIskontoPct = 0.15m,
            HedefRiskEsigiPct = 0.80m,
            StokGunEsigi = 7m,
            IadeRiskEsigiPct = 0.10m,
            RiskWeights = new Dictionary<string, int>
            {
                ["HEDEF_ALTINDA"] = 35,
                ["MARJ_ESIK_ALTI"] = 30,
                ["YUKSEK_ISKONTO"] = 20,
                ["STOK_YETERSIZ"] = 15
            },
            RiskLevels = new Dictionary<string, int>
            {
                ["DUSUK_max"] = 30,
                ["ORTA_max"] = 60,
                ["YUKSEK_max"] = 900
            }
        };
    }

    private static BayiGecmisService CreateService(
        IBayiReadRepository bayiRepo,
        ISatisReadRepository satisRepo,
        IAylikHedefReadRepository hedefRepo,
        IUrunReadRepository urunRepo,
        IStokReadRepository stokRepo)
    {
        var options = CreateOptions();
        var salesCalc = new SalesMetricsCalculator();
        var stockCalc = new StockMetricsCalculator(options.StokGunEsigi);
        var riskEngine = new RiskEngine(options);

        return new BayiGecmisService(
            bayiRepo,
            satisRepo,
            hedefRepo,
            urunRepo,
            stokRepo,
            salesCalc,
            stockCalc,
            riskEngine);
    }

    [Fact]
    public async Task GetirGecmisAsync_BayiBulunamadiginda_NotFoundDonmelidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo();
        var satisRepo = new FakeSatisRepo();
        var hedefRepo = new FakeHedefRepo();
        var urunRepo = new FakeUrunRepo();
        var stokRepo = new FakeStokRepo();

        var service = CreateService(bayiRepo, satisRepo, hedefRepo, urunRepo, stokRepo);
        var query = new BayiGecmisQuery("BAYI-999", 6);

        // ACT
        var result = await service.GetirGecmisAsync(query, CancellationToken.None);

        // ASSERT
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        Assert.Equal("BAYI_BULUNAMADI", result.ErrorCode);
    }

    [Fact]
    public async Task GetirGecmisAsync_GecerliBayiVeSatislar_AylikTrendiDonmelidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo(
            new BayiReadModel("BAYI-001", "Kurumsal", "Ege"));

        var satisRepo = new FakeSatisRepo(
            new SatisReadModel(1, new DateOnly(2026, 1, 10), "BAYI-001", "U001", "D001", 10m, 100m, 0m, null, false),
            new SatisReadModel(2, new DateOnly(2025, 12, 10), "BAYI-001", "U001", "D001", 5m, 100m, 0m, null, false))
        {
            LatestSaleDate = new DateOnly(2026, 1, 20)
        };

        var hedefRepo = new FakeHedefRepo(
            new AylikHedefReadModel("BAYI-001", 2026, 1, 1000m),
            new AylikHedefReadModel("BAYI-001", 2025, 12, 500m));

        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 50m));

        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 100m, 10m, null));

        var service = CreateService(bayiRepo, satisRepo, hedefRepo, urunRepo, stokRepo);
        var query = new BayiGecmisQuery("BAYI-001", 2);

        // ACT
        var result = await service.GetirGecmisAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("BAYI-001", result.Value!.BayiKodu);
        Assert.Equal(2, result.Value.AylikPerformanslar.Count);

        var firstMonth = result.Value.AylikPerformanslar[0]; // 2025-12
        var secondMonth = result.Value.AylikPerformanslar[1]; // 2026-01

        Assert.Equal(new DateOnly(2025, 12, 1), firstMonth.Donem);
        Assert.Equal(500m, firstMonth.GerceklesenCiro);
        Assert.Equal(1.0m, firstMonth.HedefeUlasmaPct);

        Assert.Equal(new DateOnly(2026, 1, 1), secondMonth.Donem);
        Assert.Equal(1000m, secondMonth.GerceklesenCiro);
        Assert.Equal(1.0m, secondMonth.HedefeUlasmaPct);
    }

    [Fact]
    public async Task GetirGecmisAsync_GuncelStokKritikse_SadeceEnSonAyaUygulanmaliGecmisAylaraUygulanmamalidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo(
            new BayiReadModel("BAYI-001", "Kurumsal", "Ege"));

        var satisRepo = new FakeSatisRepo(
            new SatisReadModel(1, new DateOnly(2026, 1, 10), "BAYI-001", "U001", "D001", 10m, 100m, 0m, null, false),
            new SatisReadModel(2, new DateOnly(2025, 12, 10), "BAYI-001", "U001", "D001", 5m, 100m, 0m, null, false))
        {
            LatestSaleDate = new DateOnly(2026, 1, 20)
        };

        var hedefRepo = new FakeHedefRepo(
            new AylikHedefReadModel("BAYI-001", 2026, 1, 1000m),
            new AylikHedefReadModel("BAYI-001", 2025, 12, 500m));

        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 50m));

        // Stok 0 (Kritik Stok)
        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 0m, 10m, null));

        var service = CreateService(bayiRepo, satisRepo, hedefRepo, urunRepo, stokRepo);
        var query = new BayiGecmisQuery("BAYI-001", 2);

        // ACT
        var result = await service.GetirGecmisAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        var pastMonth = result.Value!.AylikPerformanslar[0]; // 2025-12
        var latestMonth = result.Value.AylikPerformanslar[1]; // 2026-01

        // Geçmiş ayda bugünkü stok snapshot'ı olmadığı için stok riski geçmişe geriye dönük yansıtılmaz
        Assert.DoesNotContain("STOK_YETERSIZ", pastMonth.GerekceKodlari);

        // Güncel ayda ise stok riski değerlendirilir
        Assert.Contains("STOK_YETERSIZ", latestMonth.GerekceKodlari);
    }

    #region Fakes

    private sealed class FakeBayiRepo : IBayiReadRepository
    {
        private readonly List<BayiReadModel> _bayiler;
        public FakeBayiRepo(params BayiReadModel[] bayiler) => _bayiler = bayiler.ToList();

        public Task<BayiReadModel?> GetByCodeAsync(string bayiKodu, CancellationToken cancellationToken)
            => Task.FromResult(_bayiler.FirstOrDefault(b => b.BayiKodu == bayiKodu));

        public Task<IReadOnlyList<BayiReadModel>> ListAsync(string? segment, string? bolge, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BayiReadModel>>(_bayiler);
    }

    private sealed class FakeSatisRepo : ISatisReadRepository
    {
        private readonly List<SatisReadModel> _satislar;
        public DateOnly? LatestSaleDate { get; set; }

        public FakeSatisRepo(params SatisReadModel[] satislar) => _satislar = satislar.ToList();

        public Task<DateOnly?> GetLatestSaleDateAsync(CancellationToken cancellationToken) => Task.FromResult(LatestSaleDate);

        public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(_satislar.Where(s => s.Tarih >= periodStart && s.Tarih < periodEndExclusive).ToList());

        public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(_satislar.Where(s => s.BayiKodu == bayiKodu && s.Tarih >= periodStart && s.Tarih < periodEndExclusive).ToList());

        public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(string urunKodu, string depoKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(Array.Empty<SatisReadModel>());

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(new Dictionary<(string UrunKodu, string DepoKodu), decimal>());

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesForDealerAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(new Dictionary<(string UrunKodu, string DepoKodu), decimal>());

        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DateOnly>>(_satislar.Select(s => new DateOnly(s.Tarih.Year, s.Tarih.Month, 1)).Distinct().ToArray());
    }

    private sealed class FakeHedefRepo : IAylikHedefReadRepository
    {
        private readonly List<AylikHedefReadModel> _hedefler;
        public FakeHedefRepo(params AylikHedefReadModel[] hedefler) => _hedefler = hedefler.ToList();

        public Task<AylikHedefReadModel?> GetAsync(string bayiKodu, int yil, int ay, CancellationToken cancellationToken)
            => Task.FromResult(_hedefler.FirstOrDefault(h => h.BayiKodu == bayiKodu && h.Yil == yil && h.Ay == ay));

        public Task<IReadOnlyList<AylikHedefReadModel>> ListByPeriodAsync(int yil, int ay, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AylikHedefReadModel>>(_hedefler.Where(h => h.Yil == yil && h.Ay == ay).ToList());
    }

    private sealed class FakeUrunRepo : IUrunReadRepository
    {
        private readonly List<UrunReadModel> _urunler;
        public FakeUrunRepo(params UrunReadModel[] urunler) => _urunler = urunler.ToList();

        public Task<UrunReadModel?> GetByCodeAsync(string urunKodu, CancellationToken cancellationToken)
            => Task.FromResult(_urunler.FirstOrDefault(u => u.UrunKodu == urunKodu));

        public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(IReadOnlyCollection<string> urunKodlari, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UrunReadModel>>(_urunler.Where(u => urunKodlari.Contains(u.UrunKodu)).ToList());

        public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UrunReadModel>>(_urunler);
    }

    private sealed class FakeStokRepo : IStokReadRepository
    {
        private readonly List<StokReadModel> _stoklar;
        public FakeStokRepo(params StokReadModel[] stoklar) => _stoklar = stoklar.ToList();

        public Task<IReadOnlyList<StokReadModel>> ListAsync(string? depoKodu, string? urunKodu, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StokReadModel>>(_stoklar);

        public Task<StokReadModel?> GetAsync(string urunKodu, string depoKodu, CancellationToken cancellationToken)
            => Task.FromResult(_stoklar.FirstOrDefault(s => s.UrunKodu == urunKodu && s.DepoKodu == depoKodu));
    }

    #endregion
}
