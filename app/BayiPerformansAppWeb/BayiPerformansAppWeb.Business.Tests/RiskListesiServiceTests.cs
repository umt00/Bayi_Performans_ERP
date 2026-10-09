using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Business.Tests.Services;

/// <summary>
/// RiskListesiService davranış testleridir.
///
/// Bu test sınıfında özellikle:
/// - dönem belirleme,
/// - bayi filtreleri,
/// - satış/metrik hesaplama,
/// - hedef hesaplama,
/// - nullable değerlerin korunması,
/// - kritik stok sayısının gerçek stok verisinden hesaplanması,
/// - risk skorunun oluşturulması,
/// - deterministik sıralama
/// test edilir.
///
/// Herhangi bir gerçek veritabanına erişilmez.
/// Repository'ler test içerisinde basit fake sınıflarla sağlanır.
/// </summary>
public sealed class RiskListesiServiceTests
{
    /// <summary>
    /// Belirli bir dönem verildiğinde servisin o dönemi kullanması gerekir.
    ///
    /// Örneğin:
    /// 2025-05-18 verilirse
    ///
    /// periodStart = 2025-05-01
    /// periodEndExclusive = 2025-06-01
    ///
    /// olmalıdır.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_DonemVerildiginde_DogruDonemiKullanir()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository(
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 5, 10),
                    "B001",
                    "U001",
                    "D001",
                    10m,
                    100m,
                    0.10m,
                    "MAGAZA",
                    false));

        var hedefRepository =
            new FakeAylikHedefReadRepository(
                new AylikHedefReadModel(
                    "B001",
                    2025,
                    5,
                    1000m));

        var urunRepository =
            new FakeUrunReadRepository(
                new UrunReadModel(
                    "U001",
                    "Kategori",
                    "AltKategori",
                    50m));

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 18));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Servis verilen tarihin bulunduğu ayın ilk gününü dönem olarak kullanmalıdır.
        Assert.Equal(
            new DateOnly(2025, 5, 1),
            result.Value!.Donem);

        // Satış repository'sine gönderilen dönem başlangıcı kontrol edilir.
        Assert.Equal(
            new DateOnly(2025, 5, 1),
            satisRepository.LastPeriodStart);

        // Dönem sonu exclusive olarak bir sonraki ayın ilk günü olmalıdır.
        Assert.Equal(
            new DateOnly(2025, 6, 1),
            satisRepository.LastPeriodEndExclusive);
    }

    /// <summary>
    /// Dönem verilmediğinde servis son satış tarihini okuyup
    /// onun bulunduğu ayı kullanmalıdır.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_DonemVerilmediginde_SonSatisTarihininAyiniKullanir()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository(
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 8, 20),
                    "B001",
                    "U001",
                    "D001",
                    5m,
                    100m,
                    0m,
                    "MAGAZA",
                    false));

        // Fake repository'nin son satış tarihini açıkça belirliyoruz.
        satisRepository.LatestSaleDate =
            new DateOnly(2025, 8, 20);

        var hedefRepository =
            new FakeAylikHedefReadRepository();

        var urunRepository =
            new FakeUrunReadRepository();

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: null);

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Son satış tarihi Ağustos 2025 olduğundan dönem 2025-08 olmalıdır.
        Assert.Equal(
            new DateOnly(2025, 8, 1),
            result.Value!.Donem);

        Assert.Equal(
            new DateOnly(2025, 8, 1),
            satisRepository.LastPeriodStart);

        Assert.Equal(
            new DateOnly(2025, 9, 1),
            satisRepository.LastPeriodEndExclusive);
    }

    /// <summary>
    /// Dönem verilmediğinde son satış tarihi de bulunamazsa
    /// servis SATIS_VERISI_YOK hatası döndürmelidir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_SonSatisTarihiYoksa_NotFoundDoner()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository();

        var satisRepository =
            new FakeSatisReadRepository();

        // Son satış tarihi bulunamadığını açıkça belirtiyoruz.
        satisRepository.LatestSaleDate = null;

        var hedefRepository =
            new FakeAylikHedefReadRepository();

        var urunRepository =
            new FakeUrunReadRepository();

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: null);

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);

        Assert.Equal(
            ServiceErrorKind.NotFound,
            result.ErrorKind);

        Assert.Equal(
            "SATIS_VERISI_YOK",
            result.ErrorCode);

        Assert.Null(result.Value);
    }

    /// <summary>
    /// Segment ve bölge filtrelerinin bayi repository'sine
    /// aynen aktarılması gerekir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_FiltreleriBayiRepositorysineIletir()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository();

        var satisRepository =
            new FakeSatisReadRepository();

        var hedefRepository =
            new FakeAylikHedefReadRepository();

        var urunRepository =
            new FakeUrunReadRepository();

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: "A",
                Bolge: "Marmara",
                Donem: new DateOnly(2025, 5, 1));

        // Act
        await service.ListeleAsync(
            query,
            CancellationToken.None);

        // Assert
        Assert.Equal(
            "A",
            bayiRepository.LastSegment);

        Assert.Equal(
            "Marmara",
            bayiRepository.LastBolge);
    }

    /// <summary>
    /// Filtre sonucunda hiç bayi bulunmazsa bu durum hata değildir.
    /// Servis başarılı ve boş liste döndürmelidir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_BayiYoksa_BasariliBosListeDoner()
    {
        // Arrange
        var service =
            CreateService(
                new FakeBayiReadRepository(),
                new FakeSatisReadRepository(),
                new FakeAylikHedefReadRepository(),
                new FakeUrunReadRepository(),
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: "A",
                Bolge: "Marmara",
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value!.Bayiler);
    }

    /// <summary>
    /// Normal bir satış için:
    /// - gerçekleşen ciro,
    /// - hedef gerçekleşme oranı,
    /// - marj,
    /// - iskonto
    /// gibi metriklerin doğru hesaplanması kontrol edilir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_SatisVeHedefMetrikleriniDogruHesaplar()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository(
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 5, 10),
                    "B001",
                    "U001",
                    "D001",
                    10m,
                    100m,
                    0.10m,
                    "MAGAZA",
                    false));

        var hedefRepository =
            new FakeAylikHedefReadRepository(
                new AylikHedefReadModel(
                    "B001",
                    2025,
                    5,
                    1000m));

        var urunRepository =
            new FakeUrunReadRepository(
                new UrunReadModel(
                    "U001",
                    "Kategori",
                    "AltKategori",
                    50m));

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var item =
            Assert.Single(
                result.Value!.Bayiler);

        // 10 × 100 × (1 - 0.10) = 900
        //
        // Hedef = 1000
        //
        // Gerçekleşme = 900 / 1000 = 0.90
        Assert.Equal(
            0.90m,
            item.HedefeUlasmaPct);

        // İndirimli fiyat = 100 × (1 - 0.10) = 90
        //
        // Maliyet = 50
        //
        // Marj = (90 - 50) / 90 = 40 / 90
        Assert.Equal(
            40m / 90m,
            item.OrtalamaMarjPct);

        // Ortalama iskonto %10 olmalıdır.
        Assert.Equal(
            0.10m,
            item.OrtalamaIskontoPct);
    }

    /// <summary>
    /// Miktar, birim fiyat veya iskonto oranından biri null ise
    /// ilgili satış satırı hesaplamaya dahil edilmemelidir.
    ///
    /// Bu testte dört satış bulunmaktadır:
    ///
    /// 1. Geçerli satış
    /// 2. Miktarı null olan satış
    /// 3. Birim fiyatı null olan satış
    /// 4. İskonto oranı null olan satış
    ///
    /// Sadece birinci satış hesaplamaya dahil edilmelidir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_EksikZorunluSatisAlanlarini_Atlar()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository(
                // ------------------------------------------------
                // Geçerli satış
                // ------------------------------------------------
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 5, 10),
                    "B001",
                    "U001",
                    "D001",
                    10m,
                    100m,
                    0m,
                    "MAGAZA",
                    false),

                // ------------------------------------------------
                // Miktar eksik
                // ------------------------------------------------
                new SatisReadModel(
                    2,
                    new DateOnly(2025, 5, 11),
                    "B001",
                    "U002",
                    "D001",
                    null,
                    100m,
                    0m,
                    "MAGAZA",
                    false),

                // ------------------------------------------------
                // Birim fiyat eksik
                // ------------------------------------------------
                new SatisReadModel(
                    3,
                    new DateOnly(2025, 5, 12),
                    "B001",
                    "U003",
                    "D001",
                    10m,
                    null,
                    0m,
                    "MAGAZA",
                    false),

                // ------------------------------------------------
                // İskonto oranı eksik
                // ------------------------------------------------
                new SatisReadModel(
                    4,
                    new DateOnly(2025, 5, 13),
                    "B001",
                    "U004",
                    "D001",
                    10m,
                    100m,
                    null,
                    "MAGAZA",
                    false));

        // --------------------------------------------------------
        // ÖNEMLİ:
        //
        // Burada hedef veriyoruz.
        //
        // Bunun amacı, yalnızca geçerli satışın hesaplandığını
        // HedefeUlasmaPct üzerinden doğrulayabilmek.
        // --------------------------------------------------------
        var hedefRepository =
            new FakeAylikHedefReadRepository(
                new AylikHedefReadModel(
                    "B001",
                    2025,
                    5,
                    1000m));

        // Sadece geçerli satışın ürünü için maliyet tanımlıyoruz.
        var urunRepository =
            new FakeUrunReadRepository(
                new UrunReadModel(
                    "U001",
                    "Kategori",
                    "AltKategori",
                    50m));

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var item =
            Assert.Single(
                result.Value!.Bayiler);

        // --------------------------------------------------------
        // SADECE GEÇERLİ SATIŞ HESAPLANIR
        // --------------------------------------------------------
        //
        // Geçerli satış:
        //
        // Miktar      = 10
        // Birim fiyat = 100
        // İskonto     = 0
        //
        // Ciro:
        //
        // 10 × 100 × (1 - 0) = 1000
        //
        // Hedef:
        //
        // 1000
        //
        // Hedef gerçekleşme:
        //
        // 1000 / 1000 = 1.00
        //
        // Diğer üç satışın zorunlu alanlarından biri null olduğu
        // için RiskListesiService tarafından salesLines listesine
        // eklenmemektedir.
        // --------------------------------------------------------
        Assert.Equal(
            1.00m,
            item.HedefeUlasmaPct);
    }

    /// <summary>
    /// Hedef bulunmadığında HedefeUlasmaPct null kalmalıdır.
    ///
    /// Burada null değer 0'a çevrilmemelidir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_HedefYoksa_HedefGerceklesmeNullKalir()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository(
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 5, 10),
                    "B001",
                    "U001",
                    "D001",
                    10m,
                    100m,
                    0m,
                    "MAGAZA",
                    false));

        // Hedef repository boş bırakılıyor.
        var hedefRepository =
            new FakeAylikHedefReadRepository();

        var urunRepository =
            new FakeUrunReadRepository();

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var item =
            Assert.Single(
                result.Value!.Bayiler);

        // Hedef olmadığı için hedef gerçekleşme oranı null kalmalıdır.
        Assert.Null(
            item.HedefeUlasmaPct);
    }

    /// <summary>
    /// Stok kaydı bulunmayan ürün/depo kombinasyonu kritik kabul edilmez.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_StokKaydiYoksa_StokRiskiOlusturmaz()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository();

        var hedefRepository =
            new FakeAylikHedefReadRepository();

        var urunRepository =
            new FakeUrunReadRepository();

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var item =
            Assert.Single(
                result.Value!.Bayiler);

        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            item.GerekceKodlari);
    }

    [Fact]
    public async Task ListeleAsync_KritikStokVarsa_StokRiskiniHesabaKatar()
    {
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel("B001", "A", "Marmara"));
        var satisRepository =
            new FakeSatisReadRepository(
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 5, 10),
                    "B001",
                    "U001",
                    "D001",
                    1m,
                    100m,
                    0m,
                    "MAGAZA",
                    false));
        var stokRepository =
            new RiskStokReadRepository(
                new StokReadModel(
                "U001",
                "D001",
                MevcutStok: 1m,
                MinStokSeviyesi: 10m,
                SonSayimTarihi: null));

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                new FakeAylikHedefReadRepository(),
                new FakeUrunReadRepository(),
                CreateOptions(),
                stokRepository);

        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                new RiskListesiQuery(
                    Segment: null,
                    Bolge: null,
                    Donem: new DateOnly(2025, 5, 1)),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Bayiler);
        Assert.Contains("STOK_YETERSIZ", item.GerekceKodlari);
        Assert.True(item.RiskSkoru >= 50);
    }

    /// <summary>
    /// Risk skoru büyük olan bayi önce gelmelidir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_RiskSkorunu_AzalanSiradaSiralar()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"),
                new BayiReadModel(
                    "B002",
                    "A",
                    "Marmara"));

        var satisRepository =
            new FakeSatisReadRepository(
                // B001 -> düşük hedef gerçekleşmesi
                new SatisReadModel(
                    1,
                    new DateOnly(2025, 5, 10),
                    "B001",
                    "U001",
                    "D001",
                    5m,
                    100m,
                    0m,
                    "MAGAZA",
                    false),

                // B002 -> daha yüksek hedef gerçekleşmesi
                new SatisReadModel(
                    2,
                    new DateOnly(2025, 5, 10),
                    "B002",
                    "U001",
                    "D001",
                    10m,
                    100m,
                    0m,
                    "MAGAZA",
                    false));

        var hedefRepository =
            new FakeAylikHedefReadRepository(
                new AylikHedefReadModel(
                    "B001",
                    2025,
                    5,
                    1000m),
                new AylikHedefReadModel(
                    "B002",
                    2025,
                    5,
                    1000m));

        var urunRepository =
            new FakeUrunReadRepository(
                new UrunReadModel(
                    "U001",
                    "Kategori",
                    "AltKategori",
                    50m));

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(
            2,
            result.Value!.Bayiler.Count);

        // B001:
        // 5 × 100 = 500
        // Hedef = 1000
        // Gerçekleşme = %50
        //
        // B002:
        // 10 × 100 = 1000
        // Hedef = 1000
        // Gerçekleşme = %100
        //
        // Dolayısıyla B001 daha yüksek risk taşımalıdır.
        Assert.Equal(
            "B001",
            result.Value.Bayiler[0].BayiKodu);

        Assert.Equal(
            "B002",
            result.Value.Bayiler[1].BayiKodu);
    }

    /// <summary>
    /// Risk skorları eşitse bayi koduna göre
    /// alfabetik/ordinal sıralama yapılmalıdır.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_EsRiskSkorunda_BayiKodunaGoreSiralar()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B002",
                    "A",
                    "Marmara"),
                new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara"));

        // Satış olmadığı için her iki bayi de aynı risk skoruna sahip olacaktır.
        var satisRepository =
            new FakeSatisReadRepository();

        var hedefRepository =
            new FakeAylikHedefReadRepository();

        var urunRepository =
            new FakeUrunReadRepository();

        var service =
            CreateService(
                bayiRepository,
                satisRepository,
                hedefRepository,
                urunRepository,
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Risk skorları eşit olduğunda ordinal bayi kodu sıralaması uygulanmalıdır.
        Assert.Equal(
            "B001",
            result.Value.Bayiler[0].BayiKodu);

        Assert.Equal(
            "B002",
            result.Value.Bayiler[1].BayiKodu);
    }

    /// <summary>
    /// Bayinin nullable Segment ve Bolge alanları null olduğunda
    /// ServiceOutput içerisinde boş string olarak tutulmalıdır.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_NullableBayiAlanlarini_BosStringeDonusturur()
    {
        // Arrange
        var bayiRepository =
            new FakeBayiReadRepository(
                new BayiReadModel(
                    "B001",
                    null,
                    null));

        var service =
            CreateService(
                bayiRepository,
                new FakeSatisReadRepository(),
                new FakeAylikHedefReadRepository(),
                new FakeUrunReadRepository(),
                CreateOptions());

        var query =
            new RiskListesiQuery(
                Segment: null,
                Bolge: null,
                Donem: new DateOnly(2025, 5, 1));

        // Act
        ServiceResult<Entities.ServiceOutputs.RiskListesiOutput> result =
            await service.ListeleAsync(
                query,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var item =
            Assert.Single(
                result.Value!.Bayiler);

        // Service tarafındaki ?? string.Empty davranışı test edilir.
        Assert.Equal(
            string.Empty,
            item.Segment);

        Assert.Equal(
            string.Empty,
            item.Bolge);
    }

    /// <summary>
    /// Null query gönderilmesi durumunda ArgumentNullException
    /// beklenir.
    /// </summary>
    [Fact]
    public async Task ListeleAsync_QueryNullsa_ArgumentNullExceptionFirlattirir()
    {
        // Arrange
        var service =
            CreateService(
                new FakeBayiReadRepository(),
                new FakeSatisReadRepository(),
                new FakeAylikHedefReadRepository(),
                new FakeUrunReadRepository(),
                CreateOptions());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () =>
                service.ListeleAsync(
                    null!,
                    CancellationToken.None));
    }

    /// <summary>
    /// Testlerde kullanılacak standart business rule
    /// configuration değerlerini oluşturur.
    /// </summary>
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

            RiskWeights =
                new Dictionary<string, int>
                {
                    ["MARJ_ESIK_ALTI"] = 40,
                    ["HEDEF_ALTINDA"] = 30,
                    ["YUKSEK_ISKONTO"] = 20,
                    ["STOK_YETERSIZ"] = 50,
                    ["YUKSEK_IADE"] = 25
                },

            RiskLevels =
                new Dictionary<string, int>
                {
                    ["DUSUK_max"] = 30,
                    ["ORTA_max"] = 60,
                    ["YUKSEK_max"] = 900
                }
        };
    }

    /// <summary>
    /// RiskListesiService nesnesini ortak şekilde oluşturur.
    ///
    /// Servisin constructor'ı artık stok verisini de kullandığı için
    /// IStokReadRepository ve StockMetricsCalculator burada oluşturulur.
    ///
    /// Gerçek repository yerine test fake'leri kullanılır.
    /// Böylece testlerin veritabanına veya dış servislere
    /// bağımlılığı olmaz.
    /// </summary>
    private static RiskListesiService CreateService(
        IBayiReadRepository bayiRepository,
        ISatisReadRepository satisRepository,
        IAylikHedefReadRepository hedefRepository,
        IUrunReadRepository urunRepository,
        BusinessRulesOptions options,
        IStokReadRepository? stokRepository = null)
    {
        // Satış metriklerini hesaplayan ortak calculator.
        var salesMetricsCalculator =
            new SalesMetricsCalculator();

        // Stok metriklerini hesaplayan yeni calculator.
        // StockMetricsCalculator stok gün eşiğini constructor üzerinden ister.
        // Test ayarlarında tanımlanan 7 günlük eşiği kullanıyoruz.
        var stockMetricsCalculator =
            new StockMetricsCalculator(
                options.StokGunEsigi);

        // Risk skorunu hesaplayan engine.
        var riskEngine =
            new RiskEngine(options);

        // ------------------------------------------------------------
        // ÖNEMLİ:
        //
        // RiskListesiService artık IStokReadRepository de bekliyor.
        //
        // Testlerde stok davranışını ayrıca test etmiyorsak,
        // boş bir fake repository yeterlidir.
        // ------------------------------------------------------------
        stokRepository ??= new RiskStokReadRepository();

        return new RiskListesiService(
            bayiRepository,
            satisRepository,
            hedefRepository,
            urunRepository,
            stokRepository,
            salesMetricsCalculator,
            stockMetricsCalculator,
            riskEngine);
    }

    // ============================================================
    // FAKE REPOSITORY'LER
    // ============================================================

    private sealed class RiskStokReadRepository : IStokReadRepository
    {
        private readonly IReadOnlyList<StokReadModel> _stoklar;

        public RiskStokReadRepository(params StokReadModel[] stoklar)
        {
            _stoklar = stoklar;
        }

        public Task<IReadOnlyList<StokReadModel>> ListAsync(
            string? depoKodu,
            string? urunKodu,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<StokReadModel> result = _stoklar
                .Where(stok =>
                    (depoKodu is null || stok.DepoKodu == depoKodu)
                    && (urunKodu is null || stok.UrunKodu == urunKodu))
                .ToArray();

            return Task.FromResult(result);
        }

        public Task<StokReadModel?> GetAsync(
            string urunKodu,
            string depoKodu,
            CancellationToken cancellationToken)
        {
            StokReadModel? result = _stoklar.SingleOrDefault(stok =>
                stok.UrunKodu == urunKodu
                && stok.DepoKodu == depoKodu);

            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Testlerde kullanılan sahte bayi repository'sidir.
    ///
    /// Gerçek veritabanı yerine bellekteki liste üzerinden
    /// sonuç üretir.
    /// </summary>
    private sealed class FakeBayiReadRepository
        : IBayiReadRepository
    {
        private readonly IReadOnlyList<BayiReadModel> _bayiler;

        /// <summary>
        /// Servise gönderilen son segment filtresini tutar.
        /// </summary>
        public string? LastSegment
        {
            get;
            private set;
        }

        /// <summary>
        /// Servise gönderilen son bölge filtresini tutar.
        /// </summary>
        public string? LastBolge
        {
            get;
            private set;
        }

        public FakeBayiReadRepository(
            params BayiReadModel[] bayiler)
        {
            _bayiler = bayiler;
        }

        /// <summary>
        /// Belirli bayi koduna göre bayi getirir.
        /// </summary>
        public Task<BayiReadModel?> GetByCodeAsync(
            string bayiKodu,
            CancellationToken cancellationToken)
        {
            BayiReadModel? bayi =
                _bayiler.FirstOrDefault(
                    x => x.BayiKodu == bayiKodu);

            return Task.FromResult(bayi);
        }

        /// <summary>
        /// Segment ve bölge filtrelerini uygular.
        ///
        /// Ayrıca testlerin filtrelerin gerçekten repository'ye
        /// iletildiğini doğrulayabilmesi için son değerleri kaydeder.
        /// </summary>
        public Task<IReadOnlyList<BayiReadModel>> ListAsync(
            string? segment,
            string? bolge,
            CancellationToken cancellationToken)
        {
            LastSegment = segment;
            LastBolge = bolge;

            IEnumerable<BayiReadModel> result =
                _bayiler;

            if (segment is not null)
            {
                result =
                    result.Where(
                        x => x.Segment == segment);
            }

            if (bolge is not null)
            {
                result =
                    result.Where(
                        x => x.Bolge == bolge);
            }

            return Task.FromResult<IReadOnlyList<BayiReadModel>>(
                result.ToList());
        }
    }

    /// <summary>
    /// Testlerde kullanılan sahte satış repository'sidir.
    ///
    /// Dönem filtrelerini bellekteki satış listesi üzerinde uygular.
    /// </summary>
    private sealed class FakeSatisReadRepository
        : ISatisReadRepository
    {
        private readonly IReadOnlyList<SatisReadModel> _satislar;

        /// <summary>
        /// GetLatestSaleDateAsync tarafından döndürülecek son satış tarihi.
        /// </summary>
        public DateOnly? LatestSaleDate
        {
            get;
            set;
        }

        /// <summary>
        /// ListByPeriodAsync metoduna gönderilen son dönem başlangıcı.
        /// </summary>
        public DateOnly? LastPeriodStart
        {
            get;
            private set;
        }

        /// <summary>
        /// ListByPeriodAsync metoduna gönderilen dönem sonu.
        /// </summary>
        public DateOnly? LastPeriodEndExclusive
        {
            get;
            private set;
        }

        public FakeSatisReadRepository(
            params SatisReadModel[] satislar)
        {
            _satislar = satislar;

            // Satış varsa otomatik olarak en son satış tarihini bul.
            // Test isterse daha sonra LatestSaleDate değerini değiştirebilir.
            LatestSaleDate =
                satislar.Length == 0
                    ? null
                    : satislar.Max(
                        x => x.Tarih);
        }

        /// <summary>
        /// Sistemdeki son satış tarihini döndürür.
        /// </summary>
        public Task<DateOnly?> GetLatestSaleDateAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                LatestSaleDate);
        }

        /// <summary>
        /// Belirli dönem içerisindeki satışları döndürür.
        ///
        /// Dönem başlangıcı inclusive,
        /// dönem sonu exclusive'dir.
        /// </summary>
        public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
        {
            LastPeriodStart = periodStart;
            LastPeriodEndExclusive = periodEndExclusive;

            IReadOnlyList<SatisReadModel> result =
                _satislar
                    .Where(
                        x =>
                            x.Tarih >= periodStart &&
                            x.Tarih < periodEndExclusive)
                    .ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Belirli bayi ve dönem içerisindeki satışları döndürür.
        ///
        /// Bu servis şu an toplu ListByPeriodAsync kullandığı için
        /// testlerde doğrudan kullanılmasa da interface sözleşmesini
        /// karşılamak için uygulanmıştır.
        /// </summary>
        public Task<IReadOnlyList<SatisReadModel>>
            ListByDealerAndPeriodAsync(
                string bayiKodu,
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                CancellationToken cancellationToken)
        {
            IReadOnlyList<SatisReadModel> result =
                _satislar
                    .Where(
                        x =>
                            x.BayiKodu == bayiKodu &&
                            x.Tarih >= periodStart &&
                            x.Tarih < periodEndExclusive)
                    .ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Belirli ürün, depo ve dönem içerisindeki satışları döndürür.
        ///
        /// Servis tarafından kullanılmasa da interface sözleşmesi
        /// gereği fake repository içerisinde uygulanmıştır.
        /// </summary>
        public Task<IReadOnlyList<SatisReadModel>>
            ListByProductWarehouseAndPeriodAsync(
                string urunKodu,
                string depoKodu,
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                CancellationToken cancellationToken)
        {
            IReadOnlyList<SatisReadModel> result =
                _satislar
                    .Where(
                        x =>
                            x.UrunKodu == urunKodu &&
                            x.DepoKodu == depoKodu &&
                            x.Tarih >= periodStart &&
                            x.Tarih < periodEndExclusive)
                    .ToList();

            return Task.FromResult(result);
        }

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
            GetTotalSalesQuantitiesByPeriodAsync(
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Aggregate(periodStart, periodEndExclusive, bayiKodu: null));
        }

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
            GetTotalSalesQuantitiesForDealerAsync(
                string bayiKodu,
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Aggregate(periodStart, periodEndExclusive, bayiKodu));
        }

        private IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>
            Aggregate(
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                string? bayiKodu)
        {
            return _satislar
                .Where(sale =>
                    sale.Tarih >= periodStart
                    && sale.Tarih < periodEndExclusive
                    && (bayiKodu is null || sale.BayiKodu == bayiKodu)
                    && sale.Miktar.HasValue)
                .GroupBy(sale => (sale.UrunKodu, sale.DepoKodu))
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(sale =>
                        sale.IsIade == true
                            ? -Math.Abs(sale.Miktar!.Value)
                            : sale.Miktar!.Value));
        }

        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<DateOnly> result = _satislar
                .Select(s => new DateOnly(s.Tarih.Year, s.Tarih.Month, 1))
                .Distinct()
                .OrderBy(d => d)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Testlerde kullanılan sahte aylık hedef repository'sidir.
    /// </summary>
    private sealed class FakeAylikHedefReadRepository
        : IAylikHedefReadRepository
    {
        private readonly IReadOnlyList<AylikHedefReadModel> _hedefler;

        public FakeAylikHedefReadRepository(
            params AylikHedefReadModel[] hedefler)
        {
            _hedefler = hedefler;
        }

        public Task<AylikHedefReadModel?> GetAsync(
            string bayiKodu,
            int yil,
            int ay,
            CancellationToken cancellationToken)
        {
            AylikHedefReadModel? hedef =
                _hedefler.FirstOrDefault(
                    x =>
                        x.BayiKodu == bayiKodu &&
                        x.Yil == yil &&
                        x.Ay == ay);

            return Task.FromResult(hedef);
        }

        public Task<IReadOnlyList<AylikHedefReadModel>>
            ListByPeriodAsync(
                int yil,
                int ay,
                CancellationToken cancellationToken)
        {
            IReadOnlyList<AylikHedefReadModel> result =
                _hedefler
                    .Where(
                        x =>
                            x.Yil == yil &&
                            x.Ay == ay)
                    .ToList();

            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Testlerde kullanılan sahte ürün repository'sidir.
    /// </summary>
    private sealed class FakeUrunReadRepository
        : IUrunReadRepository
    {
        private readonly IReadOnlyList<UrunReadModel> _urunler;

        public FakeUrunReadRepository(
            params UrunReadModel[] urunler)
        {
            _urunler = urunler;
        }

        public Task<UrunReadModel?> GetByCodeAsync(
            string urunKodu,
            CancellationToken cancellationToken)
        {
            UrunReadModel? urun =
                _urunler.FirstOrDefault(
                    x => x.UrunKodu == urunKodu);

            return Task.FromResult(urun);
        }

        public Task<IReadOnlyList<UrunReadModel>>
            ListByCodesAsync(
                IReadOnlyCollection<string> urunKodlari,
                CancellationToken cancellationToken)
        {
            IReadOnlyList<UrunReadModel> result =
                _urunler
                    .Where(
                        x =>
                            urunKodlari.Contains(
                                x.UrunKodu,
                                StringComparer.Ordinal))
                    .ToList();

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_urunler);
        }
    }
}
