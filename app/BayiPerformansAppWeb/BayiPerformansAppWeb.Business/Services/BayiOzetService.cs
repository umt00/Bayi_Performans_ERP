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
/// Admin paneli yönetici özeti ve genel bayi KPI metriklerini hesaplar.
/// </summary>
public sealed class BayiOzetService : IBayiOzetService
{
    private readonly IBayiReadRepository _bayiRepository;
    private readonly ISatisReadRepository _satisRepository;
    private readonly IAylikHedefReadRepository _hedefRepository;
    private readonly IUrunReadRepository _urunRepository;
    private readonly IStokReadRepository _stokRepository;
    private readonly SalesMetricsCalculator _salesMetricsCalculator;
    private readonly StockMetricsCalculator _stockMetricsCalculator;
    private readonly RiskEngine _riskEngine;

    public BayiOzetService(
        IBayiReadRepository bayiRepository,
        ISatisReadRepository satisRepository,
        IAylikHedefReadRepository hedefRepository,
        IUrunReadRepository urunRepository,
        IStokReadRepository stokRepository,
        SalesMetricsCalculator salesMetricsCalculator,
        StockMetricsCalculator stockMetricsCalculator,
        RiskEngine riskEngine)
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
    }

    public async Task<ServiceResult<BayilerOzetOutput>> GetirOzetAsync(
        BayilerOzetQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

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
                return ServiceResult<BayilerOzetOutput>.Fail(
                    ServiceErrorKind.NotFound,
                    "SATIS_VERISI_YOK",
                    "Varsayılan dönemi belirlemek için satış tarihi bulunamadı.");
            }

            donemTarihi = latestSaleDate.Value;
        }

        DateOnly periodStart = new(donemTarihi.Year, donemTarihi.Month, 1);
        DateOnly periodEndExclusive = periodStart.AddMonths(1);

        IReadOnlyList<BayiReadModel> bayiler = await _bayiRepository.ListAsync(
            query.Segment,
            query.Bolge,
            cancellationToken);

        if (bayiler.Count == 0)
        {
            return ServiceResult<BayilerOzetOutput>.Success(
                new BayilerOzetOutput(
                    Donem: periodStart,
                    ToplamBayiSayisi: 0,
                    AktifSatisYapanBayiSayisi: 0,
                    ToplamGerceklesenCiro: 0m,
                    ToplamHedefCiro: null,
                    GenelHedefeUlasmaPct: null,
                    GenelOrtalamaMarjPct: null,
                    GenelOrtalamaIskontoPct: null,
                    YuksekRiskliBayiSayisi: 0,
                    OrtaRiskliBayiSayisi: 0,
                    DusukRiskliBayiSayisi: 0,
                    KritikStokEtkilenenBayiSayisi: 0,
                    GerekceKoduDagilimi: new Dictionary<string, int>(StringComparer.Ordinal)));
        }

        IReadOnlyList<SatisReadModel> satislar = await _satisRepository.ListByPeriodAsync(
            periodStart,
            periodEndExclusive,
            cancellationToken);

        string[] urunKodlari = satislar
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

        Dictionary<string, List<SatisReadModel>> satisByBayi = satislar
            .Where(s => !string.IsNullOrWhiteSpace(s.BayiKodu))
            .GroupBy(s => s.BayiKodu, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        IReadOnlyList<AylikHedefReadModel> hedefler = await _hedefRepository.ListByPeriodAsync(
            periodStart.Year,
            periodStart.Month,
            cancellationToken);

        Dictionary<string, AylikHedefReadModel> hedefByBayi = hedefler.ToDictionary(
            h => h.BayiKodu,
            StringComparer.Ordinal);

        DateOnly historyStart = periodStart.AddDays(-89);
        DateOnly historyEndExclusive = periodStart.AddDays(1);

        IReadOnlyList<StokReadModel> tumStoklar = await _stokRepository.ListAsync(null, null, cancellationToken);
        Dictionary<(string UrunKodu, string DepoKodu), StokReadModel> stokByUrunDepo = tumStoklar
            .Where(s => !string.IsNullOrWhiteSpace(s.UrunKodu) && !string.IsNullOrWhiteSpace(s.DepoKodu))
            .GroupBy(s => (s.UrunKodu, s.DepoKodu))
            .ToDictionary(g => g.Key, g => g.First());

        IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal> satisHizlari =
            await _satisRepository.GetTotalSalesQuantitiesByPeriodAsync(
                historyStart,
                historyEndExclusive,
                cancellationToken);

        decimal toplamCiro = 0m;
        decimal toplamHedefliGerceklesenCiro = 0m;
        decimal? toplamHedef = null;
        int aktifSatisYapanBayi = 0;
        int yuksekRisk = 0;
        int ortaRisk = 0;
        int dusukRisk = 0;
        int kritikStokEtkilenen = 0;
        Dictionary<string, int> gerekceDagilimi = new(StringComparer.Ordinal);

        List<decimal> marjListesi = new();
        List<decimal> iskontoListesi = new();

        foreach (var bayi in bayiler)
        {
            satisByBayi.TryGetValue(bayi.BayiKodu, out var bayiSatislari);
            bayiSatislari ??= new List<SatisReadModel>();

            if (bayiSatislari.Count > 0)
            {
                aktifSatisYapanBayi++;
            }

            List<SalesMetricLine> salesLines = new();
            foreach (var satis in bayiSatislari)
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

            hedefByBayi.TryGetValue(bayi.BayiKodu, out var hedef);
            decimal? hedefCiro = hedef?.Hedef;

            if (hedefCiro.HasValue)
            {
                toplamHedef = (toplamHedef ?? 0m) + hedefCiro.Value;
            }

            SalesMetricsResult salesMetrics = _salesMetricsCalculator.Calculate(salesLines, hedefCiro);
            toplamCiro += salesMetrics.GerceklesenCiro;

            if (hedefCiro.HasValue)
            {
                toplamHedefliGerceklesenCiro += salesMetrics.GerceklesenCiro;
            }

            if (salesMetrics.OrtalamaMarjPct.HasValue)
            {
                marjListesi.Add(salesMetrics.OrtalamaMarjPct.Value);
            }

            if (salesMetrics.OrtalamaIskontoPct.HasValue)
            {
                iskontoListesi.Add(salesMetrics.OrtalamaIskontoPct.Value);
            }

            int kritikStokSayisi = 0;
            var productWarehousePairs = bayiSatislari
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

            if (kritikStokSayisi > 0)
            {
                kritikStokEtkilenen++;
            }

            RiskEngineResult risk = _riskEngine.Calculate(salesMetrics, kritikStokSayisi);

            if (string.Equals(risk.RiskSeviyesi, "YUKSEK", StringComparison.OrdinalIgnoreCase))
            {
                yuksekRisk++;
            }
            else if (string.Equals(risk.RiskSeviyesi, "ORTA", StringComparison.OrdinalIgnoreCase))
            {
                ortaRisk++;
            }
            else
            {
                dusukRisk++;
            }

            foreach (var kod in risk.GerekceKodlari)
            {
                gerekceDagilimi[kod] = gerekceDagilimi.TryGetValue(kod, out int c) ? c + 1 : 1;
            }
        }

        decimal? genelHedefPct = (toplamHedef.HasValue && toplamHedef.Value > 0)
            ? Math.Round(toplamHedefliGerceklesenCiro / toplamHedef.Value, 4)
            : null;

        decimal? genelMarjPct = marjListesi.Count > 0
            ? Math.Round(marjListesi.Average(), 4)
            : null;

        decimal? genelIskontoPct = iskontoListesi.Count > 0
            ? Math.Round(iskontoListesi.Average(), 4)
            : null;

        var output = new BayilerOzetOutput(
            Donem: periodStart,
            ToplamBayiSayisi: bayiler.Count,
            AktifSatisYapanBayiSayisi: aktifSatisYapanBayi,
            ToplamGerceklesenCiro: toplamCiro,
            ToplamHedefCiro: toplamHedef,
            GenelHedefeUlasmaPct: genelHedefPct,
            GenelOrtalamaMarjPct: genelMarjPct,
            GenelOrtalamaIskontoPct: genelIskontoPct,
            YuksekRiskliBayiSayisi: yuksekRisk,
            OrtaRiskliBayiSayisi: ortaRisk,
            DusukRiskliBayiSayisi: dusukRisk,
            KritikStokEtkilenenBayiSayisi: kritikStokEtkilenen,
            GerekceKoduDagilimi: gerekceDagilimi);

        return ServiceResult<BayilerOzetOutput>.Success(output);
    }
}
