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

public sealed class SnapshotHistoryAndRiskTests
{
    private static BusinessRulesOptions CreateOptions() => new()
    {
        MarjEsigiPct = 0.15m,
        MaxIskontoPct = 0.15m,
        HedefRiskEsigiPct = 0.80m,
        StokGunEsigi = 7m,
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

    [Fact]
    public async Task RiskListesiService_SnapshotKullanildiginda_SehirBilgisiniPopuleEtmeliveFiltreleriUygulamalidir()
    {
        // Arrange
        var fakeSnapshotRepo = new FakeSnapshotRepo([
            new BayiDonemPerformansReadModel("B0001", new DateOnly(2026, 8, 1), "Alpha Bayi", "KOBİ", "Marmara", 100000m, 120000m, 83.33m, 18.5m, 5.0m, 2.0m, 1, 75, "YUKSEK", ["HEDEF_ALTINDA"], "İstanbul"),
            new BayiDonemPerformansReadModel("B0002", new DateOnly(2026, 8, 1), "Beta Bayi", "Kurumsal", "Ege", 50000m, 60000m, 83.33m, 10.0m, 12.0m, 0.0m, 0, 20, "DUSUK", [], "İzmir")
        ]);

        var service = new RiskListesiService(
            new FakeBayiRepo(),
            new ThrowingSatisRepo(),
            new FakeHedefRepo(),
            new FakeUrunRepo(),
            new FakeStokRepo(),
            new SalesMetricsCalculator(),
            new StockMetricsCalculator(7m),
            new RiskEngine(CreateOptions()),
            fakeSnapshotRepo);

        var query = new RiskListesiQuery(
            Donem: new DateOnly(2026, 8, 1),
            Arama: "Alpha",
            MinRiskSkoru: 50,
            Sayfa: 1,
            SayfaBoyutu: 50);

        // Act
        var result = await service.ListeleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.Bayiler);
        var item = result.Value.Bayiler[0];
        Assert.Equal("B0001", item.BayiKodu);
        Assert.Equal("İstanbul", item.Sehir);
        Assert.True(item.KritikStokVarMi);
        Assert.Equal(75, item.RiskSkoru);
    }

    [Fact]
    public async Task BayiGecmisService_SnapshotKullanildiginda_CanliSatisHesabinaDusmedenGecmisPerformansiDonmelidir()
    {
        // Arrange
        var fakeBayiRepo = new FakeBayiRepo(new BayiReadModel("B0001", "KOBİ", "Marmara", "İstanbul", "B0001 Bayisi"));
        var fakeSnapshotRepo = new FakeSnapshotRepo([
            new BayiDonemPerformansReadModel("B0001", new DateOnly(2026, 8, 1), "B0001 Bayisi", "KOBİ", "Marmara", 100000m, 120000m, 83.33m, 18.5m, 5.0m, 2.0m, 1, 25, "DUSUK", ["HEDEF_ALTINDA"], "İstanbul"),
            new BayiDonemPerformansReadModel("B0001", new DateOnly(2026, 7, 1), "B0001 Bayisi", "KOBİ", "Marmara", 95000m, 100000m, 95.0m, 19.0m, 4.0m, 1.0m, 0, 10, "DUSUK", [], "İstanbul")
        ]);

        var service = new BayiGecmisService(
            fakeBayiRepo,
            new ThrowingSatisRepo(),
            new FakeHedefRepo(),
            new FakeUrunRepo(),
            new FakeStokRepo(),
            new SalesMetricsCalculator(),
            new StockMetricsCalculator(7m),
            new RiskEngine(CreateOptions()),
            fakeSnapshotRepo);

        // Act
        var result = await service.GetirGecmisAsync(new BayiGecmisQuery("B0001", 60), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("B0001", result.Value.BayiKodu);
        Assert.Equal(2, result.Value.AylikPerformanslar.Count);
    }

    [Fact]
    public async Task BayiGecmisService_BayiBulunamadiginda_404Donmelidir()
    {
        // Arrange
        var fakeBayiRepo = new FakeBayiRepo();
        var fakeSnapshotRepo = new FakeSnapshotRepo([]);

        var service = new BayiGecmisService(
            fakeBayiRepo,
            new ThrowingSatisRepo(),
            new FakeHedefRepo(),
            new FakeUrunRepo(),
            new FakeStokRepo(),
            new SalesMetricsCalculator(),
            new StockMetricsCalculator(7m),
            new RiskEngine(CreateOptions()),
            fakeSnapshotRepo);

        // Act
        var result = await service.GetirGecmisAsync(new BayiGecmisQuery("B9999", 60), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        Assert.Equal("BAYI_BULUNAMADI", result.ErrorCode);
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

        public Task<IReadOnlyList<string>> ListDistinctCitiesAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(_bayiler.Select(b => b.Sehir ?? "").Where(s => s != "").Distinct().ToList());

        public Task<IReadOnlyList<string>> ListDistinctRegionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(_bayiler.Select(b => b.Bolge ?? "").Where(s => s != "").Distinct().ToList());

        public Task<IReadOnlyList<string>> ListDistinctSegmentsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<string>>(_bayiler.Select(b => b.Segment ?? "").Where(s => s != "").Distinct().ToList());
    }

    private sealed class FakeSnapshotRepo : IBayiDonemPerformansReadRepository
    {
        private readonly List<BayiDonemPerformansReadModel> _rows;
        public BayiDonemPerformansQuery? LastQuery { get; private set; }

        public FakeSnapshotRepo(IEnumerable<BayiDonemPerformansReadModel> rows)
        {
            _rows = rows.ToList();
        }

        public Task<BayiDonemPerformansPage> ListAsync(BayiDonemPerformansQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            var filtered = _rows.AsEnumerable();
            if (query.Donem.HasValue) filtered = filtered.Where(r => r.Donem == query.Donem.Value);
            var list = filtered.ToList();

            return Task.FromResult(new BayiDonemPerformansPage(
                query.Donem ?? _rows.FirstOrDefault()?.Donem,
                list.Count,
                query.Sayfa,
                query.SayfaBoyutu,
                list));
        }

        public Task<BayiDonemPerformansReadModel?> GetAsync(string bayiKodu, DateOnly? donem, CancellationToken cancellationToken)
        {
            return Task.FromResult(_rows.FirstOrDefault(r => r.BayiKodu == bayiKodu && (!donem.HasValue || r.Donem == donem.Value)));
        }

        public Task<PerformansDashboardStatsReadModel> GetDashboardStatsAsync(DateOnly? donem, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PerformansDashboardStatsReadModel(donem, _rows.Count, 0m, 0m, 0, 0, null, null, null));
        }

        public Task<IReadOnlyList<BayiDonemPerformansReadModel>> GetHistoryAsync(string bayiKodu, int limit, CancellationToken cancellationToken)
        {
            var history = _rows.Where(r => r.BayiKodu == bayiKodu).OrderByDescending(r => r.Donem).Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<BayiDonemPerformansReadModel>>(history);
        }
    }

    private sealed class ThrowingSatisRepo : ISatisReadRepository
    {
        public Task<DateOnly?> GetLatestSaleDateAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");

        public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");

        public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");

        public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(string urunKodu, string depoKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesForDealerAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");

        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("Canlı satış hesabı çağrılmamalıdır!");
    }

    private sealed class FakeHedefRepo : IAylikHedefReadRepository
    {
        public Task<AylikHedefReadModel?> GetAsync(string bayiKodu, int yil, int ay, CancellationToken cancellationToken) => Task.FromResult<AylikHedefReadModel?>(null);
        public Task<IReadOnlyList<AylikHedefReadModel>> ListByPeriodAsync(int yil, int ay, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AylikHedefReadModel>>([]);
    }

    private sealed class FakeUrunRepo : IUrunReadRepository
    {
        public Task<UrunReadModel?> GetByCodeAsync(string urunKodu, CancellationToken cancellationToken) => Task.FromResult<UrunReadModel?>(null);
        public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(IReadOnlyCollection<string> urunKodlari, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UrunReadModel>>([]);
        public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UrunReadModel>>([]);
    }

    private sealed class FakeStokRepo : IStokReadRepository
    {
        public Task<IReadOnlyList<StokReadModel>> ListAsync(string? depoKodu, string? urunKodu, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StokReadModel>>([]);
        public Task<StokReadModel?> GetAsync(string urunKodu, string depoKodu, CancellationToken cancellationToken) => Task.FromResult<StokReadModel?>(null);
    }

    #endregion
}
