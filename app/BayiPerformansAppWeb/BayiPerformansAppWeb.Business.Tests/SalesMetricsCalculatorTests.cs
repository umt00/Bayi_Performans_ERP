using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BayiPerformansAppWeb.Business.Calculations;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests.Calculations;

/// <summary>
/// SalesMetricsCalculator sınıfının hesaplama kurallarını test eder.
///
/// Bu testlerin amacı:
/// - Hesaplamaların beklenen sonucu verdiğini kontrol etmek
/// - Geçersiz değerlerde hata üretildiğini kontrol etmek
/// - İade, iskonto, hedef ve marj gibi farklı durumların
///   doğru şekilde işlendiğini doğrulamaktır.
///
/// Testler gerçek Supabase veya başka bir veri kaynağına bağlanmaz.
/// Test verileri doğrudan testlerin içinde oluşturulur.
///
/// ÖNEMLİ:
/// SalesMetricsCalculator oranları 0-1 aralığında döndürür.
///
/// Örnek:
/// %15 = 0.15
/// %20 = 0.20
/// %72 = 0.72
/// %80 = 0.80
/// </summary>
public sealed class SalesMetricsCalculatorTests
{
    // Test edeceğimiz SalesMetricsCalculator nesnesi.
    private readonly SalesMetricsCalculator _calculator = new();

    /// <summary>
    /// Normal bir satışta net satır tutarının,
    /// iskonto uygulandıktan sonra doğru hesaplandığını test eder.
    ///
    /// 10 adet × 1000 TL = 10000 TL brüt
    /// %10 iskonto sonrası = 9000 TL
    /// </summary>
    [Fact]
    public void CalculateNetLineAmount_NormalSale_ReturnsDiscountedAmount()
    {
        decimal result = _calculator.CalculateNetLineAmount(
            miktar: 10,
            birimFiyat: 1000,
            iskontoOrani: 0.10m);

        // Beklenen sonuç 9000 TL.
        Assert.Equal(9000m, result);
    }

    /// <summary>
    /// Negatif miktarlı bir satış/iade satırının
    /// negatif net tutar ürettiğini test eder.
    ///
    /// -2 adet × 1000 TL × %90 = -1800 TL
    ///
    /// Bu, negatif miktarlı iadenin ciroyu azaltabildiğini doğrular.
    /// </summary>
    [Fact]
    public void CalculateNetLineAmount_ReturnSale_ReturnsNegativeAmount()
    {
        decimal result = _calculator.CalculateNetLineAmount(
            miktar: -2,
            birimFiyat: 1000,
            iskontoOrani: 0.10m);

        // Beklenen sonuç -1800 TL.
        Assert.Equal(-1800m, result);
    }

    /// <summary>
    /// IsIade=true olan pozitif miktarlı bir kaydın da
    /// cirodan düşürüldüğünü test eder.
    ///
    /// Miktar = 2
    /// Birim fiyat = 1000
    /// İskonto = %10
    ///
    /// İade olduğu için:
    /// -2 × 1000 × 0.90 = -1800
    /// </summary>
    [Fact]
    public void CalculateNetLineAmount_ReturnFlagWithPositiveQuantity_ReturnsNegativeAmount()
    {
        decimal result = _calculator.CalculateNetLineAmount(
            miktar: 2,
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            isIade: true);

        // IsIade=true olduğu için sonuç negatif olmalıdır.
        Assert.Equal(-1800m, result);
    }

    /// <summary>
    /// Normal satış ve iade birlikte bulunduğunda
    /// gerçekleşen net cironun doğru hesaplandığını test eder.
    ///
    /// İlk satır:
    /// 10 × 1000 × 0.90 = 9000
    ///
    /// İkinci satır:
    /// -2 × 1000 × 0.90 = -1800
    ///
    /// Toplam:
    /// 9000 - 1800 = 7200
    /// </summary>
    [Fact]
    public void CalculateRealizedRevenue_NormalSalesAndReturns_ReturnsNetRevenue()
    {
        List<SalesMetricLine> salesLines =
        [
            new(1, 10, 1000, 0.10m, 700, false),
            new(2, -2, 1000, 0.10m, 700, true)
        ];

        decimal result = _calculator.CalculateRealizedRevenue(salesLines);

        // İade cirodan düşüldüğü için beklenen net ciro 7200 TL.
        Assert.Equal(7200m, result);
    }

    /// <summary>
    /// Tek bir satış satırının marj oranının doğru hesaplandığını test eder.
    ///
    /// Birim fiyat: 1000
    /// İskonto: %10 = 0.10
    /// İndirimli satış fiyatı: 900
    /// Maliyet: 700
    ///
    /// Marj:
    /// (900 - 700) / 900
    /// = 0.222222...
    ///
    /// Yani yaklaşık %22.22.
    ///
    /// Business katmanında sonuç 0-1 formatında tutulur:
    /// %22.22 -> yaklaşık 0.22
    /// </summary>
    [Fact]
    public void CalculateLineMarginPercent_ValidInputs_ReturnsExpectedMargin()
    {
        decimal? result = _calculator.CalculateLineMarginPercent(
            birimFiyat: 1000,
            iskontoOrani: 0.10m,
            birimMaliyet: 700);

        // Sonucun null olmaması bekleniyor.
        Assert.NotNull(result);

        // Sonuç 0-1 formatında tutulduğu için
        // %22.22 yaklaşık 0.22'dir.
        Assert.Equal(
            0.22m,
            decimal.Round(result.Value, 2));
    }

    /// <summary>
    /// Birden fazla satış satırında ağırlıklı ortalama marjın
    /// doğru hesaplandığını test eder.
    ///
    /// Normal satış ve iade aynı maliyet bilgisine sahip olduğu için
    /// toplam net ciro ve toplam kâr üzerinden marj hesaplanır.
    ///
    /// Sonuç yaklaşık:
    /// 0.2222
    ///
    /// Bu da yaklaşık %22.22 marja karşılık gelir.
    /// </summary>
    [Fact]
    public void CalculateWeightedAverageMarginPercent_NormalSalesAndReturns_ReturnsExpectedMargin()
    {
        List<SalesMetricLine> salesLines =
        [
            new(1, 10, 1000, 0.10m, 700, false),
            new(2, -2, 1000, 0.10m, 700, true)
        ];

        decimal? result =
            _calculator.CalculateWeightedAverageMarginPercent(
                salesLines);

        // Sonucun hesaplanabilir olması bekleniyor.
        Assert.NotNull(result);

        // Business katmanında oran 0-1 formatındadır.
        // %22.22 -> yaklaşık 0.22.
        Assert.Equal(
            0.22m,
            decimal.Round(result.Value, 2));
    }

    /// <summary>
    /// Hedef ciro mevcut olduğunda gerçekleşen cironun
    /// hedefe ulaşma oranının doğru hesaplandığını test eder.
    ///
    /// 900000 / 1000000 = 0.90
    ///
    /// Yani hedefin %90'ına ulaşılmıştır.
    ///
    /// Business katmanında sonuç:
    /// %90 -> 0.90
    /// </summary>
    [Fact]
    public void CalculateTargetAchievementPercent_TargetExists_ReturnsPercentage()
    {
        decimal? result =
            _calculator.CalculateTargetAchievementPercent(
                gerceklesenCiro: 900000,
                hedefCiro: 1000000);

        // %90 = 0.90
        Assert.Equal(0.90m, result);
    }

    /// <summary>
    /// Hedef ciro null olduğunda hedefe ulaşma oranının
    /// hesaplanamayacağını ve null döndüğünü test eder.
    /// </summary>
    [Fact]
    public void CalculateTargetAchievementPercent_TargetMissing_ReturnsNull()
    {
        decimal? result =
            _calculator.CalculateTargetAchievementPercent(
                gerceklesenCiro: 900000,
                hedefCiro: null);

        // Hedef olmadığı için sonuç null olmalı.
        Assert.Null(result);
    }

    /// <summary>
    /// Hedef ciro 0 olduğunda hedefe ulaşma oranının
    /// hesaplanamayacağını test eder.
    ///
    /// Sıfıra bölme yapılmamalıdır.
    /// </summary>
    [Fact]
    public void CalculateTargetAchievementPercent_ZeroTarget_ReturnsNull()
    {
        decimal? result =
            _calculator.CalculateTargetAchievementPercent(
                gerceklesenCiro: 900000,
                hedefCiro: 0);

        // Hedef 0 olduğu için oran hesaplanamaz.
        Assert.Null(result);
    }

    /// <summary>
    /// Ortalama iskonto hesabında sadece pozitif satış miktarlarının
    /// kullanıldığını ve ağırlıklı ortalama hesaplandığını test eder.
    ///
    /// İlk satış:
    /// 10 × 1000 = 10000 brüt, %10 iskonto
    ///
    /// İkinci satış:
    /// 5 × 2000 = 10000 brüt, %20 iskonto
    ///
    /// İade satırı:
    /// -2 × 1000
    ///
    /// İade satırı bu hesaplamaya dahil edilmez.
    ///
    /// İki pozitif satışın brüt tutarları eşit olduğu için:
    ///
    /// (%10 + %20) / 2 = %15
    ///
    /// Business katmanında:
    /// %15 -> 0.15
    /// </summary>
    [Fact]
    public void CalculateAverageDiscountPercent_UsesPositiveSalesOnly_ReturnsWeightedAverage()
    {
        List<SalesMetricLine> salesLines =
        [
            new(1, 10, 1000, 0.10m, 700, false),
            new(2, 5, 2000, 0.20m, 1400, false),
            new(3, -2, 1000, 0.50m, 700, true)
        ];

        decimal? result =
            _calculator.CalculateAverageDiscountPercent(
                salesLines);

        // Hesaplamanın yapılabilmesi bekleniyor.
        Assert.NotNull(result);

        // %15 = 0.15.
        Assert.Equal(0.15m, result);
    }

    /// <summary>
    /// Satışlar içerisinde iade bulunduğunda iade oranının
    /// doğru hesaplandığını test eder.
    ///
    /// Pozitif satış miktarı:
    /// 10 + 5 = 15
    ///
    /// İade miktarı:
    /// 3
    ///
    /// İade oranı:
    /// 3 / 15 = 0.20
    ///
    /// Yani %20.
    /// </summary>
    [Fact]
    public void CalculateReturnRatePercent_WhenReturnExists_ReturnsReturnPercentage()
    {
        List<SalesMetricLine> salesLines =
        [
            new(1, 10, 1000, 0m, 700, false),
            new(2, 5, 500, 0m, 350, false),
            new(3, -3, 1000, 0m, 700, true)
        ];

        decimal? result =
            _calculator.CalculateReturnRatePercent(
                salesLines);

        // %20 = 0.20.
        Assert.Equal(0.20m, result);
    }

    /// <summary>
    /// Pozitif miktarlı fakat IsIade=true olan bir kaydın
    /// iade oranına dahil edildiğini test eder.
    ///
    /// Satış:
    /// 10 adet
    ///
    /// İade:
    /// 2 adet, fakat miktar pozitif ve IsIade=true
    ///
    /// İade oranı:
    /// 2 / 10 = 0.20
    /// </summary>
    [Fact]
    public void CalculateReturnRatePercent_ReturnFlagWithPositiveQuantity_IsIncluded()
    {
        List<SalesMetricLine> salesLines =
        [
            new(1, 10, 1000, 0m, 700, false),
            new(2, 2, 1000, 0m, 700, true)
        ];

        decimal? result =
            _calculator.CalculateReturnRatePercent(
                salesLines);

        // %20 = 0.20.
        Assert.Equal(0.20m, result);
    }

    /// <summary>
    /// Calculate metodunun bütün temel metrikleri aynı anda
    /// doğru şekilde hesaplayıp sonuç nesnesine koyduğunu test eder.
    ///
    /// Bu test, sınıfın üst seviye Calculate metodunun
    /// diğer hesaplama metotlarıyla doğru şekilde çalıştığını
    /// topluca kontrol eder.
    /// </summary>
    [Fact]
    public void Calculate_AllMetrics_ReturnsExpectedResult()
    {
        List<SalesMetricLine> salesLines =
        [
            new(1, 10, 1000, 0.10m, 700, false),
            new(2, -2, 1000, 0.10m, 700, true)
        ];

        SalesMetricsResult result =
            _calculator.Calculate(
                salesLines,
                targetRevenue: 10000);

        // Net gerçekleşen ciro:
        // 9000 - 1800 = 7200
        Assert.Equal(
            7200m,
            result.GerceklesenCiro);

        // Testte verilen hedef ciro.
        Assert.Equal(
            10000m,
            result.HedefCiro);

        // Hedefe ulaşma:
        // 7200 / 10000 = 0.72
        //
        // Yani %72.
        Assert.Equal(
            0.72m,
            result.HedefeUlasmaPct);

        // Ağırlıklı ortalama marj:
        // yaklaşık 0.2222
        //
        // Yani yaklaşık %22.22.
        Assert.Equal(
            0.22m,
            decimal.Round(
                result.OrtalamaMarjPct!.Value,
                2));

        // Satışların iskonto oranı:
        // %10 = 0.10
        Assert.Equal(
            0.10m,
            result.OrtalamaIskontoPct);

        // İade oranı:
        // 2 / 10 = 0.20
        //
        // Yani %20.
        Assert.Equal(
            0.20m,
            result.IadeOraniPct);
    }

    /// <summary>
    /// İskonto oranı 1'den büyük olduğunda
    /// SalesMetricsCalculator'ın ArgumentOutOfRangeException
    /// fırlattığını test eder.
    ///
    /// İskonto oranı bu projede 0 ile 1 arasında olmalıdır.
    ///
    /// Örneğin:
    /// %10 = 0.10
    /// %50 = 0.50
    /// %100 = 1.00
    ///
    /// 1.20 ise %120 anlamına geldiği için geçersizdir.
    /// </summary>
    [Fact]
    public void CalculateNetLineAmount_DiscountRateGreaterThanOne_ThrowsException()
    {
        // Geçersiz iskonto oranı verildiğinde exception bekleniyor.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.CalculateNetLineAmount(
                miktar: 10,
                birimFiyat: 1000,
                iskontoOrani: 1.20m));
    }
}
