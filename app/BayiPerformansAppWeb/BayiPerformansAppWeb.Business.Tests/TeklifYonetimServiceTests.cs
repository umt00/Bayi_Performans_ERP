using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Repositories;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class TeklifYonetimServiceTests
{
    private sealed class FakeBayiRepo : IBayiReadRepository
    {
        public Task<IReadOnlyList<BayiReadModel>> ListAsync(string? segment, string? bolge, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BayiReadModel>>(new List<BayiReadModel>
            {
                new("B001", "Kurumsal", "Marmara", "Istanbul", "Bayi 1")
            });

        public Task<BayiReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default)
        {
            if (kod == "B001")
            {
                return Task.FromResult<BayiReadModel?>(new BayiReadModel("B001", "Kurumsal", "Marmara", "Istanbul", "Bayi 1"));
            }
            return Task.FromResult<BayiReadModel?>(null);
        }
    }

    private sealed class FakeUrunRepo : IUrunReadRepository
    {
        public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UrunReadModel>>(new List<UrunReadModel>
            {
                new("U001", "Elektronik", "Bilgisayar", 10000m, "Laptop", "Penta"),
                new("U002", "Elektronik", "Aksesuar", 200m, "Mouse", "Penta")
            });

        public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
            ListAllAsync(ct);

        public Task<UrunReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default)
        {
            if (kod == "U001") return Task.FromResult<UrunReadModel?>(new UrunReadModel("U001", "Elektronik", "Bilgisayar", 10000m, "Laptop", "Penta"));
            if (kod == "U002") return Task.FromResult<UrunReadModel?>(new UrunReadModel("U002", "Elektronik", "Aksesuar", 200m, "Mouse", "Penta"));
            return Task.FromResult<UrunReadModel?>(null);
        }
    }

    private sealed class FakeDepoRepo : IDepoReadRepository
    {
        public Task<IReadOnlyList<DepoReadModel>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DepoReadModel>>(new List<DepoReadModel>
            {
                new("DP01", "Marmara", "Merkez Depo", "Istanbul")
            });

        public Task<DepoReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default)
        {
            if (kod == "DP01") return Task.FromResult<DepoReadModel?>(new DepoReadModel("DP01", "Marmara", "Merkez Depo", "Istanbul"));
            return Task.FromResult<DepoReadModel?>(null);
        }
    }

    private sealed class FakeStokRepo : IStokReadRepository
    {
        public Task<IReadOnlyList<StokReadModel>> ListAsync(string? depoKodu, string? urunKodu, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StokReadModel>>(new List<StokReadModel>
            {
                new("U001", "DP01", 50m, 5m, DateOnly.FromDateTime(DateTime.UtcNow)),
                new("U002", "DP01", 100m, 10m, DateOnly.FromDateTime(DateTime.UtcNow))
            });

        public Task<StokReadModel?> GetAsync(string urunKodu, string depoKodu, CancellationToken ct = default)
        {
            if (urunKodu == "U001" && depoKodu == "DP01")
                return Task.FromResult<StokReadModel?>(new StokReadModel("U001", "DP01", 50m, 5m, DateOnly.FromDateTime(DateTime.UtcNow)));
            if (urunKodu == "U002" && depoKodu == "DP01")
                return Task.FromResult<StokReadModel?>(new StokReadModel("U002", "DP01", 100m, 10m, DateOnly.FromDateTime(DateTime.UtcNow)));
            return Task.FromResult<StokReadModel?>(null);
        }
    }

    private sealed class FakePerformansRepo : IBayiDonemPerformansReadRepository
    {
        private readonly BayiDonemPerformansReadModel _model = new(
            BayiKodu: "B001",
            Donem: new DateOnly(2026, 8, 1),
            BayiAdi: "Bayi 1",
            Segment: "Kurumsal",
            Bolge: "Marmara",
            Ciro: 92500m,
            Hedef: 100000m,
            HedefTutmaOrani: 92.5m,
            OrtalamaKarMarji: 18.25m,
            OrtalamaIskontoOrani: 0.5m,
            OrtalamaIadeOrani: 1.25m,
            KritikStokKalanUrunSayisi: 1,
            RiskSkoru: 25,
            RiskSeviyesi: "DUSUK",
            GerekceKodlari: new[] { "PERFORMANS_RISKI" });

        public Task<BayiDonemPerformansPage> ListAsync(
            BayiDonemPerformansQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BayiDonemPerformansPage(
                _model.Donem,
                1,
                query.Sayfa,
                query.SayfaBoyutu,
                new[] { _model }));

        public Task<BayiDonemPerformansReadModel?> GetAsync(
            string bayiKodu,
            DateOnly? donem,
            CancellationToken cancellationToken) =>
            Task.FromResult<BayiDonemPerformansReadModel?>(
                bayiKodu == _model.BayiKodu ? _model : null);

        public Task<PerformansDashboardStatsReadModel> GetDashboardStatsAsync(
            DateOnly? donem,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PerformansDashboardStatsReadModel(
                _model.Donem,
                1,
                _model.Ciro ?? 0m,
                _model.Hedef ?? 0m,
                0,
                1,
                _model.HedefTutmaOrani,
                _model.OrtalamaKarMarji,
                _model.RiskSkoru));

        public Task<IReadOnlyList<BayiDonemPerformansReadModel>> GetHistoryAsync(
            string bayiKodu,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BayiDonemPerformansReadModel>>(
                bayiKodu == _model.BayiKodu && limit > 0
                    ? new[] { _model }
                    : Array.Empty<BayiDonemPerformansReadModel>());
    }

    private readonly BusinessRulesOptions _options = new()
    {
        MarjEsigiPct = 0.05m,
        MaxIskontoPct = 0.15m,
        HedefRiskEsigiPct = 0.80m,
        StokGunEsigi = 7m,
        MinMarjRedPct = 0.00m,
        IadeRiskEsigiPct = 0.10m,
        RiskLevels = new Dictionary<string, int>
        {
            ["DUSUK_max"] = 30,
            ["ORTA_max"] = 60,
            ["YUKSEK_max"] = 900
        },
        RiskWeights = new Dictionary<string, int>
        {
            ["MARJ_ESIK_ALTI"] = 40,
            ["HEDEF_ALTINDA"] = 30,
            ["YUKSEK_ISKONTO"] = 20,
            ["STOK_YETERSIZ"] = 20
        }
    };

    private TeklifYonetimService CreateService(
        ITeklifWriteRepository? repo = null,
        IBayiDonemPerformansReadRepository? performansRepo = null)
    {
        var options = _options;
        var decisionEngine = new OfferDecisionEngine(options);
        return performansRepo is null
            ? new TeklifYonetimService(
                repo ?? new TeklifWriteRepository(),
                new FakeBayiRepo(),
                new FakeUrunRepo(),
                new FakeDepoRepo(),
                new FakeStokRepo(),
                decisionEngine,
                options,
                TimeProvider.System)
            : new TeklifYonetimService(
                repo ?? new TeklifWriteRepository(),
                new FakeBayiRepo(),
                new FakeUrunRepo(),
                new FakeDepoRepo(),
                new FakeStokRepo(),
                decisionEngine,
                options,
                performansRepo,
                TimeProvider.System);
    }

    [Fact]
    public async Task TeklifOlusturAsync_GecerliKomut_TeklifKaydederVeHesaplamaYapmaz()
    {
        var service = CreateService();

        var command = new YeniTeklifCommand(
            BayiKodu: "B001",
            Aciklama: "Test Teklifi",
            Kalemler: new List<TeklifKalemCommand>
            {
                new("U001", "DP01", 5, 12000m, 0.05m),
                new("U002", "DP01", 10, 300m, 0.02m)
            });

        var result = await service.TeklifOlusturAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.StartsWith("TKLF-", result.Value.TeklifId);
        Assert.Equal(TeklifDurumlari.Taslak, result.Value.Durum);
        Assert.Equal(2, result.Value.Kalemler.Count);
        Assert.Null(result.Value.DegerlendirmeSonucu);
        Assert.Equal("Istanbul", result.Value.BayiSehir);
        Assert.Equal("Marmara", result.Value.BayiBolge);
    }

    [Fact]
    public async Task TeklifOlusturAsync_GecersizBayi_NotFoundDoner()
    {
        var service = CreateService();

        var command = new YeniTeklifCommand(
            BayiKodu: "B999",
            Aciklama: "Test",
            Kalemler: new List<TeklifKalemCommand>
            {
                new("U001", "DP01", 1, 1000m, 0m)
            });

        var result = await service.TeklifOlusturAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("BAYI_BULUNAMADI", result.ErrorCode);
    }

    [Fact]
    public async Task TeklifDegerlendirByIdAsync_GecerliTeklif_HerKalemiVeGenelKarariUretir()
    {
        var repo = new TeklifWriteRepository();
        var service = CreateService(repo);

        var command = new YeniTeklifCommand(
            BayiKodu: "B001",
            Aciklama: "Kapsamlı Teklif",
            Kalemler: new List<TeklifKalemCommand>
            {
                new("U001", "DP01", 2, 12000m, 0.05m),
                new("U002", "DP01", 5, 300m, 0.05m)
            });

        var createResult = await service.TeklifOlusturAsync(command, CancellationToken.None);
        string teklifId = createResult.Value!.TeklifId;

        var evalResult = await service.TeklifDegerlendirByIdAsync(teklifId, CancellationToken.None);

        Assert.True(evalResult.IsSuccess);
        Assert.NotNull(evalResult.Value);
        Assert.Equal("APPROVE", evalResult.Value.GenelKarar);
        Assert.Equal(2, evalResult.Value.KalemSonuclari.Count);
        Assert.True(evalResult.Value.ToplamNetTutar > 0);

        var saved = await service.TeklifGetirAsync(teklifId, CancellationToken.None);
        Assert.Equal(TeklifDurumlari.Degerlendirildi, saved.Value!.Durum);
        Assert.NotNull(saved.Value.DegerlendirmeSonucu);
    }

    [Fact]
    public async Task TeklifDegerlendirByIdAsync_OlmayanTeklif_NotFoundDoner()
    {
        var service = CreateService();

        var result = await service.TeklifDegerlendirByIdAsync("TKLF-OLMAYAN", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("TEKLIF_BULUNAMADI", result.ErrorCode);
    }

    [Fact]
    public async Task TeklifAnlikDegerlendirAsync_GecerliKomut_AnlikSonucDoner()
    {
        var service = CreateService();

        var command = new YeniTeklifCommand(
            BayiKodu: "B001",
            Aciklama: "Anlık",
            Kalemler: new List<TeklifKalemCommand>
            {
                new("U001", "DP01", 10, 15000m, 0.05m)
            });

        var result = await service.TeklifAnlikDegerlendirAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("APPROVE", result.Value.GenelKarar);
    }

    [Fact]
    public async Task TeklifAnlikDegerlendirAsync_AylikPerformansMetrikleriniYenidenHesaplamadanAktarir()
    {
        var service = CreateService(performansRepo: new FakePerformansRepo());
        var command = new YeniTeklifCommand(
            BayiKodu: "B001",
            Aciklama: "Aylık karne testi",
            Kalemler: new[]
            {
                new TeklifKalemCommand("U001", "DP01", 1, 15000m, 0.05m)
            });

        var result = await service.TeklifAnlikDegerlendirAsync(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(25, result.Value.RiskSkoru);
        Assert.Equal("DUSUK", result.Value.RiskSeviyesi);
        Assert.Equal(92.5m, result.Value.HedefTutturmaYuzdesi);
        Assert.Equal(0.5m, result.Value.OrtalamaIskontoOrani);
        Assert.Equal(1.25m, result.Value.IadeOrani);
        Assert.Contains("PERFORMANS_RISKI", result.Value.GenelGerekceKodlari);
    }

    [Fact]
    public async Task TeklifleriListeleAsync_SehirVeBolgeFiltresiniRepositoryeIletir()
    {
        var service = CreateService();
        var createResult = await service.TeklifOlusturAsync(
            new YeniTeklifCommand(
                "B001",
                "Filtre testi",
                new[] { new TeklifKalemCommand("U001", "DP01", 1, 12000m, 0.05m) }),
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);

        var result = await service.TeklifleriListeleAsync(
            new TeklifListeFiltre(
                BayiKodu: "B001",
                Sehir: "Istanbul",
                Bolge: "Marmara",
                Durum: TeklifDurumlari.Taslak),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Contains(result.Value, item => item.TeklifId == createResult.Value!.TeklifId);
        Assert.All(result.Value, item => Assert.Equal("Marmara", item.BayiBolge));
    }
}
