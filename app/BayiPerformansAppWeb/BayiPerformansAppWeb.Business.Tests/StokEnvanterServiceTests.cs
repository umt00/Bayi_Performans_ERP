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

public sealed class StokEnvanterServiceTests
{
    private static BusinessRulesOptions CreateOptions(decimal stokGunEsigi = 7m)
    {
        return new BusinessRulesOptions
        {
            StokGunEsigi = stokGunEsigi
        };
    }

    private static FakeTimeProvider CreateTimeProvider(DateTimeOffset? utcNow = null)
    {
        return new FakeTimeProvider(
            utcNow ?? new DateTimeOffset(2026, 1, 31, 12, 0, 0, TimeSpan.Zero));
    }

    private static StokEnvanterService CreateService(
        IStokReadRepository stokRepo,
        ISatisReadRepository satisRepo,
        IUrunReadRepository urunRepo,
        IDepoReadRepository depoRepo)
    {
        var options = CreateOptions();
        var timeProvider = CreateTimeProvider();

        return new StokEnvanterService(
            stokRepo,
            satisRepo,
            urunRepo,
            depoRepo,
            options,
            timeProvider);
    }

    [Fact]
    public async Task ListeleAsync_TumFiltrelerBosken_TumEnvanteriGetirmelidir()
    {
        // ARRANGE
        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 50m, 10m, null),
            new StokReadModel("U002", "D001", 0m, 10m, null));

        var satisRepo = new FakeSatisRepo();
        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 100m),
            new UrunReadModel("U002", "Kat2", "Alt2", 200m));

        var depoRepo = new FakeDepoRepo(
            new DepoReadModel("D001", "Istanbul"));

        var service = CreateService(stokRepo, satisRepo, urunRepo, depoRepo);
        var query = new StokEnvanterQuery(Sayfa: 1, SayfaBoyutu: 50);

        // ACT
        var result = await service.ListeleAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.ToplamKayitSayisi);
        Assert.Equal(2, result.Value.Stoklar.Count);
    }

    [Fact]
    public async Task ListeleAsync_DurumFiltresiTukendi_SadeceTukenenleriGetirmelidir()
    {
        // ARRANGE
        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 50m, 10m, null),
            new StokReadModel("U002", "D001", 0m, 10m, null));

        var satisRepo = new FakeSatisRepo();
        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 100m),
            new UrunReadModel("U002", "Kat2", "Alt2", 200m));

        var depoRepo = new FakeDepoRepo(new DepoReadModel("D001", "Ist"));

        var service = CreateService(stokRepo, satisRepo, urunRepo, depoRepo);
        var query = new StokEnvanterQuery(StokDurumu: "TUKENDI", Sayfa: 1, SayfaBoyutu: 50);

        // ACT
        var result = await service.ListeleAsync(query, CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var item = Assert.Single(result.Value!.Stoklar);
        Assert.Equal("U002", item.UrunKodu);
        Assert.True(item.KritikMi);
    }

    [Fact]
    public async Task GetirOzetAsync_DepoBazliKPIlariDogruHesaplamalidir()
    {
        // ARRANGE
        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 100m, 10m, null),
            new StokReadModel("U002", "D001", 0m, 10m, null),
            new StokReadModel("U001", "D002", 5m, 10m, null));

        var satisRepo = new FakeSatisRepo();
        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U001", "Kat1", "Alt1", 100m),
            new UrunReadModel("U002", "Kat2", "Alt2", 200m));

        var depoRepo = new FakeDepoRepo(
            new DepoReadModel("D001", "Ist"),
            new DepoReadModel("D002", "Izm"));

        var service = CreateService(stokRepo, satisRepo, urunRepo, depoRepo);

        // ACT
        var result = await service.GetirOzetAsync(new StokOzetQuery(null), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value!.ToplamStokKalemSayisi);
        Assert.Equal(105m, result.Value.ToplamFizikselStokAdedi);
        Assert.Equal(1, result.Value.TukenenKalemSayisi);
        Assert.Equal(2, result.Value.DepoBazliOzetler.Count);
    }

    [Fact]
    public async Task GetirOzetAsync_EksikMevcutStokVarsa_InvalidStockDataHatasiDonmelidir()
    {
        // ARRANGE
        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", null, 10m, null)); // MevcutStok null bozuk veri

        var satisRepo = new FakeSatisRepo();
        var urunRepo = new FakeUrunRepo();
        var depoRepo = new FakeDepoRepo();

        var service = CreateService(stokRepo, satisRepo, urunRepo, depoRepo);

        // ACT
        var result = await service.GetirOzetAsync(new StokOzetQuery(null), CancellationToken.None);

        // ASSERT
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("INVALID_STOCK_DATA", result.ErrorCode);
    }

    [Fact]
    public async Task GetirOzetAsync_EksikMiktarliSatisVarsa_InvalidSaleDataHatasiDonmelidir()
    {
        // ARRANGE
        var stokRepo = new FakeStokRepo(
            new StokReadModel("U001", "D001", 50m, 10m, null));

        var satisRepo = new FakeSatisRepo(
            new SatisReadModel(1, new DateOnly(2026, 1, 10), "B001", "U001", "D001", null, 100m, 0m, null, false)); // Miktar null

        var urunRepo = new FakeUrunRepo();
        var depoRepo = new FakeDepoRepo(new DepoReadModel("D001", "Ist"));

        var service = CreateService(stokRepo, satisRepo, urunRepo, depoRepo);

        // ACT
        var result = await service.GetirOzetAsync(new StokOzetQuery(null), CancellationToken.None);

        // ASSERT
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("INVALID_SALE_DATA", result.ErrorCode);
    }

    #region Fakes

    private sealed class FakeStokRepo : IStokReadRepository
    {
        private readonly List<StokReadModel> _stoklar;
        public FakeStokRepo(params StokReadModel[] stoklar) => _stoklar = stoklar.ToList();

        public Task<IReadOnlyList<StokReadModel>> ListAsync(string? depoKodu, string? urunKodu, CancellationToken cancellationToken)
        {
            IEnumerable<StokReadModel> q = _stoklar;
            if (!string.IsNullOrWhiteSpace(depoKodu)) q = q.Where(s => s.DepoKodu == depoKodu);
            if (!string.IsNullOrWhiteSpace(urunKodu)) q = q.Where(s => s.UrunKodu == urunKodu);
            return Task.FromResult<IReadOnlyList<StokReadModel>>(q.ToList());
        }

        public Task<StokReadModel?> GetAsync(string urunKodu, string depoKodu, CancellationToken cancellationToken)
            => Task.FromResult(_stoklar.FirstOrDefault(s => s.UrunKodu == urunKodu && s.DepoKodu == depoKodu));
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
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(Array.Empty<SatisReadModel>());
        public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(string urunKodu, string depoKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(Array.Empty<SatisReadModel>());
        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesForDealerAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DateOnly>>(Array.Empty<DateOnly>());
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

    private sealed class FakeDepoRepo : IDepoReadRepository
    {
        private readonly List<DepoReadModel> _depolar;
        public FakeDepoRepo(params DepoReadModel[] depolar) => _depolar = depolar.ToList();

        public Task<DepoReadModel?> GetByCodeAsync(string depoKodu, CancellationToken cancellationToken)
            => Task.FromResult(_depolar.FirstOrDefault(d => d.DepoKodu == depoKodu));
        public Task<IReadOnlyList<DepoReadModel>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DepoReadModel>>(_depolar);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    #endregion
}
