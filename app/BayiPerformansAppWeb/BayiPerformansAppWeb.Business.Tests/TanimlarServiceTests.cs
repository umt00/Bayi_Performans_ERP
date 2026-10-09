using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;
using Xunit;

namespace BayiPerformansAppWeb.Tests.Business.Services;

public sealed class TanimlarServiceTests
{
    [Fact]
    public async Task ListeleBayilerAsync_FiltreleriRepositoryeAktaripSonucuDonmelidir()
    {
        var bayiRepo = new FakeBayiRepo(
            new BayiReadModel("B1", "Kurumsal", "Ege", "Izmir", "Bayi 1"),
            new BayiReadModel("B2", "KOBİ", "Marmara", "Istanbul", "Bayi 2"));
        var service = new TanimlarService(
            bayiRepo,
            new FakeDepoRepo(),
            new FakeUrunRepo(),
            new FakeSatisRepo());

        var result = await service.ListeleBayilerAsync(
            "Kurumsal",
            "Ege",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var bayi = Assert.Single(result.Value!);
        Assert.Equal("B1", bayi.BayiKodu);
        Assert.Equal("Kurumsal", bayiRepo.LastSegment);
        Assert.Equal("Ege", bayiRepo.LastBolge);
    }

    [Fact]
    public async Task ListeleDepolarAsync_RepositorySonucunuDonmelidir()
    {
        var service = new TanimlarService(
            new FakeBayiRepo(),
            new FakeDepoRepo(
                new DepoReadModel("DP01", "Marmara", "Merkez Depo", "Istanbul"),
                new DepoReadModel("DP02", "Ege", "Ege Depo", "Izmir")),
            new FakeUrunRepo(),
            new FakeSatisRepo());

        var result = await service.ListeleDepolarAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("DP01", result.Value[0].DepoKodu);
    }

    [Fact]
    public async Task GetirFiltreSecenekleriAsync_VeriTabanindakiDegerleriGruplayipTekilSeceneklerOlarakDonmelidir()
    {
        // ARRANGE
        var bayiRepo = new FakeBayiRepo(
            new BayiReadModel("B1", "Kurumsal", "Ege"),
            new BayiReadModel("B2", "KOBİ", "Marmara"),
            new BayiReadModel("B3", "Bireysel", "Ege"));

        var depoRepo = new FakeDepoRepo(
            new DepoReadModel("D1", "Istanbul"),
            new DepoReadModel("D2", "Izmir"));

        var urunRepo = new FakeUrunRepo(
            new UrunReadModel("U1", "Elektronik", "Laptop", 1000m),
            new UrunReadModel("U2", "Elektronik", "Telefon", 500m),
            new UrunReadModel("U3", "Mobilya", "Masa", 200m));

        var satisRepo = new FakeSatisRepo(
            new DateOnly(2026, 1, 1),
            new DateOnly(2025, 12, 1));

        var service = new TanimlarService(bayiRepo, depoRepo, urunRepo, satisRepo);

        // ACT
        var result = await service.GetirFiltreSecenekleriAsync(CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var secenekler = result.Value!;

        // Bolgeler tekil ve sirali olmali (Bayi ve Depo bolgeleri)
        Assert.Equal(new[] { "Ege", "Istanbul", "Izmir", "Marmara" }, secenekler.Bolgeler);

        // Segmentler tekil ve sirali olmali
        Assert.Equal(new[] { "Bireysel", "KOBİ", "Kurumsal" }, secenekler.Segmentler);

        // Depolar 2 adet olmali
        Assert.Equal(2, secenekler.Depolar.Count);
        Assert.Equal("D1", secenekler.Depolar[0].DepoKodu);

        // Kategoriler 2 adet olmali (Elektronik -> Laptop, Telefon; Mobilya -> Masa)
        Assert.Equal(2, secenekler.Kategoriler.Count);
        var elektronik = secenekler.Kategoriler.First(k => k.Kategori == "Elektronik");
        Assert.Equal(2, elektronik.AltKategoriler.Count);

        // Donemler
        Assert.Equal(2, secenekler.Donemler.Count);

        // Gerekce kodlari
        Assert.Contains(secenekler.BayiRiskGerekceKodlari, k => k == "HEDEF_ALTINDA");
        Assert.Contains(secenekler.BayiRiskGerekceKodlari, k => k == "MARJ_ESIK_ALTI");
        Assert.Contains(secenekler.StokRiskGerekceKodlari, k => k == "STOK_YETERSIZ");
    }

    #region Fakes

    private sealed class FakeBayiRepo : IBayiReadRepository
    {
        private readonly List<BayiReadModel> _bayiler;
        public FakeBayiRepo(params BayiReadModel[] bayiler) => _bayiler = bayiler.ToList();
        public string? LastSegment { get; private set; }
        public string? LastBolge { get; private set; }

        public Task<BayiReadModel?> GetByCodeAsync(string bayiKodu, CancellationToken cancellationToken)
            => Task.FromResult(_bayiler.FirstOrDefault(b => b.BayiKodu == bayiKodu));
        public Task<IReadOnlyList<BayiReadModel>> ListAsync(string? segment, string? bolge, CancellationToken cancellationToken)
        {
            LastSegment = segment;
            LastBolge = bolge;

            IEnumerable<BayiReadModel> query = _bayiler;
            if (!string.IsNullOrWhiteSpace(segment))
            {
                query = query.Where(b => string.Equals(b.Segment, segment, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(bolge))
            {
                query = query.Where(b => string.Equals(b.Bolge, bolge, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<BayiReadModel>>(query.ToArray());
        }
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

    private sealed class FakeSatisRepo : ISatisReadRepository
    {
        private readonly List<DateOnly> _donemler;
        public FakeSatisRepo(params DateOnly[] donemler) => _donemler = donemler.ToList();

        public Task<DateOnly?> GetLatestSaleDateAsync(CancellationToken cancellationToken) => Task.FromResult<DateOnly?>(null);
        public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(Array.Empty<SatisReadModel>());
        public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(Array.Empty<SatisReadModel>());
        public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(string urunKodu, string depoKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SatisReadModel>>(Array.Empty<SatisReadModel>());
        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesByPeriodAsync(DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>> GetTotalSalesQuantitiesForDealerAsync(string bayiKodu, DateOnly periodStart, DateOnly periodEndExclusive, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DateOnly>>(_donemler);
    }

    #endregion
}
