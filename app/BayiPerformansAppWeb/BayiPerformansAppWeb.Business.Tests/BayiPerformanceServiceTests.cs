using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Tests.Business.Services;

public sealed class BayiPerformansServiceTests
{
    // ------------------------------------------------------------
    // 1. Geçersiz bayi kodu
    // ------------------------------------------------------------
    // Bayi kodu boş veya sadece boşluklardan oluşuyorsa
    // servisin repository'ye gitmeden Validation dönmesini bekleriz.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetirAsync_GecersizBayiKodu_Verildiginde_ValidationDoner(
        string bayiKodu)
    {
        // Arrange
        FakeBayiReadRepository bayiRepository = new();

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                bayiKodu,
                new DateOnly(2026, 1, 1),
                CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(
            ServiceErrorKind.Validation,
            result.ErrorKind);

        Assert.Equal(
            "GECERSIZ_BAYI_KODU",
            result.ErrorCode);

        // Geçersiz kodda repository çağrılmamalıdır.
        Assert.False(bayiRepository.GetByCodeCalled);
    }

    // ------------------------------------------------------------
    // 2. Bayi bulunamadı
    // ------------------------------------------------------------
    // Geçerli bir bayi kodu verildiğinde ancak repository bayi
    // bulamadığında NotFound dönmesini bekleriz.
    [Fact]
    public async Task GetirAsync_BayiBulunamazsa_NotFoundDoner()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository = new();

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                new DateOnly(2026, 1, 1),
                CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);

        Assert.Equal(
            ServiceErrorKind.NotFound,
            result.ErrorKind);

        Assert.Equal(
            "BAYI_BULUNAMADI",
            result.ErrorCode);

        Assert.Equal(
            "B001",
            bayiRepository.LastBayiKodu);
    }

    // ------------------------------------------------------------
    // 3. Dönem verilmediğinde son satış tarihi kullanılır.
    // ------------------------------------------------------------
    [Fact]
    public async Task GetirAsync_DonemVerilmezse_SonSatisTarihiniKullanir()
    {
        // Arrange
        DateOnly latestSaleDate =
            new(2026, 3, 18);

        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new()
            {
                LatestSaleDate = latestSaleDate
            };

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                null,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Son satış tarihi 18 Mart olsa da dönem tarihi
        // ayın ilk günü olarak output'a yazılmalıdır.
        Assert.Equal(
            new DateOnly(2026, 3, 1),
            result.Value!.Donem);

        Assert.True(
            satisRepository.GetLatestSaleDateCalled);
    }

    // ------------------------------------------------------------
    // 4. Dönem verilmediğinde son satış tarihi de yoksa
    // ------------------------------------------------------------
    [Fact]
    public async Task GetirAsync_DonemVerilmezVeSonSatisTarihiYoksa_NotFoundDoner()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new()
            {
                LatestSaleDate = null
            };

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                null,
                CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);

        Assert.Equal(
            ServiceErrorKind.NotFound,
            result.ErrorKind);

        Assert.Equal(
            "SATIS_VERISI_YOK",
            result.ErrorCode);
    }

    // ------------------------------------------------------------
    // 5. Verilen dönem doğru period aralığına çevrilir.
    // ------------------------------------------------------------
    // Örneğin:
    //
    // 15 Şubat 2026
    //
    // periodStart:
    // 01.02.2026
    //
    // periodEndExclusive:
    // 01.03.2026
    //
    // olmalıdır.
    [Fact]
    public async Task GetirAsync_DonemVerildiginde_DogruPeriodGonderir()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new();

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository);

        DateOnly requestedDate =
            new(2026, 2, 15);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                requestedDate,
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        Assert.Equal(
            new DateOnly(2026, 2, 1),
            satisRepository.LastPeriodStart);

        Assert.Equal(
            new DateOnly(2026, 3, 1),
            satisRepository.LastPeriodEndExclusive);
    }

    // ------------------------------------------------------------
    // 6. Hedef bulunmuyorsa nullable olarak korunur.
    // ------------------------------------------------------------
    [Fact]
    public async Task GetirAsync_HedefYoksa_HedefAlanlariNullKalir()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new();

        FakeAylikHedefReadRepository hedefRepository =
            new()
            {
                Result = null
            };

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository,
                hedefRepository: hedefRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                new DateOnly(2026, 2, 15),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Hedef bulunmadığında null korunmalıdır.
        Assert.Null(result.Value!.HedefCiro);

        // Hedef olmadığı için hedef gerçekleşme oranı da
        // hesaplanmamalı ve null kalmalıdır.
        Assert.Null(result.Value.HedefeUlasmaPct);
    }

    // ------------------------------------------------------------
    // 7. Eksik satış alanları hesaplamaya dahil edilmez.
    // ------------------------------------------------------------
    // Servis:
    //
    // Miktar yok
    // VEYA
    // Birim fiyat yok
    // VEYA
    // İskonto yok
    //
    // olan satışları SalesMetricLine'a çevirmemektedir.
    //
    // Bunu doğrudan SalesMetricsCalculator üzerinden değil,
    // ortaya çıkan ciro üzerinden davranışsal olarak test ediyoruz.
    [Fact]
    public async Task GetirAsync_EksikSatisAlanlarini_HesabaKatmaz()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new()
            {
                Sales =
                [
                    // Bu kayıt geçerli:
                    // 10 * 100 * (1 - 0.10) = 900
                    new SatisReadModel(
                        SatisId: 1,
                        Tarih: new DateOnly(2026, 2, 10),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: 10,
                        BirimFiyat: 100,
                        IskontoOrani: 0.10m,
                        SatisKanali: null,
                        IsIade: false),

                    // Miktar eksik -> hesaplanmamalı.
                    new SatisReadModel(
                        SatisId: 2,
                        Tarih: new DateOnly(2026, 2, 11),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: null,
                        BirimFiyat: 100,
                        IskontoOrani: 0.10m,
                        SatisKanali: null,
                        IsIade: false),

                    // Birim fiyat eksik -> hesaplanmamalı.
                    new SatisReadModel(
                        SatisId: 3,
                        Tarih: new DateOnly(2026, 2, 12),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: 10,
                        BirimFiyat: null,
                        IskontoOrani: 0.10m,
                        SatisKanali: null,
                        IsIade: false),

                    // İskonto eksik -> hesaplanmamalı.
                    new SatisReadModel(
                        SatisId: 4,
                        Tarih: new DateOnly(2026, 2, 13),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: 10,
                        BirimFiyat: 100,
                        IskontoOrani: null,
                        SatisKanali: null,
                        IsIade: false)
                ]
            };

        FakeUrunReadRepository urunRepository =
            new()
            {
                Results =
                [
                    new UrunReadModel(
                        "U001",
                        "Kategori",
                        "AltKategori",
                        50)
                ]
            };

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository,
                urunRepository: urunRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                new DateOnly(2026, 2, 15),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        // Sadece ilk kayıt hesaplanmalıdır.
        Assert.Equal(
            900m,
            result.Value!.GerceklesenCiro);
    }

    // ------------------------------------------------------------
    // 8. Kritik stok yoksa kritik stok sayısı 0 olur.
    // ------------------------------------------------------------
    // Bu test özellikle:
    //
    // kritikStokUrunSayisi = 0
    //
    // durumunu garanti altına alır.
    [Fact]
    public async Task GetirAsync_KritikStokYoksa_KritikStokSayisiSifirOlur()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new()
            {
                Sales =
                [
                    new SatisReadModel(
                        SatisId: 1,
                        Tarih: new DateOnly(2026, 2, 10),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: 10,
                        BirimFiyat: 100,
                        IskontoOrani: 0,
                        SatisKanali: null,
                        IsIade: false)
                ]
            };

        // Stok kaydı mevcut ancak yeterli stok var.
        FakeStokReadRepository stokRepository =
            new();

        stokRepository.Stocks[
            ("U001", "D001")
        ] =
            new StokReadModel(
                UrunKodu: "U001",
                DepoKodu: "D001",
                MevcutStok: 1000,
                MinStokSeviyesi: 10,
                SonSayimTarihi: new DateOnly(2026, 2, 1));

        // 90 günlük satış:
        //
        // 10 adet / 90 = 0.111...
        //
        // 1000 stok / 0.111... = çok yüksek stok günü.
        FakeStokReadRepository stok =
            stokRepository;

        FakeSatisReadRepository historicalSalesRepository =
            satisRepository;

        historicalSalesRepository.HistoricalSales[
            ("U001", "D001")
        ] =
        [
            new SatisReadModel(
                SatisId: 100,
                Tarih: new DateOnly(2026, 1, 10),
                BayiKodu: "B001",
                UrunKodu: "U001",
                DepoKodu: "D001",
                Miktar: 90,
                BirimFiyat: 100,
                IskontoOrani: 0,
                SatisKanali: null,
                IsIade: false)
        ];

        BusinessRulesOptions options =
            CreateOptions();

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: historicalSalesRepository,
                stokRepository: stok);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                new DateOnly(2026, 2, 15),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(
            0,
            result.Value!.KritikStokUrunSayisi);

        // KRİTİK NOKTA:
        //
        // kritikStokUrunSayisi = 0 olduğunda RiskEngine,
        // STOK_YETERSIZ gerekçesi üretmemelidir.
        Assert.DoesNotContain(
            "STOK_YETERSIZ",
            result.Value.GerekceKodlari);
    }

    // ------------------------------------------------------------
    // 9. Kritik stok varsa sayı artırılır.
    // ------------------------------------------------------------
    [Fact]
    public async Task GetirAsync_KritikStokVarsa_KritikStokSayisiniArtirir()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    "B001",
                    "A",
                    "Marmara")
            };

        FakeSatisReadRepository satisRepository =
            new()
            {
                Sales =
                [
                    new SatisReadModel(
                        SatisId: 1,
                        Tarih: new DateOnly(2026, 2, 10),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: 10,
                        BirimFiyat: 100,
                        IskontoOrani: 0,
                        SatisKanali: null,
                        IsIade: false)
                ]
            };

        // Kritik olması için mevcut stok çok düşük tutuluyor.
        FakeStokReadRepository stokRepository =
            new();

        stokRepository.Stocks[
            ("U001", "D001")
        ] =
            new StokReadModel(
                UrunKodu: "U001",
                DepoKodu: "D001",
                MevcutStok: 1,
                MinStokSeviyesi: 0,
                SonSayimTarihi: new DateOnly(2026, 2, 1));

        // 90 günlük satış = 90.
        //
        // Günlük satış = 1.
        //
        // Stok = 1.
        //
        // Tahmini stok günü = 1.
        //
        // Threshold = 7.
        //
        // 1 < 7 => KRİTİK.
        satisRepository.HistoricalSales[
            ("U001", "D001")
        ] =
        [
            new SatisReadModel(
                SatisId: 100,
                Tarih: new DateOnly(2026, 1, 10),
                BayiKodu: "B001",
                UrunKodu: "U001",
                DepoKodu: "D001",
                Miktar: 90,
                BirimFiyat: 100,
                IskontoOrani: 0,
                SatisKanali: null,
                IsIade: false)
        ];

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository,
                stokRepository: stokRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                new DateOnly(2026, 2, 15),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        Assert.Equal(
            1,
            result.Value!.KritikStokUrunSayisi);

        // Kritik stok bulunduğundan RiskEngine'in
        // STOK_YETERSIZ üretmesini bekleriz.
        Assert.Contains(
            "STOK_YETERSIZ",
            result.Value.GerekceKodlari);
    }

    // ------------------------------------------------------------
    // 10. Başarılı sonuç output'u doğru doldurur.
    // ------------------------------------------------------------
    [Fact]
    public async Task GetirAsync_BasariliDurumda_OutputDogruOlusturulur()
    {
        // Arrange
        FakeBayiReadRepository bayiRepository =
            new()
            {
                Result = new BayiReadModel(
                    BayiKodu: "B001",
                    Segment: "PREMIUM",
                    Bolge: "MARMARA")
            };

        FakeSatisReadRepository satisRepository =
            new()
            {
                Sales =
                [
                    new SatisReadModel(
                        SatisId: 1,
                        Tarih: new DateOnly(2026, 2, 10),
                        BayiKodu: "B001",
                        UrunKodu: "U001",
                        DepoKodu: "D001",
                        Miktar: 10,
                        BirimFiyat: 100,
                        IskontoOrani: 0,
                        SatisKanali: "MAGAZA",
                        IsIade: false)
                ]
            };

        FakeUrunReadRepository urunRepository =
            new()
            {
                Results =
                [
                    new UrunReadModel(
                        UrunKodu: "U001",
                        Kategori: "Ana",
                        AltKategori: "Alt",
                        BirimMaliyet: 60)
                ]
            };

        FakeAylikHedefReadRepository hedefRepository =
            new()
            {
                Result = new AylikHedefReadModel(
                    BayiKodu: "B001",
                    Yil: 2026,
                    Ay: 2,
                    Hedef: 1000)
            };

        BayiPerformansService service =
            CreateService(
                bayiRepository: bayiRepository,
                satisRepository: satisRepository,
                hedefRepository: hedefRepository,
                urunRepository: urunRepository);

        // Act
        ServiceResult<BayiPerformansAppWeb.Entities.ServiceOutputs.BayiPerformansOutput> result =
            await service.GetirAsync(
                "B001",
                new DateOnly(2026, 2, 15),
                CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var output = result.Value!;

        Assert.Equal(
            "B001",
            output.BayiKodu);

        Assert.Equal(
            "PREMIUM",
            output.Segment);

        Assert.Equal(
            "MARMARA",
            output.Bolge);

        Assert.Equal(
            new DateOnly(2026, 2, 1),
            output.Donem);

        // 10 * 100 = 1000 net ciro.
        Assert.Equal(
            1000m,
            output.GerceklesenCiro);

        Assert.Equal(
            1000m,
            output.HedefCiro);

        // 1000 / 1000 = 1.00
        Assert.Equal(
            1m,
            output.HedefeUlasmaPct);

        Assert.Equal(
            0.40m,
            output.OrtalamaMarjPct);

        Assert.Equal(
            0m,
            output.OrtalamaIskontoPct);

        Assert.Equal(
            0m,
            output.IadeOraniPct);

        Assert.Equal(
            CreateOptions().MarjEsigiPct,
            output.MarjEsigiPct);

        Assert.Equal(
            0,
            output.KritikStokUrunSayisi);
    }

    // ------------------------------------------------------------
    // Yardımcı servis oluşturucu
    // ------------------------------------------------------------
    // Her testte constructor'ın tamamını tekrar yazmamak için
    // ortak bir factory metodu kullanıyoruz.
    private static BayiPerformansService CreateService(
        FakeBayiReadRepository? bayiRepository = null,
        FakeSatisReadRepository? satisRepository = null,
        FakeAylikHedefReadRepository? hedefRepository = null,
        FakeUrunReadRepository? urunRepository = null,
        FakeStokReadRepository? stokRepository = null)
    {
        bayiRepository ??= new FakeBayiReadRepository
        {
            Result = new BayiReadModel(
                "B001",
                "A",
                "Marmara")
        };

        satisRepository ??= new FakeSatisReadRepository();

        hedefRepository ??= new FakeAylikHedefReadRepository();

        urunRepository ??= new FakeUrunReadRepository();

        stokRepository ??= new FakeStokReadRepository();

        BusinessRulesOptions options =
            CreateOptions();

        SalesMetricsCalculator salesMetricsCalculator =
            new();

        StockMetricsCalculator stockMetricsCalculator =
            new(options.StokGunEsigi);

        RiskEngine riskEngine =
            new(options);

        return new BayiPerformansService(
            bayiRepository,
            satisRepository,
            hedefRepository,
            urunRepository,
            stokRepository,
            salesMetricsCalculator,
            stockMetricsCalculator,
            riskEngine,
            options);
    }

    // ------------------------------------------------------------
    // Test configuration
    // ------------------------------------------------------------
    // Gerçek thresholds.json'a bağımlı olmamak için testlerde
    // deterministik bir BusinessRulesOptions kullanıyoruz.
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

            RiskWeights = new Dictionary<string, int>
            {
                ["MARJ_ESIK_ALTI"] = 40,
                ["HEDEF_ALTINDA"] = 30,
                ["YUKSEK_ISKONTO"] = 20,
                ["STOK_YETERSIZ"] = 50,
                ["YUKSEK_IADE"] = 30
            },

            RiskLevels = new Dictionary<string, int>
            {
                ["DUSUK_max"] = 30,
                ["ORTA_max"] = 60,
                ["YUKSEK_max"] = 900
            }
        };
    }
}


// ============================================================================
// FAKE REPOSITORY'LER
// ============================================================================
// Moq kullanmadığımız için repository interface'lerini test içinde
// basit fake sınıflarla implement ediyoruz.
//
// Bunların amacı gerçek database davranışını taklit etmek değil;
// servise kontrollü veri verip servisin davranışını test etmektir.
// ============================================================================


// ----------------------------------------------------------------------------
// IBayiReadRepository Fake
// ----------------------------------------------------------------------------
internal sealed class FakeBayiReadRepository : IBayiReadRepository
{
    public BayiReadModel? Result { get; set; }

    public bool GetByCodeCalled { get; private set; }

    public string? LastBayiKodu { get; private set; }

    public Task<BayiReadModel?> GetByCodeAsync(
        string bayiKodu,
        CancellationToken cancellationToken)
    {
        GetByCodeCalled = true;

        LastBayiKodu = bayiKodu;

        return Task.FromResult(Result);
    }

    public Task<IReadOnlyList<BayiReadModel>> ListAsync(
        string? segment,
        string? bolge,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<BayiReadModel>>(
            Array.Empty<BayiReadModel>());
    }
}


// ----------------------------------------------------------------------------
// ISatisReadRepository Fake
// ----------------------------------------------------------------------------
internal sealed class FakeSatisReadRepository : ISatisReadRepository
{
    public DateOnly? LatestSaleDate { get; set; }

    public bool GetLatestSaleDateCalled { get; private set; }

    public IReadOnlyList<SatisReadModel> Sales { get; set; } =
        Array.Empty<SatisReadModel>();

    public Dictionary<
        (string UrunKodu, string DepoKodu),
        IReadOnlyList<SatisReadModel>>
        HistoricalSales
    { get; } = new();

    public DateOnly? LastPeriodStart { get; private set; }

    public DateOnly? LastPeriodEndExclusive { get; private set; }

    public Task<DateOnly?> GetLatestSaleDateAsync(
        CancellationToken cancellationToken)
    {
        GetLatestSaleDateCalled = true;

        return Task.FromResult(LatestSaleDate);
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Sales);
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(
        string bayiKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        LastPeriodStart = periodStart;
        LastPeriodEndExclusive = periodEndExclusive;

        return Task.FromResult(Sales);
    }

    public Task<IReadOnlyList<SatisReadModel>>
        ListByProductWarehouseAndPeriodAsync(
            string urunKodu,
            string depoKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        if (HistoricalSales.TryGetValue(
                (urunKodu, depoKodu),
                out IReadOnlyList<SatisReadModel>? result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult<IReadOnlyList<SatisReadModel>>(
            Array.Empty<SatisReadModel>());
    }

    public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesByPeriodAsync(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        return Task.FromResult(AggregateHistoricalSales());
    }

    public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesForDealerAsync(
            string bayiKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        return Task.FromResult(AggregateHistoricalSales());
    }

    public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DateOnly> result = Sales
            .Select(s => new DateOnly(s.Tarih.Year, s.Tarih.Month, 1))
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        return Task.FromResult(result);
    }

    private IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>
        AggregateHistoricalSales()
    {
        return HistoricalSales
            .SelectMany(entry => entry.Value)
            .Where(sale => sale.Miktar.HasValue)
            .GroupBy(sale => (sale.UrunKodu, sale.DepoKodu))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(sale =>
                    sale.IsIade == true
                        ? -Math.Abs(sale.Miktar!.Value)
                        : sale.Miktar!.Value));
    }
}


// ----------------------------------------------------------------------------
// IUrunReadRepository Fake
// ----------------------------------------------------------------------------
internal sealed class FakeUrunReadRepository : IUrunReadRepository
{
    public IReadOnlyList<UrunReadModel> Results { get; set; } =
        Array.Empty<UrunReadModel>();

    public Task<UrunReadModel?> GetByCodeAsync(
        string urunKodu,
        CancellationToken cancellationToken)
    {
        UrunReadModel? result =
            Results.FirstOrDefault(
                x => x.UrunKodu == urunKodu);

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(
        IReadOnlyCollection<string> urunKodlari,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UrunReadModel> result =
            Results
                .Where(x => urunKodlari.Contains(
                    x.UrunKodu,
                    StringComparer.Ordinal))
                .ToArray();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Results);
    }
}


// ----------------------------------------------------------------------------
// IStokReadRepository Fake
// ----------------------------------------------------------------------------
internal sealed class FakeStokReadRepository : IStokReadRepository
{
    public Dictionary<
        (string UrunKodu, string DepoKodu),
        StokReadModel>
        Stocks
    { get; } = new();

    public Task<IReadOnlyList<StokReadModel>> ListAsync(
        string? depoKodu,
        string? urunKodu,
        CancellationToken cancellationToken)
    {
        IEnumerable<StokReadModel> result =
            Stocks.Values;

        if (!string.IsNullOrWhiteSpace(depoKodu))
        {
            result =
                result.Where(x => x.DepoKodu == depoKodu);
        }

        if (!string.IsNullOrWhiteSpace(urunKodu))
        {
            result =
                result.Where(x => x.UrunKodu == urunKodu);
        }

        return Task.FromResult<IReadOnlyList<StokReadModel>>(
            result.ToArray());
    }

    public Task<StokReadModel?> GetAsync(
        string urunKodu,
        string depoKodu,
        CancellationToken cancellationToken)
    {
        Stocks.TryGetValue(
            (urunKodu, depoKodu),
            out StokReadModel? result);

        return Task.FromResult(result);
    }
}


// ----------------------------------------------------------------------------
// IAylikHedefReadRepository Fake
// ----------------------------------------------------------------------------
internal sealed class FakeAylikHedefReadRepository
    : IAylikHedefReadRepository
{
    public AylikHedefReadModel? Result { get; set; }

    public Task<AylikHedefReadModel?> GetAsync(
        string bayiKodu,
        int yil,
        int ay,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Result);
    }

    public Task<IReadOnlyList<AylikHedefReadModel>> ListByPeriodAsync(
        int yil,
        int ay,
        CancellationToken cancellationToken)
    {
        if (Result is null)
        {
            return Task.FromResult<IReadOnlyList<AylikHedefReadModel>>(
                Array.Empty<AylikHedefReadModel>());
        }

        return Task.FromResult<IReadOnlyList<AylikHedefReadModel>>(
            new[] { Result });
    }
}
