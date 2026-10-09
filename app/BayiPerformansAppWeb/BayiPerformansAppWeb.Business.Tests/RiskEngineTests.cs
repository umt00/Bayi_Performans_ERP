using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;

using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;

using Xunit;

namespace BayiPerformansAppWeb.Business.Tests.Calculations;

/// <summary>
/// RiskEngine sınıfının deterministik risk hesaplama kurallarını test eder.
///
/// Testler:
/// - Marj riskini
/// - Hedef gerçekleşme riskini
/// - İskonto riskini
/// - Stok riskini
/// - İade riskini
/// - Risk puanlarının toplanmasını
/// - Risk seviyesinin belirlenmesini
/// - Gerekçe kodlarının oluşturulmasını
///
/// kontrol eder.
///
/// Testler gerçek veritabanına veya Supabase'e bağlanmaz.
/// Bütün test verileri doğrudan test içerisinde oluşturulur.
/// </summary>
public sealed class RiskEngineTests
{
    /// <summary>
    /// Testlerde kullanılacak BusinessRulesOptions nesnesini oluşturur.
    ///
    /// Bu değerler thresholds.json içerisindeki mevcut eşiklerle aynıdır:
    ///
    /// Marj eşiği       : 0.15
    /// Maksimum iskonto : 0.15
    /// Hedef eşiği      : 0.80
    /// İade eşiği       : 0.10
    ///
    /// Risk ağırlıkları:
    /// MARJ_ESIK_ALTI : 40
    /// HEDEF_ALTINDA  : 30
    /// YUKSEK_ISKONTO : 20
    /// STOK_YETERSIZ  : 20
    /// YUKSEK_IADE    : 20
    ///
    /// Risk seviyeleri:
    /// DUSUK : 30
    /// ORTA  : 60
    /// YUKSEK: 900
    /// </summary>
    private static BusinessRulesOptions CreateOptions()
    {
        return new BusinessRulesOptions
        {
            MarjEsigiPct = 0.15m,
            MaxIskontoPct = 0.15m,
            HedefRiskEsigiPct = 0.80m,
            StokGunEsigi = 7,
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

    /// <summary>
    /// Test için yeni bir RiskEngine oluşturur.
    /// </summary>
    private static RiskEngine CreateEngine()
    {
        return new RiskEngine(CreateOptions());
    }

    /// <summary>
    /// Testlerde kullanılacak risksiz örnek satış metriklerini oluşturur.
    ///
    /// Marj:
    /// 0.20 -> %20
    ///
    /// Hedef:
    /// 0.90 -> %90
    ///
    /// İskonto:
    /// 0.10 -> %10
    ///
    /// İade:
    /// 0.05 -> %5
    ///
    /// Bu değerlerin hiçbiri risk eşiğini aşmadığı için
    /// herhangi bir risk gerekçesi oluşmaması beklenir.
    /// </summary>
    private static SalesMetricsResult CreateSafeMetrics()
    {
        return new SalesMetricsResult(
            GerceklesenCiro: 90000m,
            HedefCiro: 100000m,
            HedefeUlasmaPct: 0.90m,
            OrtalamaMarjPct: 0.20m,
            OrtalamaIskontoPct: 0.10m,
            IadeOraniPct: 0.05m);
    }

    /// <summary>
    /// Hiçbir risk kuralı tetiklenmediğinde:
    ///
    /// Risk skoru = 0
    /// Risk seviyesi = DUSUK
    ///
    /// olması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Calculate_NoRisk_ReturnsZeroScoreAndLowRisk()
    {
        RiskEngine engine = CreateEngine();

        RiskEngineResult result = engine.Calculate(
            CreateSafeMetrics(),
            kritikStokUrunSayisi: 0);

        Assert.Equal(0, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);
        Assert.Empty(result.GerekceKodlari);
    }

    /// <summary>
    /// Ortalama marj threshold değerinin altında olduğunda
    /// MARJ_ESIK_ALTI gerekçesinin oluşturulduğunu test eder.
    ///
    /// Marj:
    /// 0.10
    ///
    /// Threshold:
    /// 0.15
    ///
    /// Risk ağırlığı:
    /// 40
    /// </summary>
    [Fact]
    public void Calculate_MarginBelowThreshold_AddsMarginRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            GerceklesenCiro: 100000m,
            HedefCiro: 100000m,
            HedefeUlasmaPct: 0.90m,
            OrtalamaMarjPct: 0.10m,
            OrtalamaIskontoPct: 0.10m,
            IadeOraniPct: 0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(40, result.RiskSkoru);
        Assert.Equal("ORTA", result.RiskSeviyesi);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Marj tam olarak threshold değerine eşit olduğunda
    /// risk oluşmaması gerektiğini test eder.
    ///
    /// Kural:
    /// Marj < 0.15
    ///
    /// Dolayısıyla:
    /// Marj = 0.15
    ///
    /// risk değildir.
    /// </summary>
    [Fact]
    public void Calculate_MarginExactlyAtThreshold_DoesNotAddMarginRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            100000m,
            100000m,
            0.90m,
            0.15m,
            0.10m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(0, result.RiskSkoru);

        Assert.DoesNotContain(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Hedef gerçekleşme oranı threshold değerinin altında olduğunda
    /// HEDEF_ALTINDA gerekçesinin oluşturulduğunu test eder.
    ///
    /// Hedef gerçekleşme:
    /// 0.70
    ///
    /// Threshold:
    /// 0.80
    ///
    /// Risk ağırlığı:
    /// 30
    /// </summary>
    [Fact]
    public void Calculate_TargetBelowThreshold_AddsTargetRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            70000m,
            100000m,
            0.70m,
            0.20m,
            0.10m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(30, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);

        Assert.Contains(
            "HEDEF_ALTINDA",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Hedef gerçekleşme oranı tam olarak 0.80 olduğunda
    /// risk oluşmaması gerektiğini test eder.
    ///
    /// Kural:
    /// Hedef < 0.80
    ///
    /// 0.80 değeri threshold'a eşit olduğu için risk değildir.
    /// </summary>
    [Fact]
    public void Calculate_TargetExactlyAtThreshold_DoesNotAddTargetRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            80000m,
            100000m,
            0.80m,
            0.20m,
            0.10m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(0, result.RiskSkoru);

        Assert.DoesNotContain(
            "HEDEF_ALTINDA",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Ortalama iskonto maksimum threshold değerini aştığında
    /// YUKSEK_ISKONTO gerekçesinin oluşturulduğunu test eder.
    ///
    /// İskonto:
    /// 0.20
    ///
    /// Maksimum:
    /// 0.15
    ///
    /// Risk ağırlığı:
    /// 20
    /// </summary>
    [Fact]
    public void Calculate_DiscountAboveThreshold_AddsDiscountRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            100000m,
            100000m,
            0.90m,
            0.20m,
            0.20m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(20, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);

        Assert.Contains(
            "YUKSEK_ISKONTO",
            result.GerekceKodlari);
    }

    /// <summary>
    /// İskonto tam olarak maksimum threshold değerine eşit olduğunda
    /// risk oluşmaması gerektiğini test eder.
    ///
    /// Kural:
    /// İskonto > 0.15
    ///
    /// Dolayısıyla 0.15 risk değildir.
    /// </summary>
    [Fact]
    public void Calculate_DiscountExactlyAtThreshold_DoesNotAddDiscountRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            100000m,
            100000m,
            0.90m,
            0.20m,
            0.15m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(0, result.RiskSkoru);

        Assert.DoesNotContain(
            "YUKSEK_ISKONTO",
            result.GerekceKodlari);
    }

    /// <summary>
    /// En az bir kritik stok ürünü bulunduğunda
    /// STOK_YETERSIZ riskinin oluşturulduğunu test eder.
    ///
    /// Kritik stok sayısı 1 olduğunda:
    ///
    /// Risk ağırlığı = 20
    /// </summary>
    [Fact]
    public void Calculate_CriticalStockExists_AddsStockRisk()
    {
        RiskEngine engine = CreateEngine();

        RiskEngineResult result = engine.Calculate(
            CreateSafeMetrics(),
            kritikStokUrunSayisi: 1);

        Assert.Equal(20, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Kritik stok ürünü bulunmadığında stok riskinin
    /// oluşturulmaması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Calculate_NoCriticalStock_DoesNotAddStockRisk()
    {
        RiskEngine engine = CreateEngine();

        RiskEngineResult result = engine.Calculate(
            CreateSafeMetrics(),
            kritikStokUrunSayisi: 0);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            result.GerekceKodlari);
    }

    /// <summary>
    /// İade oranı threshold değerine eşit olduğunda
    /// YUKSEK_IADE riskinin oluşturulduğunu test eder.
    ///
    /// Buradaki kural diğer bazı eşiklerden farklıdır:
    ///
    /// İade >= 0.10
    ///
    /// olduğu anda risk oluşur.
    /// </summary>
    [Fact]
    public void Calculate_ReturnRateAtThreshold_AddsReturnRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            100000m,
            100000m,
            0.90m,
            0.20m,
            0.10m,
            0.10m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(20, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);

        Assert.Contains(
            "YUKSEK_IADE",
            result.GerekceKodlari);
    }

    /// <summary>
    /// İade oranı threshold değerinin altında olduğunda
    /// iade riskinin oluşmaması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Calculate_ReturnRateBelowThreshold_DoesNotAddReturnRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            100000m,
            100000m,
            0.90m,
            0.20m,
            0.10m,
            0.09m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.DoesNotContain(
            "YUKSEK_IADE",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Birden fazla risk aynı anda oluştuğunda
    /// bütün risk ağırlıklarının toplandığını test eder.
    ///
    /// Marj riski      = 40
    /// Hedef riski     = 30
    /// İskonto riski   = 20
    /// Stok riski      = 20
    /// İade riski      = 20
    ///
    /// Toplam:
    /// 40 + 30 + 20 + 20 + 20 = 130
    ///
    /// 130 değeri ORTA sınırını geçtiği için YUKSEK olur.
    /// </summary>
    [Fact]
    public void Calculate_MultipleRisks_SumsAllRiskWeights()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            50000m,
            100000m,
            0.50m,
            0.10m,
            0.20m,
            0.15m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 5);

        Assert.Equal(130, result.RiskSkoru);
        Assert.Equal("YUKSEK", result.RiskSeviyesi);

        Assert.Contains(
            "MARJ_ESIK_ALTI",
            result.GerekceKodlari);

        Assert.Contains(
            "HEDEF_ALTINDA",
            result.GerekceKodlari);

        Assert.Contains(
            "YUKSEK_ISKONTO",
            result.GerekceKodlari);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);

        Assert.Contains(
            "YUKSEK_IADE",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Risk skoru 30 olduğunda DUSUK seviyesinde kalması gerektiğini
    /// test eder.
    ///
    /// DUSUK_max = 30
    /// </summary>
    [Fact]
    public void Calculate_ScoreAtLowMaximum_ReturnsLowRisk()
    {
        RiskEngine engine = CreateEngine();

        // 30 puan oluşturmak için yalnızca hedef riski kullanılır.
        SalesMetricsResult metrics = new(
            70000m,
            100000m,
            0.70m,
            0.20m,
            0.10m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(30, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);
    }

    /// <summary>
/// Risk skoru 60 olduğunda ORTA seviyesinde kalması gerektiğini
/// test eder.
///
/// ORTA_max = 60
///
/// 40 marj + 20 iskonto = 60.
///
/// Hedef gerçekleşme oranı 0.90 olduğu için
/// HEDEF_ALTINDA riski oluşmaz.
/// </summary>
[Fact]
public void Calculate_ScoreAtMediumMaximum_ReturnsMediumRisk()
{
    RiskEngine engine = CreateEngine();

    SalesMetricsResult metrics = new(
        90000m,
        100000m,
        0.90m,  // Hedef %90 -> risk yok
        0.10m,  // Marj %10 -> +40
        0.20m,  // İskonto %20 -> +20
        0.05m); // İade %5 -> risk yok

    RiskEngineResult result = engine.Calculate(
        metrics,
        kritikStokUrunSayisi: 0);

    Assert.Equal(60, result.RiskSkoru);
    Assert.Equal("ORTA", result.RiskSeviyesi);
}

    /// <summary>
    /// Risk skorunun ORTA threshold'unu geçtiğinde
    /// YUKSEK seviyeye yükseldiğini test eder.
    ///
    /// 60'ın üzerindeki herhangi bir skor YUKSEK olur.
    /// </summary>
    [Fact]
    public void Calculate_ScoreAboveMediumMaximum_ReturnsHighRisk()
    {
        RiskEngine engine = CreateEngine();

        // 40 marj + 30 hedef = 70 puan.
        SalesMetricsResult metrics = new(
            50000m,
            100000m,
            0.50m,
            0.10m,
            0.10m,
            0.05m);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(70, result.RiskSkoru);
        Assert.Equal("YUKSEK", result.RiskSeviyesi);
    }

    /// <summary>
    /// Metriklerden biri null olduğunda RiskEngine'in
    /// o metrik için risk üretmemesi gerektiğini test eder.
    ///
    /// Null:
    /// - Hedef
    /// - Marj
    /// - İskonto
    /// - İade
    ///
    /// gibi değerlerin "risk var" şeklinde yorumlanmaması gerekir.
    /// </summary>
    [Fact]
    public void Calculate_NullMetrics_DoesNotCreateMetricRisk()
    {
        RiskEngine engine = CreateEngine();

        SalesMetricsResult metrics = new(
            GerceklesenCiro: 100000m,
            HedefCiro: null,
            HedefeUlasmaPct: null,
            OrtalamaMarjPct: null,
            OrtalamaIskontoPct: null,
            IadeOraniPct: null);

        RiskEngineResult result = engine.Calculate(
            metrics,
            kritikStokUrunSayisi: 0);

        Assert.Equal(0, result.RiskSkoru);
        Assert.Equal("DUSUK", result.RiskSeviyesi);
        Assert.Empty(result.GerekceKodlari);
    }

    /// <summary>
    /// RiskEngine'e null metrics gönderildiğinde
    /// ArgumentNullException fırlatılması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Calculate_NullMetrics_ThrowsArgumentNullException()
    {
        RiskEngine engine = CreateEngine();

        Assert.Throws<ArgumentNullException>(() =>
            engine.Calculate(
                metrics: null!,
                kritikStokUrunSayisi: 0));
    }

    /// <summary>
    /// RiskEngine oluşturulurken null configuration gönderildiğinde
    /// ArgumentNullException fırlatılması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RiskEngine(null!));
    }
}
