using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Belirli bir dönem için filtrelenmiş bayilerin
/// risk listesini toplu olarak hesaplar.
/// </summary>
public sealed class RiskListesiService : IRiskListesiService
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

    public RiskListesiService(
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

    public async Task<ServiceResult<RiskListesiOutput>> ListeleAsync(
        RiskListesiQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. Önce Veritabanı bayi_donem_performans Tablosundan Hazır Snapshot Verilerini Oku
        List<RiskListesiItemOutput> riskItems;
        DateOnly periodStart;

        if (_bayiDonemPerformansReadRepository != null)
        {
            var dbQuery = new BayiDonemPerformansQuery(
                query.Donem,
                query.RiskSeviyesi,
                query.Bolge,
                query.Segment,
                Sayfa: 1,
                SayfaBoyutu: 10000,
                Sehir: query.Sehir);

            var pageResult = await _bayiDonemPerformansReadRepository.ListAsync(dbQuery, cancellationToken);
            periodStart = pageResult.Donem ?? query.Donem ?? DateOnly.FromDateTime(DateTime.Today);

            riskItems = pageResult.Bayiler.Select(item => new RiskListesiItemOutput(
                BayiKodu: item.BayiKodu,
                Segment: item.Segment ?? string.Empty,
                Bolge: item.Bolge ?? string.Empty,
                HedefeUlasmaPct: item.HedefTutmaOrani,
                OrtalamaMarjPct: item.OrtalamaKarMarji,
                OrtalamaIskontoPct: item.OrtalamaIskontoOrani,
                RiskSkoru: item.RiskSkoru ?? 0,
                RiskSeviyesi: item.RiskSeviyesi ?? "DUSUK",
                GerekceKodlari: item.GerekceKodlari ?? Array.Empty<string>(),
                Sehir: item.Sehir,
                BayiAdi: item.BayiAdi,
                GerceklesenCiro: item.Ciro,
                HedefCiro: item.Hedef,
                KritikStokVarMi: (item.KritikStokKalanUrunSayisi ?? 0) > 0
            )).ToList();
        }
        else
        {
            // 2. Repository enjekte edilmemişse (eski test ortamları) dinamik canlı hesaplama yedek akışı
            DateOnly donemTarihi;
            if (query.Donem.HasValue)
            {
                donemTarihi = query.Donem.Value;
            }
            else
            {
                DateOnly? latestSaleDate = await _satisRepository.GetLatestSaleDateAsync(cancellationToken);
                if (!latestSaleDate.HasValue)
                {
                    return ServiceResult<RiskListesiOutput>.Fail(
                        ServiceErrorKind.NotFound,
                        "SATIS_VERISI_YOK",
                        "Varsayılan dönemi belirlemek için satış tarihi bulunamadı.");
                }
                donemTarihi = latestSaleDate.Value;
            }

            periodStart = new(donemTarihi.Year, donemTarihi.Month, 1);
            DateOnly periodEndExclusive = periodStart.AddMonths(1);

            // BAYİLERİ GETİR
            HashSet<string>? segmentSet = ParseCommaSeparated(query.Segment);
            HashSet<string>? bolgeSet = ParseCommaSeparated(query.Bolge);
            HashSet<string>? sehirSet = ParseCommaSeparated(query.Sehir);

            string? repoSegment = query.Segment is not null && !query.Segment.Contains(',') ? query.Segment : null;
            string? repoBolge = query.Bolge is not null && !query.Bolge.Contains(',') ? query.Bolge : null;

            IReadOnlyList<BayiReadModel> allBayiler = await _bayiRepository.ListAsync(repoSegment, repoBolge, cancellationToken);

            var filteredBayiler = allBayiler.Where(b =>
            {
                if (segmentSet is not null && (b.Segment is null || !segmentSet.Contains(b.Segment.ToUpperInvariant())))
                    return false;
                if (bolgeSet is not null && (b.Bolge is null || !bolgeSet.Contains(b.Bolge.ToUpperInvariant())))
                    return false;
                if (sehirSet is not null && (b.Sehir is null || !sehirSet.Contains(b.Sehir.ToUpperInvariant())))
                    return false;
                return true;
            }).ToList();

            if (filteredBayiler.Count == 0)
            {
                return ServiceResult<RiskListesiOutput>.Success(
                    new RiskListesiOutput(
                        Donem: periodStart,
                        Bayiler: Array.Empty<RiskListesiItemOutput>(),
                        ToplamKayitSayisi: 0,
                        Sayfa: query.Sayfa,
                        SayfaBoyutu: query.SayfaBoyutu));
            }

            // 3. DÖNEM SATIŞLARINI GETİR
            IReadOnlyList<SatisReadModel> satislar = await _satisRepository.ListByPeriodAsync(
                periodStart,
                periodEndExclusive,
                cancellationToken);

            // 4. ÜRÜN MALİYETLERİNİ GETİR
            string[] urunKodlari = satislar
                .Select(satis => satis.UrunKodu)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            IReadOnlyList<UrunReadModel> urunler = urunKodlari.Length == 0
                ? Array.Empty<UrunReadModel>()
                : await _urunRepository.ListByCodesAsync(urunKodlari, cancellationToken);

            Dictionary<string, UrunReadModel> urunByCode = urunler
                .Where(u => !string.IsNullOrWhiteSpace(u.UrunKodu))
                .ToDictionary(u => u.UrunKodu, StringComparer.Ordinal);

            // 5. AYLIK HEDEFLERİ GETİR
            IReadOnlyList<AylikHedefReadModel> hedefler = await _hedefRepository.ListByPeriodAsync(
                periodStart.Year,
                periodStart.Month,
                cancellationToken);

            Dictionary<string, AylikHedefReadModel> hedefByBayi = hedefler
                .Where(h => !string.IsNullOrWhiteSpace(h.BayiKodu))
                .ToDictionary(h => h.BayiKodu, StringComparer.Ordinal);

            // 6. KRİTİK STOK BİLGİLERİNİ HAZIRLA
            DateOnly? latestSaleRef = await _satisRepository.GetLatestSaleDateAsync(cancellationToken);
            DateOnly historyEndExclusive = (latestSaleRef ?? periodStart).AddDays(1);
            DateOnly historyStart = historyEndExclusive.AddDays(-90);

            IReadOnlyList<StokReadModel> stoklar = await _stokRepository.ListAsync(null, null, cancellationToken);
            var stokGruplari = stoklar
                .Where(s => !string.IsNullOrWhiteSpace(s.UrunKodu) && !string.IsNullOrWhiteSpace(s.DepoKodu))
                .GroupBy(s => (UrunKodu: s.UrunKodu, DepoKodu: s.DepoKodu));

            Dictionary<(string UrunKodu, string DepoKodu), StokReadModel> stokByUrunDepo =
                stokGruplari.ToDictionary(g => g.Key, g => g.First());

            IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal> satisHizlari =
                await _satisRepository.GetTotalSalesQuantitiesByPeriodAsync(historyStart, historyEndExclusive, cancellationToken);

            Dictionary<string, List<SatisReadModel>> satisByBayi = satislar
                .Where(s => !string.IsNullOrWhiteSpace(s.BayiKodu))
                .GroupBy(s => s.BayiKodu, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            // 7. BAYİ RİSKLERİNİ VE METRİKLERİNİ HESAPLA
            riskItems = new List<RiskListesiItemOutput>();

            foreach (BayiReadModel bayi in filteredBayiler)
            {
                satisByBayi.TryGetValue(bayi.BayiKodu, out List<SatisReadModel>? bayiSatislari);
                bayiSatislari ??= [];

                List<SalesMetricLine> salesLines = new();
                foreach (SatisReadModel satis in bayiSatislari)
                {
                    if (!satis.Miktar.HasValue || !satis.BirimFiyat.HasValue || !satis.IskontoOrani.HasValue)
                        continue;

                    urunByCode.TryGetValue(satis.UrunKodu, out UrunReadModel? urun);
                    salesLines.Add(new SalesMetricLine(
                        SatisId: satis.SatisId,
                        Miktar: satis.Miktar.Value,
                        BirimFiyat: satis.BirimFiyat.Value,
                        IskontoOrani: satis.IskontoOrani.Value,
                        BirimMaliyet: urun?.BirimMaliyet,
                        IsIade: satis.IsIade ?? false));
                }

                hedefByBayi.TryGetValue(bayi.BayiKodu, out AylikHedefReadModel? hedef);
                decimal? hedefCiro = hedef?.Hedef;

                SalesMetricsResult salesMetrics = _salesMetricsCalculator.Calculate(salesLines, hedefCiro);
                int kritikStokUrunSayisi = CalculateCriticalStockCount(bayiSatislari, stokByUrunDepo, satisHizlari);
                RiskEngineResult risk = _riskEngine.Calculate(salesMetrics, kritikStokUrunSayisi);

                riskItems.Add(new RiskListesiItemOutput(
                    BayiKodu: bayi.BayiKodu,
                    Segment: bayi.Segment ?? string.Empty,
                    Bolge: bayi.Bolge ?? string.Empty,
                    HedefeUlasmaPct: salesMetrics.HedefeUlasmaPct,
                    OrtalamaMarjPct: salesMetrics.OrtalamaMarjPct,
                    OrtalamaIskontoPct: salesMetrics.OrtalamaIskontoPct,
                    RiskSkoru: risk.RiskSkoru,
                    RiskSeviyesi: risk.RiskSeviyesi,
                    GerekceKodlari: risk.GerekceKodlari,
                    Sehir: bayi.Sehir,
                    BayiAdi: bayi.BayiAdi,
                    GerceklesenCiro: salesMetrics.GerceklesenCiro,
                    HedefCiro: hedefCiro,
                    KritikStokVarMi: kritikStokUrunSayisi > 0));
            }
        }

        // 8. İLERİ SEVİYE ÇOK BOYUTLU FİLTRELEME
        IEnumerable<RiskListesiItemOutput> filteredQuery = riskItems;

        HashSet<string>? riskSeviyeleriSet = ParseCommaSeparated(query.RiskSeviyesi);
        if (riskSeviyeleriSet is not null)
        {
            filteredQuery = filteredQuery.Where(item =>
                riskSeviyeleriSet.Contains(item.RiskSeviyesi.ToUpperInvariant()));
        }

        if (query.MinRiskSkoru.HasValue)
        {
            filteredQuery = filteredQuery.Where(item => item.RiskSkoru >= query.MinRiskSkoru.Value);
        }

        if (query.MaxRiskSkoru.HasValue)
        {
            filteredQuery = filteredQuery.Where(item => item.RiskSkoru <= query.MaxRiskSkoru.Value);
        }

        HashSet<string>? gerekceKodlariSet = ParseCommaSeparated(query.GerekceKodu);
        if (gerekceKodlariSet is not null)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.GerekceKodlari.Any(code => gerekceKodlariSet.Contains(code.ToUpperInvariant())));
        }

        if (query.MinHedefeUlasmaPct.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.HedefeUlasmaPct.HasValue && item.HedefeUlasmaPct.Value >= query.MinHedefeUlasmaPct.Value);
        }

        if (query.MaxHedefeUlasmaPct.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.HedefeUlasmaPct.HasValue && item.HedefeUlasmaPct.Value <= query.MaxHedefeUlasmaPct.Value);
        }

        if (query.MinMarjPct.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.OrtalamaMarjPct.HasValue && item.OrtalamaMarjPct.Value >= query.MinMarjPct.Value);
        }

        if (query.MaxMarjPct.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.OrtalamaMarjPct.HasValue && item.OrtalamaMarjPct.Value <= query.MaxMarjPct.Value);
        }

        if (query.MinIskontoPct.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.OrtalamaIskontoPct.HasValue && item.OrtalamaIskontoPct.Value >= query.MinIskontoPct.Value);
        }

        if (query.MaxIskontoPct.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.OrtalamaIskontoPct.HasValue && item.OrtalamaIskontoPct.Value <= query.MaxIskontoPct.Value);
        }

        if (query.MinCiro.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.GerceklesenCiro.HasValue && item.GerceklesenCiro.Value >= query.MinCiro.Value);
        }

        if (query.MaxCiro.HasValue)
        {
            filteredQuery = filteredQuery.Where(item =>
                item.GerceklesenCiro.HasValue && item.GerceklesenCiro.Value <= query.MaxCiro.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.AktiviteDurumu))
        {
            var status = query.AktiviteDurumu.Trim().ToUpperInvariant();
            if (status == "AKTIF")
            {
                filteredQuery = filteredQuery.Where(item => (item.GerceklesenCiro ?? 0m) > 0m);
            }
            else if (status == "PASIF")
            {
                filteredQuery = filteredQuery.Where(item => (item.GerceklesenCiro ?? 0m) == 0m);
            }
        }

        if (query.KritikStokVarMi.HasValue)
        {
            filteredQuery = filteredQuery.Where(item => item.KritikStokVarMi == query.KritikStokVarMi.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Arama))
        {
            var searchTerm = query.Arama.Trim();
            filteredQuery = filteredQuery.Where(item =>
                item.BayiKodu.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(item.BayiAdi) && item.BayiAdi.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                || item.Segment.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || item.Bolge.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(item.Sehir) && item.Sehir.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filteredQuery.ToList();
        int totalFilteredCount = filteredList.Count;

        // 9. DETERMINİSTİK SIRALAMA
        bool isAscending = string.Equals(query.SiralaYon, "asc", StringComparison.OrdinalIgnoreCase);
        var sortField = query.SiralaAlan?.Trim().ToLowerInvariant();

        IEnumerable<RiskListesiItemOutput> sortedList = sortField switch
        {
            "hedefe_ulasma_pct" => isAscending
                ? filteredList.OrderBy(x => x.HedefeUlasmaPct ?? decimal.MaxValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.HedefeUlasmaPct ?? decimal.MinValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal),

            "ortalama_marj_pct" => isAscending
                ? filteredList.OrderBy(x => x.OrtalamaMarjPct ?? decimal.MaxValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.OrtalamaMarjPct ?? decimal.MinValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal),

            "ortalama_iskonto_pct" => isAscending
                ? filteredList.OrderBy(x => x.OrtalamaIskontoPct ?? decimal.MaxValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.OrtalamaIskontoPct ?? decimal.MinValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal),

            "ciro" or "gerceklesen_ciro" => isAscending
                ? filteredList.OrderBy(x => x.GerceklesenCiro ?? decimal.MaxValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.GerceklesenCiro ?? decimal.MinValue).ThenBy(x => x.BayiKodu, StringComparer.Ordinal),

            "bayi_kodu" => isAscending
                ? filteredList.OrderBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.BayiKodu, StringComparer.Ordinal),

            "sehir" => isAscending
                ? filteredList.OrderBy(x => x.Sehir ?? string.Empty, StringComparer.Ordinal).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.Sehir ?? string.Empty, StringComparer.Ordinal).ThenBy(x => x.BayiKodu, StringComparer.Ordinal),

            _ => isAscending
                ? filteredList.OrderBy(x => x.RiskSkoru).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.RiskSkoru).ThenBy(x => x.BayiKodu, StringComparer.Ordinal)
        };

        // 10. SAYFALAMA (PAGINATION)
        var resultItems = sortedList.ToList();

        if (query.SayfaBoyutu.HasValue || (query.Sayfa.HasValue && query.Sayfa.Value > 1))
        {
            int pageNumber = query.Sayfa.GetValueOrDefault(1);
            if (pageNumber < 1) pageNumber = 1;
            int pageSize = query.SayfaBoyutu.GetValueOrDefault(1000);
            if (pageSize < 1) pageSize = 1000;

            resultItems = resultItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        // 11. OUTPUT
        RiskListesiOutput output = new(
            Donem: periodStart,
            Bayiler: resultItems,
            ToplamKayitSayisi: totalFilteredCount,
            Sayfa: query.Sayfa,
            SayfaBoyutu: query.SayfaBoyutu);

        return ServiceResult<RiskListesiOutput>.Success(output);
    }

    private int CalculateCriticalStockCount(
        IReadOnlyList<SatisReadModel> satislar,
        IReadOnlyDictionary<(string UrunKodu, string DepoKodu), StokReadModel> stokByUrunDepo,
        IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal> satisHizlari)
    {
        var productWarehousePairs = satislar
            .Select(satis => new { satis.UrunKodu, satis.DepoKodu })
            .Where(pair => !string.IsNullOrWhiteSpace(pair.UrunKodu) && !string.IsNullOrWhiteSpace(pair.DepoKodu))
            .Distinct()
            .ToArray();

        int criticalCount = 0;

        foreach (var pair in productWarehousePairs)
        {
            var key = (pair.UrunKodu!, pair.DepoKodu!);
            if (!stokByUrunDepo.TryGetValue(key, out StokReadModel? stock))
                continue;

            decimal sonUcAySatisAdedi = satisHizlari.TryGetValue(key, out decimal satisAdedi)
                ? satisAdedi
                : 0m;

            StockMetricsResult stockMetrics = _stockMetricsCalculator.Calculate(stock, sonUcAySatisAdedi);
            if (stockMetrics.KritikMi)
            {
                criticalCount++;
            }
        }

        return criticalCount;
    }

    private static HashSet<string>? ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var set = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return set.Count > 0 ? set : null;
    }
}
