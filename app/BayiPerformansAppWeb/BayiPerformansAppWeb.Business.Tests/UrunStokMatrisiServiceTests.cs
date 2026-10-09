using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class UrunStokMatrisiServiceTests
{
    private readonly BusinessRulesOptions _options = new() { StokGunEsigi = 15m };

    [Fact]
    public async Task GetirMatrisAsync_UrunlerinTumDepolardakiStoklariniVeToplaminiDonmelidir()
    {
        // ARRANGE
        var urunler = new List<UrunReadModel>
        {
            new("U001", "Bilgisayar", "Laptop", 1000m, "Dizustu Bilgisayar"),
            new("U002", "Bilesen", "RAM", 200m, "16GB RAM")
        };

        var depolar = new List<DepoReadModel>
        {
            new("DP01", "Marmara", "Ana Depo", "Istanbul"),
            new("DP02", "Ege", "Izmir Depo", "Izmir")
        };

        var stoklar = new List<StokReadModel>
        {
            new("U001", "DP01", 50m, 10m, new DateOnly(2025, 6, 1)),
            new("U001", "DP02", 5m, 10m, new DateOnly(2025, 6, 1)), // kritik stok
            new("U002", "DP01", 100m, 20m, new DateOnly(2025, 6, 1))
        };

        var satislar = new List<SatisReadModel>
        {
            new("1", new DateOnly(2025, 6, 10), "B001", "U001", "DP01", 10m, 1200m, 0.05m, "Online", false)
        };

        var service = CreateService(urunler, depolar, stoklar, satislar);

        // ACT
        var result = await service.GetirMatrisAsync(new UrunStokMatrisiQuery(), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.ToplamUrunSayisi);
        Assert.Equal(2, result.Value.Depolar.Count);

        var u001 = result.Value.Urunler.First(u => u.UrunKodu == "U001");
        Assert.Equal("Dizustu Bilgisayar", u001.UrunAdi);
        Assert.Equal(55m, u001.ToplamMevcutStok); // 50 + 5
        Assert.Equal(1, u001.KritikDepoSayisi); // DP02 mevcut 5 < min 10 -> kritik
        Assert.Equal(2, u001.DepoStoklari.Count);

        var dp01Detay = u001.DepoStoklari.First(d => d.DepoKodu == "DP01");
        Assert.Equal(50m, dp01Detay.MevcutStok);
        Assert.False(dp01Detay.KritikMi);

        var dp02Detay = u001.DepoStoklari.First(d => d.DepoKodu == "DP02");
        Assert.Equal(5m, dp02Detay.MevcutStok);
        Assert.True(dp02Detay.KritikMi);
    }

    [Fact]
    public async Task GetirMatrisAsync_UrunKodlariFiltresiIleSadeceBelirtilenUrunleriGetirmelidir()
    {
        // ARRANGE
        var urunler = new List<UrunReadModel>
        {
            new("U001", "Bilgisayar", "Laptop", 1000m, "Laptop 1"),
            new("U002", "Bilesen", "RAM", 200m, "RAM 1"),
            new("U003", "Bilesen", "SSD", 300m, "SSD 1")
        };

        var depolar = new List<DepoReadModel> { new("DP01", "Marmara", "Ana Depo", "Istanbul") };
        var stoklar = new List<StokReadModel>
        {
            new("U001", "DP01", 10m, 5m, null),
            new("U002", "DP01", 20m, 5m, null),
            new("U003", "DP01", 30m, 5m, null)
        };

        var service = CreateService(urunler, depolar, stoklar, []);

        // ACT
        var result = await service.GetirMatrisAsync(new UrunStokMatrisiQuery(UrunKodlari: "U001,U003"), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.ToplamUrunSayisi);
        Assert.Contains(result.Value.Urunler, u => u.UrunKodu == "U001");
        Assert.Contains(result.Value.Urunler, u => u.UrunKodu == "U003");
        Assert.DoesNotContain(result.Value.Urunler, u => u.UrunKodu == "U002");
    }

    private UrunStokMatrisiService CreateService(
        List<UrunReadModel> urunler,
        List<DepoReadModel> depolar,
        List<StokReadModel> stoklar,
        List<SatisReadModel> satislar)
    {
        var urunRepo = new FakeUrunRepo(urunler);
        var depoRepo = new FakeDepoRepo(depolar);
        var stokRepo = new FakeStokRepo(stoklar);
        var satisRepo = new FakeSatisRepo(satislar);

        return new UrunStokMatrisiService(
            urunRepo, depoRepo, stokRepo, satisRepo,
            _options, TimeProvider.System);
    }

    #region Fakes

    private sealed class FakeUrunRepo(List<UrunReadModel> urunler) : IUrunReadRepository
    {
        public Task<UrunReadModel?> GetByCodeAsync(string urunKodu, CancellationToken cancellationToken)
            => Task.FromResult(urunler.FirstOrDefault(u => u.UrunKodu == urunKodu));
        public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(IReadOnlyCollection<string> urunKodlari, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UrunReadModel>>(urunler.Where(u => urunKodlari.Contains(u.UrunKodu)).ToList());
        public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<UrunReadModel>>(urunler);
    }

    private sealed class FakeDepoRepo(List<DepoReadModel> depolar) : IDepoReadRepository
    {
        public Task<DepoReadModel?> GetByCodeAsync(string depoKodu, CancellationToken cancellationToken)
            => Task.FromResult(depolar.FirstOrDefault(d => d.DepoKodu == depoKodu));
        public Task<IReadOnlyList<DepoReadModel>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DepoReadModel>>(depolar);
    }

    private sealed class FakeStokRepo(List<StokReadModel> stoklar) : IStokReadRepository
    {
        public Task<StokReadModel?> GetAsync(string urunKodu, string depoKodu, CancellationToken cancellationToken)
            => Task.FromResult(stoklar.FirstOrDefault(s => s.UrunKodu == urunKodu && s.DepoKodu == depoKodu));
        public Task<IReadOnlyList<StokReadModel>> ListAsync(string? depoKodu, string? urunKodu, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StokReadModel>>(stoklar);
    }

    private sealed class FakeSatisRepo(List<SatisReadModel> satislar) : ISatisReadRepository
    {
        public Task<DateOnly?> GetLatestSaleDateAsync(CancellationToken cancellationToken)
            => Task.FromResult<DateOnly?>(satislar.Count > 0 ? satislar.Max(s => s.Tarih) : null);
        public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(DateOnly start, DateOnly endExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(satislar.Where(s => s.Tarih >= start && s.Tarih < endExclusive).ToList());
        public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(satislar.Where(s => s.BayiKodu == bayiKodu && s.Tarih >= periodStart && s.Tarih < periodEndExclusive).ToList());
        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DateOnly>>(satislar.Select(s => new DateOnly(s.Tarih.Year, s.Tarih.Month, 1)).Distinct().ToList());
        public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(string urunKodu, string depoKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(satislar.Where(s => s.UrunKodu == urunKodu && s.DepoKodu == depoKodu && s.Tarih >= periodStart && s.Tarih < periodEndExclusive).ToList());
        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesByPeriodAsync(DateOnly start, DateOnly endExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(
                new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesForDealerAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(
                new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
    }

    #endregion
}
