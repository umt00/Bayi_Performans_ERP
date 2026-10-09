using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Xunit;

namespace BayiPerformansAppWeb.Tests.Business.Services;

public sealed class KritikStokServiceTests
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

    private static KritikStokService CreateService(
        TestStokReadRepository stokRepository,
        TestSatisReadRepository satisRepository,
        BusinessRulesOptions? options = null,
        TimeProvider? timeProvider = null,
        IUrunReadRepository? urunRepository = null)
    {
        return new KritikStokService(
            stokRepository,
            satisRepository,
            options ?? CreateOptions(),
            timeProvider ?? CreateTimeProvider(),
            urunRepository);
    }

    #region Constructor Null Check Tests

    [Fact]
    public void Constructor_NullStokRepository_ArgumentNullExceptionFirlatmalidir()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KritikStokService(
                null!,
                new TestSatisReadRepository(),
                CreateOptions(),
                CreateTimeProvider()));
    }

    [Fact]
    public void Constructor_NullSatisRepository_ArgumentNullExceptionFirlatmalidir()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KritikStokService(
                new TestStokReadRepository(),
                null!,
                CreateOptions(),
                CreateTimeProvider()));
    }

    [Fact]
    public void Constructor_NullOptions_ArgumentNullExceptionFirlatmalidir()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KritikStokService(
                new TestStokReadRepository(),
                new TestSatisReadRepository(),
                null!,
                CreateTimeProvider()));
    }

    [Fact]
    public void Constructor_NullTimeProvider_ArgumentNullExceptionFirlatmalidir()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KritikStokService(
                new TestStokReadRepository(),
                new TestSatisReadRepository(),
                CreateOptions(),
                null!));
    }

    #endregion

    #region Controlled ServiceResult Fail (Null Safety) Tests

    [Fact]
    public async Task ListeleAsync_NullQueryGonderildiginde_ControlledFailResultDonmelidir()
    {
        // ARRANGE
        var stokRepository = new TestStokReadRepository();
        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        var result = await service.ListeleAsync(null!, CancellationToken.None);

        // ASSERT
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Equal("QUERY_NULL", result.ErrorCode);
    }

    [Fact]
    public async Task ListeleAsync_MevcutStokNullOlanKayitVarsa_ControlledFailResultDonmelidir()
    {
        // ARRANGE
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-001", "DEPO-001", null, 10m, null) }
        };

        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("INVALID_STOCK_DATA", result.ErrorCode);
    }

    [Fact]
    public async Task ListeleAsync_SatisMiktariNullOlanKayitVarsa_ControlledFailResultDonmelidir()
    {
        // ARRANGE
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-001", "DEPO-001", 10m, 5m, null) }
        };

        var satisRepository = new TestSatisReadRepository
        {
            Satislar = new[]
            {
                new SatisReadModel(1, new DateOnly(2026, 1, 10), "B001", "URUN-001", "DEPO-001", null, 10m, 0m, null, false)
            }
        };

        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("INVALID_SALE_DATA", result.ErrorCode);
    }

    #endregion

    #region Temel Hesaplama ve İş Mantığı Testleri

    [Fact]
    public async Task ListeleAsync_GecerliVerilerde_BasariliKritikStokListesiDonmelidir()
    {
        // ARRANGE
        DateTimeOffset hesaplamaTarihi = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[]
            {
                new StokReadModel("URUN-001", "DEPO-001", 50m, 20m, null)
            }
        };

        var satisRepository = new TestSatisReadRepository
        {
            Satislar = new[]
            {
                new SatisReadModel(1, new DateOnly(2026, 1, 15), "B001", "URUN-001", "DEPO-001", 900m, 10m, 0m, null, false)
            }
        };

        KritikStokService service = CreateService(stokRepository, satisRepository, CreateOptions(7m), CreateTimeProvider(hesaplamaTarihi));

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(hesaplamaTarihi, result.Value!.HesaplamaTarihi);
        Assert.Single(result.Value.Urunler);

        var item = result.Value.Urunler[0];
        Assert.Equal("URUN-001", item.UrunKodu);
        Assert.Equal("DEPO-001", item.DepoKodu);
        Assert.Equal(50m, item.MevcutStok);
        Assert.Equal(20m, item.MinStokSeviyesi);
        Assert.Equal(900m, item.SonUcAySatisAdedi);
        Assert.Equal(10m, item.OrtalamaGunlukSatis);
        Assert.Equal(5m, item.TahminiStokGunu);
        Assert.Contains("STOK_YETERSIZ", item.GerekceKodlari);
    }

    [Fact]
    public async Task ListeleAsync_MinStokAltindaAmaSatisOlmayanUrun_KritikOlarakDonmelidir()
    {
        // ARRANGE
        // Satış 0 ama MevcutStok (5) < MinStokSeviyesi (10) -> Kritik Stok sayılmalıdır.
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-NO-SALE", "DEPO-001", 5m, 10m, null) }
        };

        var satisRepository = new TestSatisReadRepository { Satislar = Array.Empty<SatisReadModel>() };

        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var item = Assert.Single(result.Value!.Urunler);
        Assert.Equal("URUN-NO-SALE", item.UrunKodu);
        Assert.Equal(0m, item.SonUcAySatisAdedi);
        Assert.Null(item.OrtalamaGunlukSatis);
        Assert.Null(item.TahminiStokGunu);
        Assert.Contains("STOK_YETERSIZ", item.GerekceKodlari);
    }

    [Fact]
    public async Task ListeleAsync_StokKritikDegilse_ListedeYerAlmamalidir()
    {
        // ARRANGE
        // Mevcut stok (100) > Min Stok (20) ve Stok Günü (100 / (90/90) = 100 gün > 7 gün eşiği) -> Kritik DEĞİL.
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-SAFE", "DEPO-001", 100m, 20m, null) }
        };

        var satisRepository = new TestSatisReadRepository
        {
            Satislar = new[] { new SatisReadModel(1, new DateOnly(2026, 1, 15), "B001", "URUN-SAFE", "DEPO-001", 90m, 10m, 0m, null, false) }
        };

        KritikStokService service = CreateService(stokRepository, satisRepository, CreateOptions(7m));

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!.Urunler);
    }

    [Fact]
    public async Task ListeleAsync_IadeSatislariniEksiOlarakHesaplamalidir()
    {
        // ARRANGE
        // 100 Adet Satış - 20 Adet İade = Net 80 Adet Satış.
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-001", "DEPO-001", 5m, 10m, null) }
        };

        var satisRepository = new TestSatisReadRepository
        {
            Satislar = new[]
            {
                new SatisReadModel(1, new DateOnly(2026, 1, 10), "B001", "URUN-001", "DEPO-001", 100m, 10m, 0m, null, false),
                new SatisReadModel(2, new DateOnly(2026, 1, 15), "B001", "URUN-001", "DEPO-001", 20m, 10m, 0m, null, true)
            }
        };

        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Urunler);
        Assert.Equal(80m, item.SonUcAySatisAdedi);
    }

    [Fact]
    public async Task ListeleAsync_CiktiyiUrunVeDepoKodunaGoreDeterministikSiralamalidir()
    {
        // ARRANGE
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[]
            {
                new StokReadModel("URUN-B", "DEPO-002", 5m, 10m, null),
                new StokReadModel("URUN-A", "DEPO-002", 5m, 10m, null),
                new StokReadModel("URUN-A", "DEPO-001", 5m, 10m, null)
            }
        };

        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        var result = await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Urunler.Count);

        // Beklenen sıralama: (URUN-A, DEPO-001) -> (URUN-A, DEPO-002) -> (URUN-B, DEPO-002)
        Assert.Equal("URUN-A", result.Value.Urunler[0].UrunKodu);
        Assert.Equal("DEPO-001", result.Value.Urunler[0].DepoKodu);

        Assert.Equal("URUN-A", result.Value.Urunler[1].UrunKodu);
        Assert.Equal("DEPO-002", result.Value.Urunler[1].DepoKodu);

        Assert.Equal("URUN-B", result.Value.Urunler[2].UrunKodu);
        Assert.Equal("DEPO-002", result.Value.Urunler[2].DepoKodu);
    }

    #endregion

    #region Sorgu, Tarih ve Performans (N+1) Doğrulamaları

    [Fact]
    public async Task ListeleAsync_FiltreParametreleriniRepositoryeAynenAktarmalidir()
    {
        // ARRANGE
        var stokRepository = new TestStokReadRepository();
        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository);

        var query = new KritikStokQuery("DEPO-99", "URUN-99");

        // ACT
        await service.ListeleAsync(query, CancellationToken.None);

        // ASSERT
        Assert.Equal("DEPO-99", stokRepository.PassedDepoKodu);
        Assert.Equal("URUN-99", stokRepository.PassedUrunKodu);
    }

    [Fact]
    public async Task ListeleAsync_CancellationTokenRepositorylereAktarmalidir()
    {
        // ARRANGE
        // Erken dönüşü engellemek için stok verisi ekliyoruz
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-001", "DEPO-001", 10m, 5m, null) }
        };
        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository);

        using var cts = new CancellationTokenSource();

        // ACT
        await service.ListeleAsync(new KritikStokQuery(null, null), cts.Token);

        // ASSERT
        Assert.Equal(cts.Token, stokRepository.PassedCancellationToken);
        Assert.Equal(cts.Token, satisRepository.PassedCancellationToken);
    }

    [Fact]
    public async Task ListeleAsync_NPlusOneSorgusuYapmamalidir_SatislarTekSorgudaGetirilmelidir()
    {
        // ARRANGE
        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[]
            {
                new StokReadModel("URUN-001", "DEPO-001", 5m, 10m, null),
                new StokReadModel("URUN-002", "DEPO-001", 5m, 10m, null),
                new StokReadModel("URUN-003", "DEPO-001", 5m, 10m, null)
            }
        };

        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository);

        // ACT
        await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        // N+1 olmadığını doğrula: ListByPeriodAsync tam olarak 1 kez çağrılmış olmalı.
        Assert.Equal(1, satisRepository.ListByPeriodCallCount);
        Assert.Equal(0, satisRepository.ListByProductWarehouseAndPeriodCallCount);
    }

    [Fact]
    public async Task ListeleAsync_90GunlukTarihAraliginiDogruHesaplamalidir()
    {
        // ARRANGE
        DateTimeOffset calculationDate = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero); // 31 Ocak 2026
        var timeProvider = CreateTimeProvider(calculationDate);

        var stokRepository = new TestStokReadRepository
        {
            Stoklar = new[] { new StokReadModel("URUN-001", "DEPO-001", 5m, 10m, null) }
        };

        var satisRepository = new TestSatisReadRepository();
        KritikStokService service = CreateService(stokRepository, satisRepository, timeProvider: timeProvider);

        // ACT
        await service.ListeleAsync(new KritikStokQuery(null, null), CancellationToken.None);

        // ASSERT
        // 31 Ocak 2026 tarihinden 89 gün öncesi 3 Kasım 2025'tir. Exclusive bitiş ise 1 Şubat 2026 olmalıdır.
        Assert.Equal(new DateOnly(2025, 11, 3), satisRepository.LastPeriodStart);
        Assert.Equal(new DateOnly(2026, 2, 1), satisRepository.LastPeriodEndExclusive);
    }

    #endregion
}

#region Test Doubles / Fakes

internal sealed class TestStokReadRepository : IStokReadRepository
{
    public IReadOnlyList<StokReadModel> Stoklar { get; set; } = Array.Empty<StokReadModel>();
    public string? PassedDepoKodu { get; private set; }
    public string? PassedUrunKodu { get; private set; }
    public CancellationToken PassedCancellationToken { get; private set; }

    public Task<IReadOnlyList<StokReadModel>> ListAsync(
        string? depoKodu,
        string? urunKodu,
        CancellationToken cancellationToken)
    {
        PassedDepoKodu = depoKodu;
        PassedUrunKodu = urunKodu;
        PassedCancellationToken = cancellationToken;
        return Task.FromResult(Stoklar);
    }

    public Task<StokReadModel?> GetAsync(
        string urunKodu,
        string depoKodu,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<StokReadModel?>(
            Stoklar.FirstOrDefault(x => x.UrunKodu == urunKodu && x.DepoKodu == depoKodu));
    }
}

internal sealed class TestSatisReadRepository : ISatisReadRepository
{
    public IReadOnlyList<SatisReadModel> Satislar { get; set; } = Array.Empty<SatisReadModel>();
    public int ListByPeriodCallCount { get; private set; }
    public int ListByProductWarehouseAndPeriodCallCount { get; private set; }
    public DateOnly LastPeriodStart { get; private set; }
    public DateOnly LastPeriodEndExclusive { get; private set; }
    public CancellationToken PassedCancellationToken { get; private set; }

    public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        ListByPeriodCallCount++;
        LastPeriodStart = periodStart;
        LastPeriodEndExclusive = periodEndExclusive;
        PassedCancellationToken = cancellationToken;
        return Task.FromResult(Satislar);
    }

    public Task<DateOnly?> GetLatestSaleDateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<DateOnly?>(null);
    }

    public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DateOnly> result = Satislar
            .Select(s => new DateOnly(s.Tarih.Year, s.Tarih.Month, 1))
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(
        string bayiKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Array.Empty<SatisReadModel>() as IReadOnlyList<SatisReadModel>);
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(
        string urunKodu,
        string depoKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        ListByProductWarehouseAndPeriodCallCount++;
        return Task.FromResult(Array.Empty<SatisReadModel>() as IReadOnlyList<SatisReadModel>);
    }

    public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesByPeriodAsync(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        return Task.FromResult(Aggregate(periodStart, periodEndExclusive));
    }

    public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesForDealerAsync(
            string bayiKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        return Task.FromResult(Aggregate(periodStart, periodEndExclusive, bayiKodu));
    }

    private IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>
        Aggregate(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            string? bayiKodu = null)
    {
        return Satislar
            .Where(sale =>
                sale.Tarih >= periodStart
                && sale.Tarih < periodEndExclusive
                && (bayiKodu is null || sale.BayiKodu == bayiKodu)
                && sale.Miktar.HasValue)
            .GroupBy(sale => (sale.UrunKodu, sale.DepoKodu))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(sale =>
                    sale.IsIade == true
                        ? -Math.Abs(sale.Miktar!.Value)
                        : sale.Miktar!.Value));
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}

#endregion
