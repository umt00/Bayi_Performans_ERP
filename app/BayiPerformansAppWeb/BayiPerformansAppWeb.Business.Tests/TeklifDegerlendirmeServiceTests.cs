using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class TeklifDegerlendirmeServiceTests
{
    // ==================================================
    // TEST CONFIGURATION
    // ==================================================

    private static BusinessRulesOptions CreateOptions()
    {
        return new BusinessRulesOptions
        {
            MarjEsigiPct = 0.15m,
            MaxIskontoPct = 0.15m,
            HedefRiskEsigiPct = 0.80m,
            StokGunEsigi = 7m,
            MinMarjRedPct = 0m,
            IadeRiskEsigiPct = 0.10m,

            RiskWeights = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["MARJ_ESIK_ALTI"] = 40,
                ["YUKSEK_ISKONTO"] = 20,
                ["STOK_YETERSIZ"] = 30
            },

            RiskLevels = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["DUSUK_max"] = 30,
                ["ORTA_max"] = 60,
                ["YUKSEK_max"] = 900
            }
        };
    }

    private static BayiReadModel CreateBayi(
        string bayiKodu = "B001")
    {
        return new BayiReadModel(
            BayiKodu: bayiKodu,
            Segment: "A",
            Bolge: "MARMARA");
    }

    private static UrunReadModel CreateUrun(
        string urunKodu = "U001",
        decimal? birimMaliyet = 60m)
    {
        return new UrunReadModel(
            UrunKodu: urunKodu,
            Kategori: "ELEKTRONIK",
            AltKategori: "AKSESUAR",
            BirimMaliyet: birimMaliyet);
    }

    private static DepoReadModel CreateDepo(
        string depoKodu = "D001")
    {
        return new DepoReadModel(
            DepoKodu: depoKodu,
            Bolge: "MARMARA");
    }

    private static StokReadModel CreateStok(
        string urunKodu = "U001",
        string depoKodu = "D001",
        decimal? mevcutStok = 100m)
    {
        return new StokReadModel(
            UrunKodu: urunKodu,
            DepoKodu: depoKodu,
            MevcutStok: mevcutStok,
            MinStokSeviyesi: 10m,
            SonSayimTarihi: new DateOnly(2026, 1, 1));
    }

    private static TeklifDegerlendirmeCommand CreateCommand(
        string bayiKodu = "B001",
        string urunKodu = "U001",
        string depoKodu = "D001",
        decimal miktar = 10m,
        decimal birimFiyat = 100m,
        decimal iskontoOrani = 0.10m)
    {
        return new TeklifDegerlendirmeCommand(
            BayiKodu: bayiKodu,
            UrunKodu: urunKodu,
            DepoKodu: depoKodu,
            Miktar: miktar,
            BirimFiyat: birimFiyat,
            IskontoOrani: iskontoOrani);
    }

    private static (
        TeklifDegerlendirmeService Service,
        FakeBayiReadRepository BayiRepository,
        FakeUrunReadRepository UrunRepository,
        FakeDepoReadRepository DepoRepository,
        FakeStokReadRepository StokRepository)
        CreateService()
    {
        BusinessRulesOptions options = CreateOptions();

        FakeBayiReadRepository bayiRepository = new()
        {
            Bayi = CreateBayi()
        };

        FakeUrunReadRepository urunRepository = new()
        {
            Urun = CreateUrun()
        };

        FakeDepoReadRepository depoRepository = new()
        {
            Depo = CreateDepo()
        };

        FakeStokReadRepository stokRepository = new()
        {
            Stok = CreateStok()
        };

        OfferDecisionEngine engine = new(options);

        TeklifDegerlendirmeService service =
            new(
                bayiRepository,
                urunRepository,
                depoRepository,
                stokRepository,
                engine,
                options);

        return (
            service,
            bayiRepository,
            urunRepository,
            depoRepository,
            stokRepository);
    }

    // ==================================================
    // 1. NULL COMMAND
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_CommandNull_IseArgumentNullExceptionDonmeli()
    {
        var test = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => test.Service.DegerlendirAsync(
                null!,
                CancellationToken.None));
    }

    // ==================================================
    // 2. BAYI KODU
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_BayiKoduBos_IseValidationDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(bayiKodu: ""),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Equal("BAYI_KODU_ZORUNLU", result.ErrorCode);
    }

    // ==================================================
    // 3. URUN KODU
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_UrunKoduBos_IseValidationDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(urunKodu: ""),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Equal("URUN_KODU_ZORUNLU", result.ErrorCode);
    }

    // ==================================================
    // 4. DEPO KODU
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_DepoKoduBos_IseValidationDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(depoKodu: ""),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Equal("DEPO_KODU_ZORUNLU", result.ErrorCode);
    }

    // ==================================================
    // 5. BAYI BULUNAMADI
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_BayiBulunamazsa_NotFoundDonmeli()
    {
        var test = CreateService();

        test.BayiRepository.Bayi = null;

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        Assert.Equal("BAYI_BULUNAMADI", result.ErrorCode);

        Assert.Equal(0, test.UrunRepository.CallCount);
        Assert.Equal(0, test.DepoRepository.CallCount);
        Assert.Equal(0, test.StokRepository.CallCount);
    }

    // ==================================================
    // 6. URUN BULUNAMADI
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_UrunBulunamazsa_NotFoundDonmeli()
    {
        var test = CreateService();

        test.UrunRepository.Urun = null;

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        Assert.Equal("URUN_BULUNAMADI", result.ErrorCode);

        Assert.Equal(0, test.DepoRepository.CallCount);
        Assert.Equal(0, test.StokRepository.CallCount);
    }

    // ==================================================
    // 7. DEPO BULUNAMADI
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_DepoBulunamazsa_NotFoundDonmeli()
    {
        var test = CreateService();

        test.DepoRepository.Depo = null;

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        Assert.Equal("DEPO_BULUNAMADI", result.ErrorCode);

        Assert.Equal(0, test.StokRepository.CallCount);
    }

    // ==================================================
    // 8. STOK KAYDI YOK
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_StokKaydiYoksa_NotFoundDonmeli()
    {
        var test = CreateService();

        test.StokRepository.Stok = null;

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        Assert.Equal("STOK_BILGISI_BULUNAMADI", result.ErrorCode);
    }

    // ==================================================
    // 9. STOK NULL
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_MevcutStokNull_IseUnavailableDonmeli()
    {
        var test = CreateService();

        test.StokRepository.Stok =
            CreateStok(mevcutStok: null);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("STOK_MIKTARI_YOK", result.ErrorCode);
    }

    // ==================================================
    // 10. MALIYET NULL
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_BirimMaliyetNull_IseUnavailableDonmeli()
    {
        var test = CreateService();

        test.UrunRepository.Urun =
            CreateUrun(birimMaliyet: null);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("MALIYET_BILGISI_YOK", result.ErrorCode);
    }

    // ==================================================
    // 11. NORMAL APPROVE
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_NormalTeklif_IseApproveDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("APPROVE", output.Karar);
        Assert.Equal(100m, output.MevcutStok);
        Assert.Equal(0.15m, output.MarjEsigiPct);

        Assert.Equal(
            0.3333333333333333333333333333m,
            output.HesaplananMarjPct);

        Assert.Empty(output.GerekceKodlari);
        Assert.Null(output.AiComment);
        Assert.Null(output.CommentSource);
    }

    // ==================================================
    // 12. HIGH DISCOUNT
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_YuksekIskontoTekBasina_IseApproveDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(iskontoOrani: 0.20m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("APPROVE", output.Karar);

        Assert.Contains(
            "YUKSEK_ISKONTO",
            output.GerekceKodlari);

        Assert.Equal(
            20,
            GetRiskScore(output.GerekceKodlari));

        Assert.Equal(
            "DUSUK",
            GetRiskLevel(output.GerekceKodlari));
    }

    // ==================================================
    // 13. MEDIUM RISK
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_OrtaRisk_IseNeedApprovalDonmeli()
    {
        var test = CreateService();

        test.UrunRepository.Urun =
            CreateUrun(birimMaliyet: 90m);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(
                    miktar: 10m,
                    birimFiyat: 100m,
                    iskontoOrani: 0m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("NEED_APPROVAL", output.Karar);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            output.GerekceKodlari);

        Assert.Equal(
            40,
            GetRiskScore(output.GerekceKodlari));

        Assert.Equal(
            "ORTA",
            GetRiskLevel(output.GerekceKodlari));

        Assert.Equal(
            0.10m,
            output.HesaplananMarjPct);
    }

    // ==================================================
    // 14. HIGH RISK
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_YuksekRisk_IseNeedApprovalDonmeli()
    {
        var test = CreateService();

        test.UrunRepository.Urun =
            CreateUrun(birimMaliyet: 75m);

        test.StokRepository.Stok =
            CreateStok(mevcutStok: 5m);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(
                    miktar: 10m,
                    birimFiyat: 100m,
                    iskontoOrani: 0.20m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("NEED_APPROVAL", output.Karar);

        Assert.Equal(
            90,
            GetRiskScore(output.GerekceKodlari));

        Assert.Equal(
            "YUKSEK",
            GetRiskLevel(output.GerekceKodlari));

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            output.GerekceKodlari);

        Assert.Contains(
            "YUKSEK_ISKONTO",
            output.GerekceKodlari);

        Assert.Contains(
            "STOK_YETERSIZ",
            output.GerekceKodlari);
    }

    // ==================================================
    // 15. NEGATIVE MARGIN
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_NegatifMarj_IseRejectDonmeli()
    {
        var test = CreateService();

        test.UrunRepository.Urun =
            CreateUrun(birimMaliyet: 120m);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(
                    birimFiyat: 100m,
                    iskontoOrani: 0m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("REJECT", output.Karar);

        Assert.True(
            output.HesaplananMarjPct < 0);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            output.GerekceKodlari);
    }

    // ==================================================
    // 16. STOCK ZERO
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_MevcutStokSifir_IseStokYetersizGerekcesiDonmeli()
    {
        var test = CreateService();

        test.StokRepository.Stok =
            CreateStok(mevcutStok: 0m);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(miktar: 10m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal(0m, output.MevcutStok);

        Assert.Contains(
            "STOK_YETERSIZ",
            output.GerekceKodlari);

        Assert.Equal("APPROVE", output.Karar);
    }

    // ==================================================
    // 17. STOCK EXACTLY EQUAL
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_StokMiktaraEsitse_StokYetersizOlmamali()
    {
        var test = CreateService();

        test.StokRepository.Stok =
            CreateStok(mevcutStok: 10m);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(miktar: 10m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal(10m, output.MevcutStok);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            output.GerekceKodlari);

        Assert.Empty(output.GerekceKodlari);
        Assert.Equal("APPROVE", output.Karar);
    }

    // ==================================================
    // 18. OUTPUT
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_BasariliSonuc_OutputAlanlariniDogruAktarmali()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("APPROVE", output.Karar);
        Assert.Empty(output.GerekceKodlari);

        Assert.Equal(
            0.3333333333333333333333333333m,
            output.HesaplananMarjPct);

        Assert.Equal(0.15m, output.MarjEsigiPct);
        Assert.Equal(100m, output.MevcutStok);

        Assert.Null(output.AiComment);
        Assert.Null(output.CommentSource);
    }

    // ==================================================
    // 19. INVALID QUANTITY
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_GecersizMiktar_IseRejectDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(miktar: 0m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("REJECT", output.Karar);

        Assert.Contains(
            "GECERSIZ_MIKTAR",
            output.GerekceKodlari);

        Assert.Null(output.HesaplananMarjPct);
        Assert.Equal(100m, output.MevcutStok);
    }

    // ==================================================
    // 20. INVALID PRICE
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_NegatifBirimFiyat_IseRejectDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(birimFiyat: -10m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("REJECT", output.Karar);

        Assert.Contains(
            "GECERSIZ_BIRIM_FIYAT",
            output.GerekceKodlari);

        Assert.Null(output.HesaplananMarjPct);
    }

    // ==================================================
    // 21. INVALID DISCOUNT
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_GecersizIskonto_IseRejectDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(iskontoOrani: 1.10m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("REJECT", output.Karar);

        Assert.Contains(
            "GECERSIZ_ISKONTO",
            output.GerekceKodlari);

        Assert.Null(output.HesaplananMarjPct);
    }

    // ==================================================
    // 22. ZERO NET PRICE
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_YuzdeYuzIskonto_IseRejectDonmeli()
    {
        var test = CreateService();

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(
                    birimFiyat: 100m,
                    iskontoOrani: 1m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal("REJECT", output.Karar);

        Assert.Contains(
            "GECERSIZ_NET_FIYAT",
            output.GerekceKodlari);

        Assert.Null(output.HesaplananMarjPct);

        // TeklifDegerlendirmeOutput içinde RiskSkoru yok.
        // Bu nedenle output.RiskSkoru() kontrolü kaldırıldı.
        //
        // OfferDecisionEngine bu senaryoda:
        // RiskSkoru = 0
        // RiskSeviyesi = YUKSEK
        // üretir; ancak bunlar service output'una taşınmıyor.
    }

    // ==================================================
    // 23. SHORT CIRCUIT
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_UrunBulunamazsa_RepositoryZinciriniDurdurmali()
    {
        var test = CreateService();

        test.UrunRepository.Urun = null;

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(),
                CancellationToken.None);

        Assert.False(result.IsSuccess);

        Assert.Equal(
            "URUN_BULUNAMADI",
            result.ErrorCode);

        Assert.Equal(
            1,
            test.BayiRepository.CallCount);

        Assert.Equal(
            1,
            test.UrunRepository.CallCount);

        Assert.Equal(
            0,
            test.DepoRepository.CallCount);

        Assert.Equal(
            0,
            test.StokRepository.CallCount);
    }

    // ==================================================
    // 24. LOW MARGIN + STOCK SHORTAGE
    // ==================================================

    [Fact]
    public async Task DegerlendirAsync_DusukMarjVeYetersizStok_IseGerekcelerBirlikteDonmeli()
    {
        var test = CreateService();

        test.UrunRepository.Urun =
            CreateUrun(birimMaliyet: 90m);

        test.StokRepository.Stok =
            CreateStok(mevcutStok: 5m);

        var result =
            await test.Service.DegerlendirAsync(
                CreateCommand(
                    miktar: 10m,
                    birimFiyat: 100m,
                    iskontoOrani: 0m),
                CancellationToken.None);

        Assert.True(result.IsSuccess);

        var output = result.Value!;

        Assert.Equal(
            "NEED_APPROVAL",
            output.Karar);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            output.GerekceKodlari);

        Assert.Contains(
            "STOK_YETERSIZ",
            output.GerekceKodlari);

        Assert.Equal(
            70,
            GetRiskScore(output.GerekceKodlari));

        Assert.Equal(
            "YUKSEK",
            GetRiskLevel(output.GerekceKodlari));
    }

    // ==================================================
    // TEST HELPERS
    // ==================================================

    private static int GetRiskScore(
        IReadOnlyList<string> gerekceKodlari)
    {
        var weights =
            new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                ["MARJ_ESIK_ALTI"] = 40,
                ["YUKSEK_ISKONTO"] = 20,
                ["STOK_YETERSIZ"] = 30
            };

        return gerekceKodlari
            .Where(weights.ContainsKey)
            .Sum(code => weights[code]);
    }

    private static string GetRiskLevel(
        IReadOnlyList<string> gerekceKodlari)
    {
        int score =
            GetRiskScore(gerekceKodlari);

        return score <= 30
            ? "DUSUK"
            : score <= 60
                ? "ORTA"
                : "YUKSEK";
    }
}


// ======================================================
// FAKE REPOSITORIES
// ======================================================

internal sealed class FakeBayiReadRepository
    : IBayiReadRepository
{
    public BayiReadModel? Bayi { get; set; }

    public int CallCount { get; private set; }

    public Task<BayiReadModel?> GetByCodeAsync(
        string bayiKodu,
        CancellationToken cancellationToken)
    {
        CallCount++;

        return Task.FromResult(Bayi);
    }

    public Task<IReadOnlyList<BayiReadModel>> ListAsync(
        string? segment,
        string? bolge,
        CancellationToken cancellationToken)
    {
        if (Bayi is null)
        {
            return Task.FromResult<
                IReadOnlyList<BayiReadModel>>(
                Array.Empty<BayiReadModel>());
        }

        return Task.FromResult<
            IReadOnlyList<BayiReadModel>>(
            new[] { Bayi });
    }
}


internal sealed class FakeUrunReadRepository
    : IUrunReadRepository
{
    public UrunReadModel? Urun { get; set; }

    public int CallCount { get; private set; }

    public Task<UrunReadModel?> GetByCodeAsync(
        string urunKodu,
        CancellationToken cancellationToken)
    {
        CallCount++;

        return Task.FromResult(Urun);
    }

    public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(
        IReadOnlyCollection<string> urunKodlari,
        CancellationToken cancellationToken)
    {
        if (Urun is null)
        {
            return Task.FromResult<
                IReadOnlyList<UrunReadModel>>(
                Array.Empty<UrunReadModel>());
        }

        return Task.FromResult<
            IReadOnlyList<UrunReadModel>>(
            new[] { Urun });
    }

    public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(
        CancellationToken cancellationToken)
    {
        if (Urun is null)
        {
            return Task.FromResult<
                IReadOnlyList<UrunReadModel>>(
                Array.Empty<UrunReadModel>());
        }

        return Task.FromResult<
            IReadOnlyList<UrunReadModel>>(
            new[] { Urun });
    }
}


internal sealed class FakeDepoReadRepository
    : IDepoReadRepository
{
    public DepoReadModel? Depo { get; set; }

    public int CallCount { get; private set; }

    public Task<DepoReadModel?> GetByCodeAsync(
        string depoKodu,
        CancellationToken cancellationToken)
    {
        CallCount++;

        return Task.FromResult(Depo);
    }

    public Task<IReadOnlyList<DepoReadModel>> ListAsync(
        CancellationToken cancellationToken)
    {
        if (Depo is null)
        {
            return Task.FromResult<
                IReadOnlyList<DepoReadModel>>(
                Array.Empty<DepoReadModel>());
        }

        return Task.FromResult<
            IReadOnlyList<DepoReadModel>>(
            new[] { Depo });
    }
}


internal sealed class FakeStokReadRepository
    : IStokReadRepository
{
    public StokReadModel? Stok { get; set; }

    public int CallCount { get; private set; }

    public Task<StokReadModel?> GetAsync(
        string urunKodu,
        string depoKodu,
        CancellationToken cancellationToken)
    {
        CallCount++;

        return Task.FromResult(Stok);
    }

    public Task<IReadOnlyList<StokReadModel>> ListAsync(
        string? depoKodu,
        string? urunKodu,
        CancellationToken cancellationToken)
    {
        if (Stok is null)
        {
            return Task.FromResult<
                IReadOnlyList<StokReadModel>>(
                Array.Empty<StokReadModel>());
        }

        return Task.FromResult<
            IReadOnlyList<StokReadModel>>(
            new[] { Stok });
    }
}