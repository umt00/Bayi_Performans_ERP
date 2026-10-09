using BayiPerformansAppWeb.Business.Configuration;

namespace BayiPerformansAppWeb.Business.Calculations;

/// <summary>
/// Teklif değerlendirme kararlarını deterministik olarak verir.
///
/// Kurallar:
/// - Marj hesaplanır.
/// - Marj business threshold kontrol edilir.
/// - İskonto threshold kontrol edilir.
/// - Stok yeterliliği kontrol edilir.
/// - Gerekçe kodları oluşturulur.
/// - Risk skoru hesaplanır.
/// - Risk seviyesi belirlenir.
/// - Negatif marj REJECT sonucuna yol açar.
/// - DUSUK risk APPROVE.
/// - ORTA risk NEED_APPROVAL.
/// - YUKSEK risk NEED_APPROVAL.
///
/// AI veya LLM kullanmaz.
/// </summary>
public sealed class OfferDecisionEngine
{
    private readonly BusinessRulesOptions _options;

    public OfferDecisionEngine(
        BusinessRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <summary>
    /// Teklif için deterministik karar üretir.
    /// </summary>
    public OfferDecisionResult Evaluate(
        decimal miktar,
        decimal birimFiyat,
        decimal iskontoOrani,
        decimal? birimMaliyet,
        decimal? mevcutStok)
    {
        List<string> gerekceKodlari = new();

        // --------------------------------------------------
        // 1. Miktar kontrolü
        // --------------------------------------------------

        if (miktar <= 0)
        {
            gerekceKodlari.Add("GECERSIZ_MIKTAR");

            return CreateRejectedResult(
                gerekceKodlari,
                null,
                mevcutStok);
        }

        // --------------------------------------------------
        // 2. Birim fiyat kontrolü
        // --------------------------------------------------

        if (birimFiyat < 0)
        {
            gerekceKodlari.Add("GECERSIZ_BIRIM_FIYAT");

            return CreateRejectedResult(
                gerekceKodlari,
                null,
                mevcutStok);
        }

        // --------------------------------------------------
        // 3. İskonto kontrolü
        // --------------------------------------------------

        if (iskontoOrani < 0 ||
            iskontoOrani > 1)
        {
            gerekceKodlari.Add("GECERSIZ_ISKONTO");

            return CreateRejectedResult(
                gerekceKodlari,
                null,
                mevcutStok);
        }

        // --------------------------------------------------
        // 4. Maliyet kontrolü
        // --------------------------------------------------

        if (!birimMaliyet.HasValue)
        {
            gerekceKodlari.Add("MALIYET_BILGISI_YOK");

            return CreateRejectedResult(
                gerekceKodlari,
                null,
                mevcutStok);
        }

        if (birimMaliyet.Value < 0)
        {
            gerekceKodlari.Add("GECERSIZ_MALIYET");

            return CreateRejectedResult(
                gerekceKodlari,
                null,
                mevcutStok);
        }

        // --------------------------------------------------
        // 5. Net satış fiyatı
        // --------------------------------------------------

        decimal indirimliBirimFiyat =
            birimFiyat * (1 - iskontoOrani);

        if (indirimliBirimFiyat <= 0)
        {
            gerekceKodlari.Add("GECERSIZ_NET_FIYAT");

            return CreateRejectedResult(
                gerekceKodlari,
                null,
                mevcutStok);
        }

        // --------------------------------------------------
        // 6. Marj hesabı
        // --------------------------------------------------

        decimal marj =
            (indirimliBirimFiyat - birimMaliyet.Value)
            / indirimliBirimFiyat;

        // --------------------------------------------------
        // 7. Marj threshold kontrolü
        // --------------------------------------------------

        if (marj < _options.MarjEsigiPct)
        {
            gerekceKodlari.Add("MARJ_ESIK_ALTI");
        }

        // --------------------------------------------------
        // 8. İskonto threshold kontrolü
        // --------------------------------------------------

        if (iskontoOrani > _options.MaxIskontoPct)
        {
            gerekceKodlari.Add("YUKSEK_ISKONTO");
        }

        // --------------------------------------------------
        // 9. Stok kontrolü
        // --------------------------------------------------

        if (mevcutStok.HasValue &&
            mevcutStok.Value < miktar)
        {
            gerekceKodlari.Add("STOK_YETERSIZ");
        }

        // --------------------------------------------------
        // 10. Risk skoru
        // --------------------------------------------------

        int riskSkoru =
            GetRiskScore(gerekceKodlari);

        // --------------------------------------------------
        // 11. Risk seviyesi
        // --------------------------------------------------

        string riskSeviyesi =
            GetRiskLevel(riskSkoru);

        // --------------------------------------------------
        // 12. Karar
        // --------------------------------------------------
        //
        // marj < 0
        //     REJECT
        //
        // DUSUK
        //     APPROVE
        //
        // ORTA
        //     NEED_APPROVAL
        //
        // YUKSEK
        //     NEED_APPROVAL
        //
        // --------------------------------------------------

        if (marj < _options.MinMarjRedPct)
        {
            return new OfferDecisionResult(
                Karar: "REJECT",
                GerekceKodlari: gerekceKodlari,
                HesaplananMarjPct: marj,
                MevcutStok: mevcutStok,
                RiskSkoru: riskSkoru,
                RiskSeviyesi: riskSeviyesi);
        }

        string karar =
            riskSeviyesi == "DUSUK"
                ? "APPROVE"
                : "NEED_APPROVAL";

        return new OfferDecisionResult(
            Karar: karar,
            GerekceKodlari: gerekceKodlari,
            HesaplananMarjPct: marj,
            MevcutStok: mevcutStok,
            RiskSkoru: riskSkoru,
            RiskSeviyesi: riskSeviyesi);
    }

    /// <summary>
    /// Gerekçe kodlarının ağırlıklarını toplayarak
    /// toplam risk skorunu hesaplar.
    /// </summary>
    private int GetRiskScore(
        IReadOnlyList<string> gerekceKodlari)
    {
        int toplamSkor = 0;

        foreach (string gerekceKodu in gerekceKodlari)
        {
            if (_options.RiskWeights.TryGetValue(
                    gerekceKodu,
                    out int weight))
            {
                toplamSkor += weight;
            }
        }

        return toplamSkor;
    }

    /// <summary>
    /// Risk skorunu risk seviyesine dönüştürür.
    ///
    /// DUSUK:
    /// 0 - DUSUK_max
    ///
    /// ORTA:
    /// DUSUK_max + 1 - ORTA_max
    ///
    /// YUKSEK:
    /// ORTA_max + 1 - YUKSEK_max
    /// </summary>
    private string GetRiskLevel(
        int riskSkoru)
    {
        int dusukMax =
            GetRiskLevelValue("DUSUK_max");

        int ortaMax =
            GetRiskLevelValue("ORTA_max");

        int yuksekMax =
            GetRiskLevelValue("YUKSEK_max");

        if (riskSkoru <= dusukMax)
        {
            return "DUSUK";
        }

        if (riskSkoru <= ortaMax)
        {
            return "ORTA";
        }

        if (riskSkoru <= yuksekMax)
        {
            return "YUKSEK";
        }

        // Configuration'daki YUKSEK_max değerini
        // aşan skorlar da en yüksek risk seviyesinde tutulur.
        return "YUKSEK";
    }

    /// <summary>
    /// RiskLevels dictionary içerisinden
    /// belirtilen değeri okur.
    /// </summary>
    private int GetRiskLevelValue(
        string key)
    {
        if (_options.RiskLevels.TryGetValue(
                key,
                out int value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Risk seviyesi configuration değeri bulunamadı: {key}");
    }

    /// <summary>
    /// Kesin red durumları için ortak sonuç oluşturur.
    /// </summary>
    private static OfferDecisionResult CreateRejectedResult(
        IReadOnlyList<string> gerekceKodlari,
        decimal? marj,
        decimal? mevcutStok)
    {
        return new OfferDecisionResult(
            Karar: "REJECT",
            GerekceKodlari: gerekceKodlari,
            HesaplananMarjPct: marj,
            MevcutStok: mevcutStok,
            RiskSkoru: 0,
            RiskSeviyesi: "YUKSEK");
    }
}

/// <summary>
/// Teklif karar motorunun sonucunu taşır.
/// </summary>
public sealed record OfferDecisionResult(
    string Karar,
    IReadOnlyList<string> GerekceKodlari,
    decimal? HesaplananMarjPct,
    decimal? MevcutStok,
    int RiskSkoru,
    string RiskSeviyesi);