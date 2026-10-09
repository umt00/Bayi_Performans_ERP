using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests.Calculations;

/// <summary>
/// OfferDecisionEngine için deterministik business rule testleri.
/// </summary>
public sealed class OfferDecisionEngineTests
{
    private static BusinessRulesOptions CreateOptions()
    {
        return new BusinessRulesOptions
        {
            MarjEsigiPct = 0.15m,
            MaxIskontoPct = 0.15m,
            HedefRiskEsigiPct = 0.80m,
            StokGunEsigi = 7m,
            MinMarjRedPct = 0.0m,
            IadeRiskEsigiPct = 0.10m,

            RiskWeights = new Dictionary<string, int>
            {
                ["MARJ_ESIK_ALTI"] = 40,
                ["HEDEF_ALTINDA"] = 30,
                ["YUKSEK_ISKONTO"] = 20,
                ["STOK_YETERSIZ"] = 20,
                ["YUKSEK_IADE"] = 20
            },

            RiskLevels = new Dictionary<string, int>
            {
                ["DUSUK_max"] = 30,
                ["ORTA_max"] = 60,
                ["YUKSEK_max"] = 900
            }
        };
    }

    private static OfferDecisionEngine CreateEngine()
    {
        return new OfferDecisionEngine(
            CreateOptions());
    }

    // ==================================================
    // APPROVE
    // ==================================================

    [Fact]
    public void Evaluate_ValidOffer_ReturnsApprove()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 600,
            mevcutStok: 20);

        Assert.Equal("APPROVE", result.Karar);
        Assert.Equal("DUSUK", result.RiskSeviyesi);
        Assert.Equal(0, result.RiskSkoru);
        Assert.Empty(result.GerekceKodlari);
    }

    // ==================================================
    // MARJ HESABI
    // ==================================================

    [Fact]
    public void Evaluate_CalculatesMarginCorrectly()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 700,
            mevcutStok: 20);

        Assert.NotNull(result.HesaplananMarjPct);

        Assert.Equal(
            22.22m,
            decimal.Round(
                result.HesaplananMarjPct!.Value * 100m,
                2));
    }

    // ==================================================
    // MARJ EŞİĞİ
    // ==================================================

    [Fact]
    public void Evaluate_MarginBelowThreshold_ReturnsNeedApproval()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0,
            birimMaliyet: 900,
            mevcutStok: 20);

        Assert.Equal(
            "NEED_APPROVAL",
            result.Karar);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);

        Assert.Equal(
            40,
            result.RiskSkoru);

        Assert.Equal(
            "ORTA",
            result.RiskSeviyesi);
    }

    // ==================================================
    // NEGATİF MARJ
    // ==================================================

    [Fact]
    public void Evaluate_NegativeMargin_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0,
            birimMaliyet: 1100,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);

        Assert.NotNull(
            result.HesaplananMarjPct);

        Assert.True(
            result.HesaplananMarjPct!.Value < 0);
    }

    // ==================================================
    // SIFIR MARJ
    // ==================================================

    [Fact]
    public void Evaluate_ZeroMargin_ReturnsNeedApproval()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0,
            birimMaliyet: 1000,
            mevcutStok: 20);

        Assert.Equal(
            "NEED_APPROVAL",
            result.Karar);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);

        Assert.Equal(
            40,
            result.RiskSkoru);

        Assert.Equal(
            "ORTA",
            result.RiskSeviyesi);
    }

    // ==================================================
    // STOK YETERSİZ
    // ==================================================

    [Fact]
    public void Evaluate_InsufficientStock_ReturnsApprove()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 600,
            mevcutStok: 5);

        Assert.Equal(
            "APPROVE",
            result.Karar);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);

        Assert.Equal(
            20,
            result.RiskSkoru);

        Assert.Equal(
            "DUSUK",
            result.RiskSeviyesi);
    }

    // ==================================================
    // MARJ + STOK
    // ==================================================

    [Fact]
    public void Evaluate_MarginAndStockProblems_ReturnsNeedApproval()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0,
            birimMaliyet: 900,
            mevcutStok: 5);

        Assert.Equal(
            "NEED_APPROVAL",
            result.Karar);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);

        Assert.Equal(
            60,
            result.RiskSkoru);

        Assert.Equal(
            "ORTA",
            result.RiskSeviyesi);
    }

    // ==================================================
    // YÜKSEK İSKONTO
    // ==================================================

    [Fact]
    public void Evaluate_HighDiscount_AddsReason()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.20m,
            birimMaliyet: 600,
            mevcutStok: 20);

        Assert.Contains(
            "YUKSEK_ISKONTO",
            result.GerekceKodlari);

        Assert.Equal(
            20,
            result.RiskSkoru);

        Assert.Equal(
            "DUSUK",
            result.RiskSeviyesi);

        Assert.Equal(
            "APPROVE",
            result.Karar);
    }

    // ==================================================
    // YÜKSEK RİSK
    // ==================================================

    [Fact]
    public void Evaluate_HighRisk_ReturnsNeedApproval()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.20m,
            birimMaliyet: 700,
            mevcutStok: 5);

        // Net fiyat:
        // 1000 * 0.80 = 800
        //
        // Marj:
        // (800 - 700) / 800 = 0.125
        //
        // %12.5 < %15
        // => MARJ_ESIK_ALTI = 40
        //
        // %20 > %15
        // => YUKSEK_ISKONTO = 20
        //
        // 5 < 10
        // => STOK_YETERSIZ = 20
        //
        // Toplam = 80

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);

        Assert.Contains(
            "YUKSEK_ISKONTO",
            result.GerekceKodlari);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);

        Assert.Equal(
            80,
            result.RiskSkoru);

        Assert.Equal(
            "YUKSEK",
            result.RiskSeviyesi);

        Assert.Equal(
            "NEED_APPROVAL",
            result.Karar);
    }

    // ==================================================
    // İSKONTO EŞİĞİNE EŞİT
    // ==================================================

    [Fact]
    public void Evaluate_DiscountExactlyAtThreshold_DoesNotAddReason()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.15m,
            birimMaliyet: 600,
            mevcutStok: 20);

        Assert.DoesNotContain(
            "YUKSEK_ISKONTO",
            result.GerekceKodlari);

        Assert.Equal(
            "APPROVE",
            result.Karar);
    }

    // ==================================================
    // STOK TAM EŞİT
    // ==================================================

    [Fact]
    public void Evaluate_StockExactlyEqualsQuantity_DoesNotAddReason()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 600,
            mevcutStok: 10);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            result.GerekceKodlari);

        Assert.Equal(
            "APPROVE",
            result.Karar);
    }

    // ==================================================
    // NULL STOK
    // ==================================================

    [Fact]
    public void Evaluate_MissingStock_DoesNotAddStockReason()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 600,
            mevcutStok: null);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            result.GerekceKodlari);

        Assert.Equal(
            "APPROVE",
            result.Karar);

        Assert.Null(
            result.MevcutStok);
    }

    // ==================================================
    // GEÇERSİZ MİKTAR
    // ==================================================

    [Fact]
    public void Evaluate_ZeroQuantity_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 0,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 600,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "GECERSIZ_MIKTAR",
            result.GerekceKodlari);

        Assert.Null(
            result.HesaplananMarjPct);
    }

    // ==================================================
    // NEGATİF FİYAT
    // ==================================================

    [Fact]
    public void Evaluate_NegativeUnitPrice_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: -100,
            iskontoOrani: 0.10m,
            birimMaliyet: 600,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "GECERSIZ_BIRIM_FIYAT",
            result.GerekceKodlari);
    }

    // ==================================================
    // GEÇERSİZ İSKONTO
    // ==================================================

    [Fact]
    public void Evaluate_InvalidDiscount_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 1.20m,
            birimMaliyet: 600,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "GECERSIZ_ISKONTO",
            result.GerekceKodlari);
    }

    // ==================================================
    // NULL MALİYET
    // ==================================================

    [Fact]
    public void Evaluate_MissingCost_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: null,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "MALIYET_BILGISI_YOK",
            result.GerekceKodlari);

        Assert.Null(
            result.HesaplananMarjPct);
    }

    // ==================================================
    // NEGATİF MALİYET
    // ==================================================

    [Fact]
    public void Evaluate_NegativeCost_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: -600,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "GECERSIZ_MALIYET",
            result.GerekceKodlari);
    }

    // ==================================================
    // SIFIR NET FİYAT
    // ==================================================

    [Fact]
    public void Evaluate_ZeroNetPrice_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 0,
            iskontoOrani: 0,
            birimMaliyet: 0,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "GECERSIZ_NET_FIYAT",
            result.GerekceKodlari);

        Assert.Null(
            result.HesaplananMarjPct);
    }

    // ==================================================
    // %100 İSKONTO
    // ==================================================

    [Fact]
    public void Evaluate_DiscountExactlyOne_ReturnsReject()
    {
        OfferDecisionEngine engine = CreateEngine();

        OfferDecisionResult result = engine.Evaluate(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 1.00m,
            birimMaliyet: 100,
            mevcutStok: 20);

        Assert.Equal(
            "REJECT",
            result.Karar);

        Assert.Contains(
            "GECERSIZ_NET_FIYAT",
            result.GerekceKodlari);
    }

    // ==================================================
    // NULL OPTIONS
    // ==================================================

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new OfferDecisionEngine(null!));
    }
}