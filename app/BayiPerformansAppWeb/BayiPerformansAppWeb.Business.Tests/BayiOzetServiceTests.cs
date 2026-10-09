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

public sealed class BayiOzetServiceTests
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

    private static BayiOzetService CreateService(
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

        return new BayiOzetService(
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
    public async Task GetirOzetAsync_HicBayiYoksa_SifirMetriklerleDonmelidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo();
        var satisRepo = new FakeSatisRepo();
        var hedefRepo = new FakeHedefRepo();
        var urunRepo = new FakeUrunRepo();
        var stokRepo = new FakeStokRepo();

        var service = CreateService(bayiRepo, satisRepo, hedefRepo, urunRepo, stokRepo);
        var query = new BayilerOzetQuery(new DateOnly(2026, 1, 1), null, null);

        // ACT
        var result = await service.GetirOzetAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0, result.Value!.ToplamBayiSayisi);
        Assert.Equal(0m, result.Value.ToplamGerceklesenCiro);
    }

    [Fact]
    public async Task GetirOzetAsync_BayilerVeSatislarVarsa_OzetMetrikleriDogruHesaplamalidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo(
            new BayiReadModel("B001", "Kurumsal", "Ege"),
            new BayiReadModel("B002", "KOBİ", "Marmara"));

        var satisRepo = new FakeSatisRepo(
            new SatisReadModel(1, new DateOnly(2026, 1, 10), "B001", "U001", "D001", 10m, 100m, 0.05m, null, false),
            new SatisReadModel(2, new DateOnly(2026, 1, 15), "B002", "U002", "D001", 5m, 200m, 0.10m, null, false))
        {
            LatestSaleDate = new DateOnly(2026, 1, 20)
        };

        var hedefRepo = new FakeHedefRepo(
            new AylikHedefReadModel("B001", 2026, 1, 1000m),
            new AylikHedefReadModel("B002", 2026, 1, 1000m));

        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 50m),
            new UrunReadModel("U002", "Kat1", "Alt1", 100m));

        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 100m, 10m, null),
            new StokReadModel("U002", "D001", 100m, 10m, null));

        var service = CreateService(bayiRepo, satisRepo, hedefRepo, urunRepo, stokRepo);
        var query = new BayilerOzetQuery(new DateOnly(2026, 1, 1), null, null);

        // ACT
        var result = await service.GetirOzetAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.ToplamBayiSayisi);
        Assert.Equal(2, result.Value.AktifSatisYapanBayiSayisi);
        // B001: 10 * 100 * 0.95 = 950; B002: 5 * 200 * 0.90 = 900 -> Toplam = 1850
        Assert.Equal(1850m, result.Value.ToplamGerceklesenCiro);
        Assert.Equal(2000m, result.Value.ToplamHedefCiro);
        Assert.Equal(0.925m, result.Value.GenelHedefeUlasmaPct);
    }

    [Fact]
    public async Task GetirOzetAsync_HedefiOlmayanBayiVarsa_HedefOraniSadeceHedefliBayilereGoreHesaplanmalidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo(
            new BayiReadModel("B001", "Kurumsal", "Ege"),
            new BayiReadModel("B002", "KOBİ", "Marmara"));

        // B001 satışı 500 (Hedef 1000 => %50), B002 satışı 500 (Hedef YOK)
        var satisRepo = new FakeSatisRepo(
            new SatisReadModel(1, new DateOnly(2026, 1, 10), "B001", "U001", "D001", 5m, 100m, 0m, null, false),
            new SatisReadModel(2, new DateOnly(2026, 1, 15), "B002", "U002", "D001", 5m, 100m, 0m, null, false));

        var hedefRepo = new FakeHedefRepo(
            new AylikHedefReadModel("B001", 2026, 1, 1000m)); // Sadece B001'in hedefi var

        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 50m),
            new UrunReadModel("U002", "Kat1", "Alt1", 50m));

        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 100m, 10m, null),
            new StokReadModel("U002", "D001", 100m, 10m, null));

        var service = CreateService(bayiRepo, satisRepo, hedefRepo, urunRepo, stokRepo);
        var query = new BayilerOzetQuery(new DateOnly(2026, 1, 1), null, null);

        // ACT
        var result = await service.GetirOzetAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.ToplamGerceklesenCiro);
        Assert.Equal(1000m, result.Value.ToplamHedefCiro);
        // Hedefe ulaşma sadece B001 üzerinden: 500 / 1000 = 0.5000 (Hedefi olmayan B002 genel oranı şişirmemeli)
        Assert.Equal(0.5m, result.Value.GenelHedefeUlasmaPct);
    }

    #region Fakes

    private sealed class FakeBayiRepo : IBayiReadRepository
    {
        private readonly List<BayiReadModel> _bayiler;
        public FakeBayiRepo(params BayiReadModel[] bayiler) => _bayiler = bayiler.ToList();

        public Task<BayiReadModel?> GetByCodeAsync(string bayiKodu, CancellationToken cancellationToken)
            => Task.FromResult(_bayiler.FirstOrDefault(b => b.BayiKodu == bayiKodu));

        public Task<IReadOnlyList<BayiReadModel>> ListAsync(string? segment, string? bolge, CancellationToken cancellationToken)
        {
            IEnumerable<BayiReadModel> q = _bayiler;
            if (!string.IsNullOrWhiteSpace(segment)) q = q.Where(b => b.Segment == segment);
            if (!string.IsNullOrWhiteSpace(bolge)) q = q.Where(b => b.Bolge == bolge);
            return Task.FromResult<IReadOnlyList<BayiReadModel>>(q.ToList());
        }
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
