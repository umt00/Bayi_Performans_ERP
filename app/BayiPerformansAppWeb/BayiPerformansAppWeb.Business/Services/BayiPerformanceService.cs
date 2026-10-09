using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Bayinin belirli bir dönem içerisindeki performansını hesaplar.
///
/// Service katmanı:
/// - Repository'lerden verileri toplar.
/// - DataAccess modellerini Business modellerine dönüştürür.
/// - SalesMetricsCalculator ile satış metriklerini hesaplar.
/// - StockMetricsCalculator ile kritik stok bilgisini hesaplar.
/// - RiskEngine ile risk skorunu üretir.
/// - Sonucu BayiPerformansOutput olarak döndürür.
///
/// Veritabanına doğrudan erişmez.
/// Tüm veri erişimi repository abstraction'ları üzerinden yapılır.
/// </summary>
public sealed class BayiPerformansService : IBayiPerformansService
{
    private readonly IBayiReadRepository _bayiRepository;
    private readonly ISatisReadRepository _satisRepository;
    private readonly IAylikHedefReadRepository _hedefRepository;
    private readonly IUrunReadRepository _urunRepository;
    private readonly IStokReadRepository _stokRepository;

    private readonly SalesMetricsCalculator _salesMetricsCalculator;
    private readonly StockMetricsCalculator _stockMetricsCalculator;
    private readonly RiskEngine _riskEngine;

    private readonly BusinessRulesOptions _options;

    public BayiPerformansService(
        IBayiReadRepository bayiRepository,
        ISatisReadRepository satisRepository,
        IAylikHedefReadRepository hedefRepository,
        IUrunReadRepository urunRepository,
        IStokReadRepository stokRepository,
        SalesMetricsCalculator salesMetricsCalculator,
        StockMetricsCalculator stockMetricsCalculator,
        RiskEngine riskEngine,
        BusinessRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(bayiRepository);
        ArgumentNullException.ThrowIfNull(satisRepository);
        ArgumentNullException.ThrowIfNull(hedefRepository);
        ArgumentNullException.ThrowIfNull(urunRepository);
        ArgumentNullException.ThrowIfNull(stokRepository);
        ArgumentNullException.ThrowIfNull(salesMetricsCalculator);
        ArgumentNullException.ThrowIfNull(stockMetricsCalculator);
        ArgumentNullException.ThrowIfNull(riskEngine);
        ArgumentNullException.ThrowIfNull(options);

        _bayiRepository = bayiRepository;
        _satisRepository = satisRepository;
        _hedefRepository = hedefRepository;
        _urunRepository = urunRepository;
        _stokRepository = stokRepository;

        _salesMetricsCalculator = salesMetricsCalculator;
        _stockMetricsCalculator = stockMetricsCalculator;
        _riskEngine = riskEngine;

        _options = options;
    }

    /// <summary>
    /// Belirtilen bayi için performans raporu üretir.
    ///
    /// Donem verilirse o ay kullanılır.
    /// Donem verilmezse sistemdeki son satış tarihinin bulunduğu
    /// ay varsayılan dönem olarak kullanılır.
    /// </summary>
    public async Task<ServiceResult<BayiPerformansOutput>> GetirAsync(
        string bayiKodu,
        DateOnly? donem,
        CancellationToken cancellationToken)
    {
        // Bayi kodunun geçerli olması gerekir.
        if (string.IsNullOrWhiteSpace(bayiKodu))
        {
            return ServiceResult<BayiPerformansOutput>.Fail(
                ServiceErrorKind.Validation,
                "GECERSIZ_BAYI_KODU",
                "Bayi kodu boş olamaz.");
        }

        bayiKodu = bayiKodu.Trim();

        // --------------------------------------------------
        // 1. BAYİ KONTROLÜ
        // --------------------------------------------------

        // Öncelikle istenen bayinin sistemde bulunup
        // bulunmadığını kontrol ediyoruz.
        BayiReadModel? bayi =
            await _bayiRepository.GetByCodeAsync(
                bayiKodu,
                cancellationToken);

        if (bayi is null)
        {
            return ServiceResult<BayiPerformansOutput>.Fail(
                ServiceErrorKind.NotFound,
                "BAYI_BULUNAMADI",
                $"'{bayiKodu}' kodlu bayi bulunamadı.");
        }

        // --------------------------------------------------
        // 2. DÖNEM BELİRLEME
        // --------------------------------------------------

        DateOnly donemTarihi;

        if (donem.HasValue)
        {
            // Kullanıcı dönem verdiyse doğrudan o dönem kullanılır.
            donemTarihi = donem.Value;
        }
        else
        {
            // Dönem verilmemişse son satış tarihi üzerinden
            // varsayılan dönem belirlenir.
            DateOnly? latestSaleDate =
                await _satisRepository.GetLatestSaleDateAsync(
                    cancellationToken);

            if (!latestSaleDate.HasValue)
            {
                return ServiceResult<BayiPerformansOutput>.Fail(
                    ServiceErrorKind.NotFound,
                    "SATIS_VERISI_YOK",
                    "Varsayılan dönemi belirlemek için satış tarihi bulunamadı.");
            }

            donemTarihi = latestSaleDate.Value;
        }

        DateOnly periodStart =
            new(
                donemTarihi.Year,
                donemTarihi.Month,
                1);

        // Bitiş tarihi exclusive tutulur.
        DateOnly periodEndExclusive =
            periodStart.AddMonths(1);

        // --------------------------------------------------
        // 3. SATIŞLARI GETİR
        // --------------------------------------------------

        IReadOnlyList<SatisReadModel> satislar =
            await _satisRepository.ListByDealerAndPeriodAsync(
                bayiKodu,
                periodStart,
                periodEndExclusive,
                cancellationToken);

        // --------------------------------------------------
        // 4. ÜRÜN MALİYETLERİNİ GETİR
        // --------------------------------------------------

        // Satışlarda kullanılan ürün kodlarını topluyoruz.
        string[] urunKodlari = satislar
            .Select(satis => satis.UrunKodu)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Ürün maliyetlerini toplu olarak getiriyoruz.
        IReadOnlyList<UrunReadModel> urunler =
            await _urunRepository.ListByCodesAsync(
                urunKodlari,
                cancellationToken);

        Dictionary<string, UrunReadModel> urunByCode =
            urunler.ToDictionary(
                urun => urun.UrunKodu,
                StringComparer.Ordinal);

        // --------------------------------------------------
        // 5. SALES METRIC LINE OLUŞTUR
        // --------------------------------------------------

        List<SalesMetricLine> salesLines = new();

        foreach (SatisReadModel satis in satislar)
        {
            // Zorunlu satış alanlarından biri eksikse
            // veri uydurmuyoruz ve satırı hesaplamaya dahil etmiyoruz.
            if (!satis.Miktar.HasValue ||
                !satis.BirimFiyat.HasValue ||
                !satis.IskontoOrani.HasValue)
            {
                continue;
            }

            // Satışın ürün maliyetini buluyoruz.
            urunByCode.TryGetValue(
                satis.UrunKodu,
                out UrunReadModel? urun);

            salesLines.Add(
                new SalesMetricLine(
                    SatisId: satis.SatisId,
                    Miktar: satis.Miktar.Value,
                    BirimFiyat: satis.BirimFiyat.Value,
                    IskontoOrani: satis.IskontoOrani.Value,
                    BirimMaliyet: urun?.BirimMaliyet,
                    IsIade: satis.IsIade ?? false));
        }

        // --------------------------------------------------
        // 6. AYLIK HEDEF
        // --------------------------------------------------

        // İlgili bayi ve dönem için hedef bilgisi okunur.
        AylikHedefReadModel? hedef =
            await _hedefRepository.GetAsync(
                bayiKodu,
                periodStart.Year,
                periodStart.Month,
                cancellationToken);

        // Hedef bulunmuyorsa null olarak calculator'a aktarılır.
        decimal? hedefCiro =
            hedef?.Hedef;

        // --------------------------------------------------
        // 7. SATIŞ METRİKLERİ
        // --------------------------------------------------

        SalesMetricsResult salesMetrics =
            _salesMetricsCalculator.Calculate(
                salesLines,
                hedefCiro);

        // --------------------------------------------------
        // 8. KRİTİK STOK HESABI
        // --------------------------------------------------

        int kritikStokUrunSayisi =
            await CalculateCriticalStockCountAsync(
                satislar,
                periodStart,
                cancellationToken);

        // --------------------------------------------------
        // 9. RISK ENGINE
        // --------------------------------------------------

        RiskEngineResult risk =
            _riskEngine.Calculate(
                salesMetrics,
                kritikStokUrunSayisi);

        // --------------------------------------------------
        // 10. OUTPUT
        // --------------------------------------------------

        string segment =
            bayi.Segment ?? string.Empty;

        string bolge =
            bayi.Bolge ?? string.Empty;

        BayiPerformansOutput output =
            new(
                BayiKodu: bayi.BayiKodu,
                Segment: segment,
                Bolge: bolge,
                Donem: periodStart,
                GerceklesenCiro:
                    salesMetrics.GerceklesenCiro,
                HedefCiro:
                    salesMetrics.HedefCiro,
                HedefeUlasmaPct:
                    salesMetrics.HedefeUlasmaPct,
                OrtalamaMarjPct:
                    salesMetrics.OrtalamaMarjPct,
                MarjEsigiPct:
                    _options.MarjEsigiPct,
                OrtalamaIskontoPct:
                    salesMetrics.OrtalamaIskontoPct,
                IadeOraniPct:
                    salesMetrics.IadeOraniPct,
                KritikStokUrunSayisi:
                    kritikStokUrunSayisi,
                RiskSkoru:
                    risk.RiskSkoru,
                RiskSeviyesi:
                    risk.RiskSeviyesi,
                GerekceKodlari:
                    risk.GerekceKodlari);

        // Hesaplanan sonuç başarılı ServiceResult olarak döndürülür.
        return ServiceResult<BayiPerformansOutput>.Success(
            output);
    }

    /// <summary>
    /// Bayinin satış yaptığı ürün/depo kombinasyonları üzerinden
    /// kritik stok ürünlerini hesaplar.
    ///
    /// Stok günü hesabında son 90 günlük ürün/depo satış hızı
    /// kullanılır.
    /// </summary>
    private async Task<int> CalculateCriticalStockCountAsync(
        IReadOnlyList<SatisReadModel> satislar,
        DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        // periodStart dahil tam 90 günlük geçmiş: [periodStart - 89, periodStart + 1)
        DateOnly historyStart =
            periodStart.AddDays(-89);

        DateOnly historyEndExclusive =
            periodStart.AddDays(1);

        // Satışlarda kullanılan benzersiz ürün/depo kombinasyonlarını
        // çıkarıyoruz.
        var productWarehousePairs =
            satislar
                .Select(satis => new
                {
                    satis.UrunKodu,
                    satis.DepoKodu
                })
                .Where(pair =>
                    !string.IsNullOrWhiteSpace(pair.UrunKodu)
                    && !string.IsNullOrWhiteSpace(pair.DepoKodu))
                .Distinct()
                .ToArray();

        if (productWarehousePairs.Length == 0)
        {
            return 0;
        }

        string bayiKodu = satislar[0].BayiKodu;

        IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>
            satisHizlari =
                await _satisRepository.GetTotalSalesQuantitiesForDealerAsync(
                    bayiKodu,
                    historyStart,
                    historyEndExclusive,
                    cancellationToken);

        int criticalCount = 0;

        foreach (var pair in productWarehousePairs)
        {
            // İlgili ürün/depo için stok kaydı okunur.
            StokReadModel? stock =
                await _stokRepository.GetAsync(
                    pair.UrunKodu!,
                    pair.DepoKodu!,
                    cancellationToken);

            // Stok kaydı yoksa mevcut mantığı koruyarak
            // bu kombinasyonu kritik stok sayısına eklemiyoruz.
            if (stock is null)
            {
                continue;
            }

            decimal sonUcAySatisAdedi =
                satisHizlari.TryGetValue(
                    (pair.UrunKodu!, pair.DepoKodu!),
                    out decimal satisAdedi)
                    ? satisAdedi
                    : 0m;

            // Stok metrikleri calculator tarafından hesaplanır.
            StockMetricsResult stockMetrics =
                _stockMetricsCalculator.Calculate(
                    stock,
                    sonUcAySatisAdedi);

            // Calculator kritik olarak işaretlediyse sayacı artırıyoruz.
            if (stockMetrics.KritikMi)
            {
                criticalCount++;
            }
        }

        return criticalCount;
    }
}
