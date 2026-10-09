using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Bir bayinin geçmiş N aylık performans trendini hesaplayan Business servisidir.
/// </summary>
public sealed class BayiGecmisService : IBayiGecmisService
{
    private readonly IBayiReadRepository _bayiRepository;
    private readonly ISatisReadRepository _satisRepository;
    private readonly IAylikHedefReadRepository _hedefRepository;
    private readonly IUrunReadRepository _urunRepository;
    private readonly IStokReadRepository _stokRepository;
    private readonly SalesMetricsCalculator _salesMetricsCalculator;
    private readonly StockMetricsCalculator _stockMetricsCalculator;
    private readonly RiskEngine _riskEngine;
    private readonly IBayiDonemPerformansReadRepository? _bayiDonemPerformansReadRepository;

    public BayiGecmisService(
        IBayiReadRepository bayiRepository,
        ISatisReadRepository satisRepository,
        IAylikHedefReadRepository hedefRepository,
        IUrunReadRepository urunRepository,
        IStokReadRepository stokRepository,
        SalesMetricsCalculator salesMetricsCalculator,
        StockMetricsCalculator stockMetricsCalculator,
        RiskEngine riskEngine,
        IBayiDonemPerformansReadRepository? bayiDonemPerformansReadRepository = null)
    {
        ArgumentNullException.ThrowIfNull(bayiRepository);
        ArgumentNullException.ThrowIfNull(satisRepository);
        ArgumentNullException.ThrowIfNull(hedefRepository);
        ArgumentNullException.ThrowIfNull(urunRepository);
        ArgumentNullException.ThrowIfNull(stokRepository);
        ArgumentNullException.ThrowIfNull(salesMetricsCalculator);
        ArgumentNullException.ThrowIfNull(stockMetricsCalculator);
        ArgumentNullException.ThrowIfNull(riskEngine);

        _bayiRepository = bayiRepository;
        _satisRepository = satisRepository;
        _hedefRepository = hedefRepository;
        _urunRepository = urunRepository;
        _stokRepository = stokRepository;
        _salesMetricsCalculator = salesMetricsCalculator;
        _stockMetricsCalculator = stockMetricsCalculator;
        _riskEngine = riskEngine;
        _bayiDonemPerformansReadRepository = bayiDonemPerformansReadRepository;
    }

    public async Task<ServiceResult<BayiGecmisPerformansOutput>> GetirGecmisAsync(
        BayiGecmisQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.BayiKodu))
        {
            return ServiceResult<BayiGecmisPerformansOutput>.Fail(
                ServiceErrorKind.Validation,
                "GECERSIZ_BAYI_KODU",
                "Bayi kodu boş olamaz.");
        }

        string bayiKodu = query.BayiKodu.Trim();

        // 1. Önce veritabanındaki bayi_donem_performans tablosundan geçmiş dönem verilerini oku
        if (_bayiDonemPerformansReadRepository != null)
        {
            BayiReadModel? masterBayi = await _bayiRepository.GetByCodeAsync(bayiKodu, cancellationToken);
            if (masterBayi is null)
            {
                return ServiceResult<BayiGecmisPerformansOutput>.Fail(
                    ServiceErrorKind.NotFound,
                    "BAYI_BULUNAMADI",
                    $"'{bayiKodu}' kodlu bayi bulunamadı.");
            }

            var historyRows = await _bayiDonemPerformansReadRepository.GetHistoryAsync(
                bayiKodu,
                query.AySayisi > 0 ? query.AySayisi : 60,
                cancellationToken);

            var mappedItems = historyRows.Select(item => new BayiAylikPerformansItemOutput(
                Donem: item.Donem,
                GerceklesenCiro: item.Ciro ?? 0m,
                HedefCiro: item.Hedef,
                HedefeUlasmaPct: item.HedefTutmaOrani,
                OrtalamaMarjPct: item.OrtalamaKarMarji,
                OrtalamaIskontoPct: item.OrtalamaIskontoOrani,
                IadeOraniPct: item.OrtalamaIadeOrani,
                RiskSkoru: item.RiskSkoru ?? 0,
                RiskSeviyesi: item.RiskSeviyesi ?? "DUSUK",
                GerekceKodlari: item.GerekceKodlari ?? Array.Empty<string>()
            )).ToList();

            return ServiceResult<BayiGecmisPerformansOutput>.Success(
                new BayiGecmisPerformansOutput(
                    BayiKodu: bayiKodu,
                    Segment: masterBayi.Segment ?? historyRows.FirstOrDefault()?.Segment ?? string.Empty,
                    Bolge: masterBayi.Bolge ?? historyRows.FirstOrDefault()?.Bolge ?? string.Empty,
                    AylikPerformanslar: mappedItems));
        }

        // 2. Tabloda veri olmaması durumunda ham satış hareketlerinden canlı hesaplama yedek akışı
        BayiReadModel? bayi = await _bayiRepository.GetByCodeAsync(bayiKodu, cancellationToken);
        if (bayi is null)
        {
            return ServiceResult<BayiGecmisPerformansOutput>.Fail(
                ServiceErrorKind.NotFound,
                "BAYI_BULUNAMADI",
                $"'{bayiKodu}' kodlu bayi bulunamadı.");
        }

        DateOnly? latestSaleDate = await _satisRepository.GetLatestSaleDateAsync(cancellationToken);
        if (!latestSaleDate.HasValue)
        {
            return ServiceResult<BayiGecmisPerformansOutput>.Fail(
                ServiceErrorKind.NotFound,
                "SATIS_VERISI_YOK",
                "Geçmiş performansı hesaplamak için satış verisi bulunamadı.");
        }

        int aySayisi = Math.Clamp(query.AySayisi > 0 ? query.AySayisi : 60, 1, 60);
        DateOnly currentMonth = new(latestSaleDate.Value.Year, latestSaleDate.Value.Month, 1);

        List<DateOnly> months = new();
        for (int i = aySayisi - 1; i >= 0; i--)
        {
            months.Add(currentMonth.AddMonths(-i));
        }

        DateOnly overallStart = months.First();
        DateOnly overallEndExclusive = months.Last().AddMonths(1);

        IReadOnlyList<SatisReadModel> allSales = await _satisRepository.ListByDealerAndPeriodAsync(
            bayiKodu,
            overallStart,
            overallEndExclusive,
            cancellationToken);

        string[] urunKodlari = allSales
            .Select(s => s.UrunKodu)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<UrunReadModel> urunler = urunKodlari.Length == 0
            ? Array.Empty<UrunReadModel>()
            : await _urunRepository.ListByCodesAsync(urunKodlari, cancellationToken);

        Dictionary<string, UrunReadModel> urunByCode = urunler.ToDictionary(
            u => u.UrunKodu,
            StringComparer.Ordinal);

        IReadOnlyList<StokReadModel> tumStoklar = await _stokRepository.ListAsync(null, null, cancellationToken);
        Dictionary<(string UrunKodu, string DepoKodu), StokReadModel> stokByUrunDepo = tumStoklar
            .Where(s => !string.IsNullOrWhiteSpace(s.UrunKodu) && !string.IsNullOrWhiteSpace(s.DepoKodu))
            .GroupBy(s => (s.UrunKodu, s.DepoKodu))
            .ToDictionary(g => g.Key, g => g.First());

        List<BayiAylikPerformansItemOutput> items = new();

        foreach (var month in months)
        {
            DateOnly monthStart = month;
            DateOnly monthEndExclusive = month.AddMonths(1);

            var monthSales = allSales
                .Where(s => s.Tarih >= monthStart && s.Tarih < monthEndExclusive)
                .ToList();

            List<SalesMetricLine> salesLines = new();
            foreach (var satis in monthSales)
            {
                if (!satis.Miktar.HasValue || !satis.BirimFiyat.HasValue || !satis.IskontoOrani.HasValue)
                {
                    continue;
                }

                urunByCode.TryGetValue(satis.UrunKodu, out var urun);
                salesLines.Add(new SalesMetricLine(
                    SatisId: satis.SatisId,
                    Miktar: satis.Miktar.Value,
                    BirimFiyat: satis.BirimFiyat.Value,
                    IskontoOrani: satis.IskontoOrani.Value,
                    BirimMaliyet: urun?.BirimMaliyet,
                    IsIade: satis.IsIade ?? false));
            }

            AylikHedefReadModel? hedef = await _hedefRepository.GetAsync(
                bayiKodu,
                month.Year,
                month.Month,
                cancellationToken);

            decimal? hedefCiro = hedef?.Hedef;

            SalesMetricsResult salesMetrics = _salesMetricsCalculator.Calculate(salesLines, hedefCiro);

            DateOnly historyStart = monthStart.AddDays(-89);
            DateOnly historyEndExclusive = monthStart.AddDays(1);

            IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal> satisHizlari =
                await _satisRepository.GetTotalSalesQuantitiesForDealerAsync(
                    bayiKodu,
                    historyStart,
                    historyEndExclusive,
                    cancellationToken);

            int kritikStokSayisi = 0;
            bool isLatestMonth = (month == months[^1]);

            if (isLatestMonth)
            {
                var productWarehousePairs = monthSales
                    .Select(s => new { s.UrunKodu, s.DepoKodu })
                    .Where(p => !string.IsNullOrWhiteSpace(p.UrunKodu) && !string.IsNullOrWhiteSpace(p.DepoKodu))
                    .Distinct()
                    .ToArray();

                foreach (var pair in productWarehousePairs)
                {
                    var key = (pair.UrunKodu!, pair.DepoKodu!);
                    if (stokByUrunDepo.TryGetValue(key, out var stock))
                    {
                        decimal sonUcAySatisAdedi = satisHizlari.TryGetValue(key, out decimal sAdet) ? sAdet : 0m;
                        var stockMetrics = _stockMetricsCalculator.Calculate(stock, sonUcAySatisAdedi);
                        if (stockMetrics.KritikMi)
                        {
                            kritikStokSayisi++;
                        }
                    }
                }
            }

            RiskEngineResult risk = _riskEngine.Calculate(salesMetrics, kritikStokSayisi);

            items.Add(new BayiAylikPerformansItemOutput(
                Donem: month,
                GerceklesenCiro: salesMetrics.GerceklesenCiro,
                HedefCiro: salesMetrics.HedefCiro,
                HedefeUlasmaPct: salesMetrics.HedefeUlasmaPct,
                OrtalamaMarjPct: salesMetrics.OrtalamaMarjPct,
                OrtalamaIskontoPct: salesMetrics.OrtalamaIskontoPct,
                IadeOraniPct: salesMetrics.IadeOraniPct,
                RiskSkoru: risk.RiskSkoru,
                RiskSeviyesi: risk.RiskSeviyesi,
                GerekceKodlari: risk.GerekceKodlari));
        }

        var output = new BayiGecmisPerformansOutput(
            BayiKodu: bayi.BayiKodu,
            Segment: bayi.Segment ?? string.Empty,
            Bolge: bayi.Bolge ?? string.Empty,
            AylikPerformanslar: items);

        return ServiceResult<BayiGecmisPerformansOutput>.Success(output);
    }
}
