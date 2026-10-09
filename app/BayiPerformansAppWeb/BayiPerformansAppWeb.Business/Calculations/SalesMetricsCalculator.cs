using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BayiPerformansAppWeb.Business.Calculations;

/// <summary>
/// Satış verilerinden temel performans metriklerini hesaplayan Business sınıfıdır.
///
/// Bu sınıf:
/// - Gerçekleşen net ciroyu,
/// - Hedefe ulaşma oranını,
/// - Ağırlıklı ortalama marjı,
/// - Ağırlıklı ortalama iskontoyu,
/// - İade oranını
/// hesaplar.
///
/// Bu sınıfın veritabanına veya Supabase'e doğrudan erişimi yoktur.
/// Kendisine verilen SalesMetricLine kayıtları üzerinde çalışır.
///
/// ÖNEMLİ:
/// Business katmanında oranlar 0-1 arasında tutulur.
///
/// Örnek:
/// %15 -> 0.15
/// %80 -> 0.80
/// %10 -> 0.10
///
/// Bu yaklaşım thresholds.json ile doğrudan uyumludur.
/// </summary>
public sealed class SalesMetricsCalculator
{
    /// <summary>
    /// Verilen satış satırlarından bütün temel satış metriklerini hesaplar.
    ///
    /// targetRevenue:
    /// İlgili dönem için bayinin hedef cirosudur.
    ///
    /// Hedef bulunmuyorsa null olarak korunur.
    /// </summary>
    public SalesMetricsResult Calculate(
        IReadOnlyCollection<SalesMetricLine> salesLines,
        decimal? targetRevenue)
    {
        ArgumentNullException.ThrowIfNull(salesLines);

        // Önce net gerçekleşen ciro hesaplanır.
        decimal realizedRevenue =
            CalculateRealizedRevenue(salesLines);

        return new SalesMetricsResult(
            GerceklesenCiro: realizedRevenue,
            HedefCiro: targetRevenue,
            HedefeUlasmaPct:
                CalculateTargetAchievementPercent(
                    realizedRevenue,
                    targetRevenue),

            OrtalamaMarjPct:
                CalculateWeightedAverageMarginPercent(
                    salesLines),

            OrtalamaIskontoPct:
                CalculateAverageDiscountPercent(
                    salesLines),

            IadeOraniPct:
                CalculateReturnRatePercent(
                    salesLines));
    }

    /// <summary>
    /// Tek bir satış satırının iskonto sonrası net tutarını hesaplar.
    ///
    /// Normal satış:
    /// Miktar × BirimFiyat × (1 - Iskonto)
    ///
    /// İade:
    /// -abs(Miktar) × BirimFiyat × (1 - Iskonto)
    ///
    /// Böylece IsIade=true olan pozitif miktarlı kayıtlar da
    /// cirodan düşürülür.
    /// </summary>
    public decimal CalculateNetLineAmount(
        decimal miktar,
        decimal birimFiyat,
        decimal iskontoOrani,
        bool isIade = false)
    {
        ValidateBirimFiyat(birimFiyat);
        ValidateIskontoOrani(iskontoOrani);

        // İade kayıtlarında miktarın pozitif veya negatif gelmesinden
        // bağımsız olarak cirodan düşmesini sağlarız.
        decimal effectiveQuantity = isIade
            ? -Math.Abs(miktar)
            : miktar;

        return effectiveQuantity
            * birimFiyat
            * (1 - iskontoOrani);
    }

    /// <summary>
    /// Bütün satış satırlarının net tutarlarını toplayarak
    /// gerçekleşen net ciroyu hesaplar.
    ///
    /// İade kayıtları cirodan düşülür.
    /// </summary>
    public decimal CalculateRealizedRevenue(
        IEnumerable<SalesMetricLine> salesLines)
    {
        ArgumentNullException.ThrowIfNull(salesLines);

        decimal toplamNetCiro = 0;

        foreach (SalesMetricLine salesLine in salesLines)
        {
            toplamNetCiro += CalculateNetLineAmount(
                salesLine.Miktar,
                salesLine.BirimFiyat,
                salesLine.IskontoOrani,
                salesLine.IsIade);
        }

        return toplamNetCiro;
    }

    /// <summary>
    /// Tek bir satış satırının marj oranını hesaplar.
    ///
    /// Formül:
    ///
    /// (İndirimliBirimFiyat - BirimMaliyet)
    /// / İndirimliBirimFiyat
    ///
    /// Sonuç 0-1 aralığında tutulur.
    ///
    /// Örneğin:
    /// %15 marj -> 0.15
    /// </summary>
    public decimal? CalculateLineMarginPercent(
        decimal birimFiyat,
        decimal iskontoOrani,
        decimal birimMaliyet)
    {
        ValidateBirimFiyat(birimFiyat);
        ValidateIskontoOrani(iskontoOrani);

        if (birimMaliyet < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(birimMaliyet),
                "Birim maliyet negatif olamaz.");
        }

        decimal indirimliBirimFiyat =
            birimFiyat * (1 - iskontoOrani);

        if (indirimliBirimFiyat <= 0)
        {
            return null;
        }

        return
            (indirimliBirimFiyat - birimMaliyet)
            / indirimliBirimFiyat;
    }

    /// <summary>
    /// Satışların ağırlıklı ortalama marjını hesaplar.
    ///
    /// Formül:
    ///
    /// Toplam Kâr / Toplam Net Ciro
    ///
    /// Sonuç 0-1 aralığındadır.
    ///
    /// Birim maliyeti bulunmayan satırlar marj hesabına
    /// dahil edilmez.
    /// </summary>
    public decimal? CalculateWeightedAverageMarginPercent(
        IEnumerable<SalesMetricLine> salesLines)
    {
        ArgumentNullException.ThrowIfNull(salesLines);

        decimal toplamNetCiro = 0;
        decimal toplamKar = 0;

        foreach (SalesMetricLine salesLine in salesLines)
        {
            // Maliyet yoksa bu satır için marj hesaplanamaz.
            if (salesLine.BirimMaliyet is null)
            {
                continue;
            }

            decimal netSatirTutari =
                CalculateNetLineAmount(
                    salesLine.Miktar,
                    salesLine.BirimFiyat,
                    salesLine.IskontoOrani,
                    salesLine.IsIade);

            // İade satırında maliyet de negatif yönde etkilenmelidir.
            decimal effectiveQuantity = salesLine.IsIade
                ? -Math.Abs(salesLine.Miktar)
                : salesLine.Miktar;

            decimal toplamSatirMaliyeti =
                effectiveQuantity * salesLine.BirimMaliyet.Value;

            toplamNetCiro += netSatirTutari;

            toplamKar +=
                netSatirTutari - toplamSatirMaliyeti;
        }

        // Net ciro 0 ise marj oranı anlamlı şekilde hesaplanamaz.
        if (toplamNetCiro <= 0)
        {
            return null;
        }

        return toplamKar / toplamNetCiro;
    }

    /// <summary>
    /// Gerçekleşen cironun hedefe ulaşma oranını hesaplar.
    ///
    /// Formül:
    ///
    /// GerçekleşenCiro / HedefCiro
    ///
    /// Sonuç 0-1 veya 1'in üzerinde olabilir.
    ///
    /// Örneğin:
    /// Hedef = 1000
    /// Gerçekleşen = 800
    ///
    /// Sonuç = 0.80
    ///
    /// Bu değer thresholds.json'daki
    /// hedef_risk_esigi_pct = 0.80
    /// ile doğrudan karşılaştırılabilir.
    /// </summary>
    public decimal? CalculateTargetAchievementPercent(
        decimal gerceklesenCiro,
        decimal? hedefCiro)
    {
        if (hedefCiro is null || hedefCiro <= 0)
        {
            return null;
        }

        return gerceklesenCiro / hedefCiro.Value;
    }

    /// <summary>
    /// Ağırlıklı ortalama iskonto oranını hesaplar.
    ///
    /// Brüt satış tutarı ağırlık olarak kullanılır.
    ///
    /// Sonuç 0-1 aralığındadır.
    ///
    /// Örneğin:
    /// %15 iskonto -> 0.15
    /// </summary>
    public decimal? CalculateAverageDiscountPercent(
        IEnumerable<SalesMetricLine> salesLines)
    {
        ArgumentNullException.ThrowIfNull(salesLines);

        decimal toplamAgirlik = 0;
        decimal toplamAgirlikliIskonto = 0;

        foreach (SalesMetricLine salesLine in salesLines)
        {
            if (salesLine.Miktar <= 0)
            {
                continue;
            }

            ValidateBirimFiyat(salesLine.BirimFiyat);
            ValidateIskontoOrani(salesLine.IskontoOrani);

            decimal brutSatirTutari =
                salesLine.Miktar
                * salesLine.BirimFiyat;

            if (brutSatirTutari <= 0)
            {
                continue;
            }

            toplamAgirlik += brutSatirTutari;

            toplamAgirlikliIskonto +=
                brutSatirTutari
                * salesLine.IskontoOrani;
        }

        if (toplamAgirlik <= 0)
        {
            return null;
        }

        return toplamAgirlikliIskonto / toplamAgirlik;
    }

    /// <summary>
    /// İade oranını hesaplar.
    ///
    /// İade kabul edilen kayıtlar:
    ///
    /// - IsIade == true
    /// VEYA
    /// - Miktar < 0
    ///
    /// Formül:
    ///
    /// Mutlak İade Miktarı / Pozitif Satış Miktarı
    ///
    /// Sonuç 0-1 aralığındadır.
    ///
    /// Örneğin:
    /// %10 iade -> 0.10
    /// </summary>
    public decimal? CalculateReturnRatePercent(
        IEnumerable<SalesMetricLine> salesLines)
    {
        ArgumentNullException.ThrowIfNull(salesLines);

        decimal pozitifSatisMiktari = 0;
        decimal mutlakIadeMiktari = 0;

        foreach (SalesMetricLine salesLine in salesLines)
        {
            bool iadeMi =
                salesLine.IsIade
                || salesLine.Miktar < 0;

            if (iadeMi)
            {
                mutlakIadeMiktari +=
                    Math.Abs(salesLine.Miktar);

                continue;
            }

            if (salesLine.Miktar > 0)
            {
                pozitifSatisMiktari +=
                    salesLine.Miktar;
            }
        }

        if (pozitifSatisMiktari <= 0)
        {
            return null;
        }

        return mutlakIadeMiktari
            / pozitifSatisMiktari;
    }

    /// <summary>
    /// Birim fiyatın negatif olmamasını kontrol eder.
    /// </summary>
    private static void ValidateBirimFiyat(
        decimal birimFiyat)
    {
        if (birimFiyat < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(birimFiyat),
                "Birim fiyat negatif olamaz.");
        }
    }

    /// <summary>
    /// İskonto oranının 0-1 arasında olmasını kontrol eder.
    ///
    /// %15 = 0.15 şeklinde gönderilmelidir.
    /// </summary>
    private static void ValidateIskontoOrani(
        decimal iskontoOrani)
    {
        if (iskontoOrani < 0 || iskontoOrani > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iskontoOrani),
                "İskonto oranı 0 ile 1 arasında olmalıdır.");
        }
    }
}

/// <summary>
/// SalesMetricsCalculator'ın ihtiyaç duyduğu tek bir satış satırını temsil eder.
///
/// Bu model DataAccess modelinden farklıdır.
/// DataAccess'ten gelen SatisReadModel ve UrunReadModel,
/// Business katmanında birleştirilerek bu modele dönüştürülür.
/// </summary>
public sealed record SalesMetricLine(
    string? SatisId,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani,
    decimal? BirimMaliyet,
    bool IsIade)
{
    public SalesMetricLine(
        long? SatisId,
        decimal Miktar,
        decimal BirimFiyat,
        decimal IskontoOrani,
        decimal? BirimMaliyet,
        bool IsIade)
        : this(
            SatisId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Miktar,
            BirimFiyat,
            IskontoOrani,
            BirimMaliyet,
            IsIade)
    {
    }
}

/// <summary>
/// SalesMetricsCalculator tarafından üretilen temel satış metrikleridir.
///
/// Oran alanlarının tamamı 0-1 formatındadır.
///
/// Örneğin:
/// 0.15 = %15
/// 0.80 = %80
/// </summary>
public sealed record SalesMetricsResult(
    decimal GerceklesenCiro,
    decimal? HedefCiro,
    decimal? HedefeUlasmaPct,
    decimal? OrtalamaMarjPct,
    decimal? OrtalamaIskontoPct,
    decimal? IadeOraniPct);
