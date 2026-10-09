using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BayiPerformansAppWeb.Business.Configuration;

namespace BayiPerformansAppWeb.Business.Calculations;

/// <summary>
/// Bayinin satış performansından deterministik risk skoru üretir.
///
/// Bu sınıf:
/// - Marj
/// - Hedef gerçekleşme
/// - İskonto
/// - Stok
/// - İade
///
/// gibi ölçülebilir kuralları değerlendirir.
///
/// Herhangi bir yapay zeka veya LLM çağrısı yapmaz.
/// Aynı girdiler her zaman aynı sonucu üretir.
/// </summary>
public sealed class RiskEngine
{
    private const string MarjRiskCode = "MARJ_ESIK_ALTI";
    private const string HedefRiskCode = "HEDEF_ALTINDA";
    private const string IskontoRiskCode = "YUKSEK_ISKONTO";
    private const string StokRiskCode = "STOK_YETERSIZ";
    private const string IadeRiskCode = "YUKSEK_IADE";

    private readonly BusinessRulesOptions _options;

    /// <summary>
    /// Risk motorunun configuration değerlerini alır.
    /// </summary>
    public RiskEngine(BusinessRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <summary>
    /// Verilen satış metriklerinden risk skorunu hesaplar.
    ///
    /// Her gerçekleşen risk kuralının ağırlığı skora eklenir.
    /// </summary>
    public RiskEngineResult Calculate(
        SalesMetricsResult metrics,
        int kritikStokUrunSayisi)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        List<string> gerekceKodlari = new();

        // Marj eşiğin altındaysa risk puanı eklenir.
        if (metrics.OrtalamaMarjPct.HasValue &&
            metrics.OrtalamaMarjPct.Value < _options.MarjEsigiPct)
        {
            gerekceKodlari.Add(MarjRiskCode);
        }

        // Hedef gerçekleşme oranı eşikten düşükse risk puanı eklenir.
        if (metrics.HedefeUlasmaPct.HasValue &&
            metrics.HedefeUlasmaPct.Value < _options.HedefRiskEsigiPct)
        {
            gerekceKodlari.Add(HedefRiskCode);
        }

        // Ortalama iskonto maksimum eşiği aşarsa risk oluşturulur.
        if (metrics.OrtalamaIskontoPct.HasValue &&
            metrics.OrtalamaIskontoPct.Value > _options.MaxIskontoPct)
        {
            gerekceKodlari.Add(IskontoRiskCode);
        }

        // Kritik stok bulunan ürünler varsa stok riski oluşturulur.
        if (kritikStokUrunSayisi > 0)
        {
            gerekceKodlari.Add(StokRiskCode);
        }

        // İade oranı eşik değerini aşıyorsa iade riski oluşturulur.
        if (metrics.IadeOraniPct.HasValue &&
            metrics.IadeOraniPct.Value >= _options.IadeRiskEsigiPct)
        {
            gerekceKodlari.Add(IadeRiskCode);
        }

        // Oluşturulan bütün gerekçelerin ağırlıkları toplanır.
        int riskSkoru = gerekceKodlari.Sum(
            code => GetRiskWeight(code));

        // Toplam puanın hangi risk seviyesine ait olduğu belirlenir.
        string riskSeviyesi = DetermineRiskLevel(riskSkoru);

        return new RiskEngineResult(
            RiskSkoru: riskSkoru,
            RiskSeviyesi: riskSeviyesi,
            GerekceKodlari: gerekceKodlari);
    }

    /// <summary>
    /// Bir risk gerekçesinin configuration içerisindeki
    /// ağırlığını döndürür.
    ///
    /// Configuration'da bulunmayan bir gerekçe için
    /// sıfır puan kullanılır.
    /// </summary>
    private int GetRiskWeight(string riskCode)
    {
        if (_options.RiskWeights.TryGetValue(
                riskCode,
                out int weight))
        {
            return weight;
        }

        return 0;
    }

    /// <summary>
    /// Risk skorunu DUSUK, ORTA veya YUKSEK olarak sınıflandırır.
    ///
    /// DUSUK:
    /// skor <= DUSUK_max
    ///
    /// ORTA:
    /// skor <= ORTA_max
    ///
    /// YUKSEK:
    /// skor <= YUKSEK_max
    ///
    /// Verilen threshold değerlerine göre maksimum değer
    /// 900 olarak kullanılmaktadır.
    /// </summary>
    private string DetermineRiskLevel(int riskScore)
    {
        int dusukMax = GetRiskLevel("DUSUK_max");
        int ortaMax = GetRiskLevel("ORTA_max");
        int yuksekMax = GetRiskLevel("YUKSEK_max");

        if (riskScore <= dusukMax)
        {
            return "DUSUK";
        }

        if (riskScore <= ortaMax)
        {
            return "ORTA";
        }

        if (riskScore <= yuksekMax)
        {
            return "YUKSEK";
        }

        // Configuration'daki maksimum değer aşılırsa
        // en yüksek risk seviyesi kullanılır.
        return "YUKSEK";
    }

    /// <summary>
    /// Risk seviyesi threshold değerini güvenli şekilde okur.
    /// </summary>
    private int GetRiskLevel(string key)
    {
        if (_options.RiskLevels.TryGetValue(
                key,
                out int value))
        {
            return value;
        }

        // Configuration eksikse güvenli tarafta kalmak için
        // maksimum seviyeyi 0 kabul ederiz.
        return 0;
    }
}

/// <summary>
/// RiskEngine tarafından oluşturulan sonucu temsil eder.
/// </summary>
public sealed record RiskEngineResult(
    int RiskSkoru,
    string RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari);
