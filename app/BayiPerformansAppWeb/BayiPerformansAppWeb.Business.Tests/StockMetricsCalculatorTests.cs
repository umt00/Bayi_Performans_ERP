using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Entities.ReadModels;

using Xunit;

namespace BayiPerformansAppWeb.Business.Tests.Calculations;

/// <summary>
/// StockMetricsCalculator sınıfının stok hesaplama kurallarını test eder.
///
/// Testler:
/// - Ortalama günlük satış hesabını
/// - Tahmini stok günü hesabını
/// - Kritik stok threshold kontrolünü
/// - Minimum stok seviyesini
/// - STOK_YETERSIZ gerekçesini
/// - Bütün hesaplamaların Calculate metodu üzerinden birlikte
///   doğru çalışmasını
/// - Geçersiz değerlerde exception oluşmasını
///
/// kontrol eder.
///
/// Testler gerçek veritabanına veya Supabase'e bağlanmaz.
/// Bütün test verileri doğrudan test içerisinde oluşturulur.
/// </summary>
public sealed class StockMetricsCalculatorTests
{
    /// <summary>
    /// Testlerde kullanılan stok gün threshold değeridir.
    ///
    /// thresholds.json içerisindeki:
    ///
    /// "stok_gun_esigi": 7
    ///
    /// değeriyle aynıdır.
    /// </summary>
    private const decimal StokGunEsigi = 7m;

    /// <summary>
    /// Test için StockMetricsCalculator oluşturur.
    /// </summary>
    private static StockMetricsCalculator CreateCalculator()
    {
        return new StockMetricsCalculator(
            StokGunEsigi);
    }

    /// <summary>
    /// Son 90 günlük satış miktarının günlük satış hızına
    /// doğru şekilde dönüştürüldüğünü test eder.
    ///
    /// 90 adet / 90 gün = 1 adet/gün
    /// </summary>
    [Fact]
    public void CalculateAverageDailySales_ValidSales_ReturnsDailyAverage()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateAverageDailySales(
                sonUcAySatisAdedi: 90m);

        Assert.NotNull(result);
        Assert.Equal(1m, result.Value);
    }

    /// <summary>
    /// 180 adet satışın günlük ortalama satış hızının
    /// 2 adet/gün olduğunu test eder.
    ///
    /// 180 / 90 = 2
    /// </summary>
    [Fact]
    public void CalculateAverageDailySales_180Units_ReturnsTwoUnitsPerDay()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateAverageDailySales(
                sonUcAySatisAdedi: 180m);

        Assert.NotNull(result);
        Assert.Equal(2m, result.Value);
    }

    /// <summary>
    /// Son üç ayda hiç satış yoksa günlük satış hızının
    /// hesaplanamayacağını ve null dönmesi gerektiğini test eder.
    /// </summary>
    [Fact]
    public void CalculateAverageDailySales_ZeroSales_ReturnsNull()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateAverageDailySales(
                sonUcAySatisAdedi: 0m);

        Assert.Null(result);
    }

    /// <summary>
    /// Son üç aylık satış miktarı negatif olduğunda
    /// bunun geçerli bir satış miktarı olarak kabul edilmemesi
    /// ve null dönmesi gerektiğini test eder.
    ///
    /// Calculator mevcut davranışında:
    /// <= 0 -> null
    /// döndürmektedir.
    /// </summary>
    [Fact]
    public void CalculateAverageDailySales_NegativeSales_ReturnsNull()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateAverageDailySales(
                sonUcAySatisAdedi: -10m);

        Assert.Null(result);
    }

    /// <summary>
    /// Mevcut stok ve günlük satış hızından
    /// tahmini stok gününün doğru hesaplandığını test eder.
    ///
    /// Mevcut stok = 14
    /// Günlük satış = 2
    ///
    /// 14 / 2 = 7 gün
    /// </summary>
    [Fact]
    public void CalculateEstimatedStockDays_ValidValues_ReturnsExpectedDays()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateEstimatedStockDays(
                mevcutStok: 14m,
                ortalamaGunlukSatis: 2m);

        Assert.NotNull(result);
        Assert.Equal(7m, result.Value);
    }

    /// <summary>
    /// Günlük satış hızı null olduğunda tahmini stok gününün
    /// hesaplanamayacağını test eder.
    /// </summary>
    [Fact]
    public void CalculateEstimatedStockDays_DailySalesMissing_ReturnsNull()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateEstimatedStockDays(
                mevcutStok: 100m,
                ortalamaGunlukSatis: null);

        Assert.Null(result);
    }

    /// <summary>
    /// Günlük satış hızı sıfır olduğunda bölme yapılamayacağı
    /// için null dönmesi gerektiğini test eder.
    /// </summary>
    [Fact]
    public void CalculateEstimatedStockDays_ZeroDailySales_ReturnsNull()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateEstimatedStockDays(
                mevcutStok: 100m,
                ortalamaGunlukSatis: 0m);

        Assert.Null(result);
    }

    /// <summary>
    /// Günlük satış hızı negatif olduğunda da geçerli bir stok günü
    /// hesabı yapılamayacağını test eder.
    /// </summary>
    [Fact]
    public void CalculateEstimatedStockDays_NegativeDailySales_ReturnsNull()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        decimal? result =
            calculator.CalculateEstimatedStockDays(
                mevcutStok: 100m,
                ortalamaGunlukSatis: -2m);

        Assert.Null(result);
    }

    /// <summary>
    /// Mevcut stok negatif olduğunda
    /// ArgumentOutOfRangeException fırlatılması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void CalculateEstimatedStockDays_NegativeStock_ThrowsException()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            calculator.CalculateEstimatedStockDays(
                mevcutStok: -1m,
                ortalamaGunlukSatis: 2m));
    }

    /// <summary>
    /// Tahmini stok günü threshold değerinden küçük olduğunda
    /// ürünün kritik kabul edilmesi gerektiğini test eder.
    ///
    /// Threshold = 7 gün
    /// Stok günü = 5 gün
    ///
    /// 5 < 7 olduğu için kritik olmalıdır.
    /// </summary>
    [Fact]
    public void IsCritical_StockDaysBelowThreshold_ReturnsTrue()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        bool result =
            calculator.IsCritical(
                tahminiStokGunu: 5m);

        Assert.True(result);
    }

    /// <summary>
    /// Tahmini stok günü threshold değerinden büyük olduğunda
    /// ürünün kritik kabul edilmemesi gerektiğini test eder.
    ///
    /// 10 > 7 olduğu için kritik değildir.
    /// </summary>
    [Fact]
    public void IsCritical_StockDaysAboveThreshold_ReturnsFalse()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        bool result =
            calculator.IsCritical(
                tahminiStokGunu: 10m);

        Assert.False(result);
    }

    /// <summary>
    /// Tahmini stok günü tam olarak threshold değerine eşit olduğunda
    /// kritik kabul edilmemesi gerektiğini test eder.
    ///
    /// Kural:
    ///
    /// stok günü < 7
    ///
    /// Dolayısıyla:
    ///
    /// 7 < 7 -> false
    /// </summary>
    [Fact]
    public void IsCritical_StockDaysExactlyAtThreshold_ReturnsFalse()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        bool result =
            calculator.IsCritical(
                tahminiStokGunu: 7m);

        Assert.False(result);
    }

    /// <summary>
    /// Tahmini stok günü null olduğunda ürünün kritik
    /// kabul edilmemesi gerektiğini test eder.
    ///
    /// Satış hızı hesaplanamıyorsa stok günü de bilinmiyor
    /// kabul edilir ve otomatik olarak kritik sayılmaz.
    /// </summary>
    [Fact]
    public void IsCritical_StockDaysMissing_ReturnsFalse()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        bool result =
            calculator.IsCritical(
                tahminiStokGunu: null);

        Assert.False(result);
    }

    /// <summary>
    /// Negatif stok gününün threshold altında olmasına rağmen
    /// mevcut public metoda göre true dönebileceğini test eder.
    ///
    /// Normal uygulama akışında negatif stok günü üretilmemelidir;
    /// çünkü CalculateEstimatedStockDays negatif stok değerini
    /// kabul etmez.
    ///
    /// Bu test IsCritical metodunun tek başına davranışını doğrular.
    /// </summary>
    [Fact]
    public void IsCritical_NegativeStockDays_ReturnsTrue()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        bool result =
            calculator.IsCritical(
                tahminiStokGunu: -1m);

        Assert.True(result);
    }

    /// <summary>
    /// Stok günü threshold'un altında olduğunda
    /// Calculate metodunun STOK_YETERSIZ gerekçesi oluşturduğunu
    /// test eder.
    ///
    /// 90 günlük satış = 90
    /// Günlük satış = 1
    /// Mevcut stok = 5
    ///
    /// Tahmini stok günü = 5
    ///
    /// 5 < 7 olduğu için kritik stoktur.
    /// </summary>
    [Fact]
    public void Calculate_StockDaysBelowThreshold_AddsStockRisk()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: 5m,
            minStokSeviyesi: 0m);

        StockMetricsResult result =
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 90m);

        Assert.Equal("URUN-001", result.UrunKodu);
        Assert.Equal("DEPO-01", result.DepoKodu);

        Assert.Equal(5m, result.MevcutStok);
        Assert.Equal(0m, result.MinStokSeviyesi);
        Assert.Equal(90m, result.SonUcAySatisAdedi);

        Assert.Equal(1m, result.OrtalamaGunlukSatis);
        Assert.Equal(5m, result.TahminiStokGunu);

        Assert.True(result.KritikMi);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Stok günü threshold'un üzerinde olduğunda
    /// STOK_YETERSIZ gerekçesinin oluşmaması gerektiğini test eder.
    ///
    /// 90 günlük satış = 90
    /// Günlük satış = 1
    /// Mevcut stok = 10
    ///
    /// Tahmini stok günü = 10.
    ///
    /// 10 < 7 olmadığı için kritik değildir.
    /// </summary>
    [Fact]
    public void Calculate_StockDaysAboveThreshold_DoesNotAddStockRisk()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: 10m,
            minStokSeviyesi: 0m);

        StockMetricsResult result =
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 90m);

        Assert.Equal(10m, result.MevcutStok);
        Assert.Equal(1m, result.OrtalamaGunlukSatis);
        Assert.Equal(10m, result.TahminiStokGunu);

        Assert.False(result.KritikMi);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Tahmini stok günü tam olarak 7 olduğunda ürünün kritik
    /// kabul edilmemesi gerektiğini Calculate metodu üzerinden test eder.
    /// </summary>
    [Fact]
    public void Calculate_StockDaysExactlyAtThreshold_IsNotCritical()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: 7m,
            minStokSeviyesi: 0m);

        StockMetricsResult result =
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 90m);

        Assert.Equal(7m, result.TahminiStokGunu);
        Assert.False(result.KritikMi);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Mevcut stok minimum stok seviyesinin altındaysa,
    /// stok günü kritik olmasa bile STOK_YETERSIZ gerekçesinin
    /// oluşturulduğunu test eder.
    ///
    /// Mevcut stok = 5
    /// Minimum stok = 10
    ///
    /// Günlük satış = 1
    /// Stok günü = 5
    ///
    /// Bu örnekte hem stok günü hem minimum stok kuralı
    /// aynı gerekçeyi üretmektedir.
    ///
    /// Calculator aynı gerekçeyi iki kez eklememelidir.
    /// </summary>
    [Fact]
    public void Calculate_StockBelowMinimum_AddsStockRiskOnlyOnce()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: 5m,
            minStokSeviyesi: 10m);

        StockMetricsResult result =
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 90m);

        Assert.True(result.KritikMi);

        Assert.Single(
            result.GerekceKodlari,
            "STOK_YETERSIZ");
    }

    /// <summary>
    /// Stok günü threshold üzerinde olsa bile mevcut stok
    /// minimum stok seviyesinin altındaysa ürün kritik kabul edilmeli (KritikMi = true)
    /// ve STOK_YETERSIZ gerekçesi oluşturulmalıdır.
    ///
    /// Mevcut stok = 5
    /// Minimum stok = 10
    ///
    /// Günlük satış = 0.5
    /// Stok günü = 10
    ///
    /// 10 >= 7 olduğu için stok günü açısından kritik değildir.
    /// Ancak mevcut stok minimum seviyenin altında olduğu için
    /// ürün kritik kabul edilmeli (KritikMi = true) ve gerekçe eklenmelidir.
    /// </summary>
    [Fact]
    public void Calculate_StockBelowMinimumEvenWhenStockDaysAreSafe_AddsStockRisk()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: 5m,
            minStokSeviyesi: 10m);

        StockMetricsResult result =
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 45m);

        Assert.Equal(0.5m, result.OrtalamaGunlukSatis);
        Assert.Equal(10m, result.TahminiStokGunu);

        // Düzeltme: Minimum stok seviyesinin altında olduğu için
        // artık KritikMi değeri true dönmelidir.
        Assert.True(result.KritikMi);

        Assert.Contains(
            "STOK_YETERSIZ",
            result.GerekceKodlari);
    }

    /// <summary>
    /// Son üç ayda satış olmadığında ortalama günlük satışın
    /// ve tahmini stok gününün null olması gerektiğini test eder.
    ///
    /// Bu durumda kritik stok gerekçesi oluşturulmaz.
    /// </summary>
    [Fact]
    public void Calculate_NoSales_ReturnsNullStockDaysAndNoStockRisk()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: 100m,
            minStokSeviyesi: 0m);

        StockMetricsResult result =
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 0m);

        Assert.Null(result.OrtalamaGunlukSatis);
        Assert.Null(result.TahminiStokGunu);

        Assert.False(result.KritikMi);

        Assert.Empty(result.GerekceKodlari);
    }

    /// <summary>
    /// MevcutStok null olduğunda Calculate metodunun
    /// InvalidOperationException fırlatması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Calculate_NullMevcutStok_ThrowsInvalidOperationException()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        StokReadModel stock = CreateStock(
            urunKodu: "URUN-001",
            depoKodu: "DEPO-01",
            mevcutStok: null,
            minStokSeviyesi: 10m);

        Assert.Throws<InvalidOperationException>(() =>
            calculator.Calculate(
                stock,
                sonUcAySatisAdedi: 90m));
    }

    /// <summary>
    /// Calculate metodunun null stok modeli kabul etmemesi
    /// ve ArgumentNullException fırlatması gerektiğini test eder.
    /// </summary>
    [Fact]
    public void Calculate_NullStock_ThrowsArgumentNullException()
    {
        StockMetricsCalculator calculator =
            CreateCalculator();

        Assert.Throws<ArgumentNullException>(() =>
            calculator.Calculate(
                stock: null!,
                sonUcAySatisAdedi: 90m));
    }

    /// <summary>
    /// Calculator oluşturulurken negatif stok gün threshold'u
    /// verilirse ArgumentOutOfRangeException fırlatılması gerektiğini
    /// test eder.
    /// </summary>
    [Fact]
    public void Constructor_NegativeThreshold_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StockMetricsCalculator(
                stokGunEsigi: -1m));
    }

    /// <summary>
    /// Stok gün threshold'u sıfır olduğunda constructor'ın
    /// bunu kabul ettiğini test eder.
    ///
    /// Sıfır negatif olmadığı için geçerli bir configuration değeridir.
    /// </summary>
    [Fact]
    public void Constructor_ZeroThreshold_DoesNotThrow()
    {
        StockMetricsCalculator calculator =
            new(0m);

        Assert.NotNull(calculator);
    }

    /// <summary>
    /// Testlerde kullanılacak StokReadModel nesnesini oluşturur.
    ///
    /// Test verisinin gerçek DataAccess bağlantısına ihtiyacı yoktur.
    /// </summary>
    private static StokReadModel CreateStock(
        string urunKodu,
        string depoKodu,
        decimal? mevcutStok,
        decimal? minStokSeviyesi)
    {
        return new StokReadModel(
            UrunKodu: urunKodu,
            DepoKodu: depoKodu,
            MevcutStok: mevcutStok,
            MinStokSeviyesi: minStokSeviyesi,
            SonSayimTarihi: null);
    }
}
