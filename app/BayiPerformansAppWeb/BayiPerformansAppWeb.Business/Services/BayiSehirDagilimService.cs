using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

public sealed class BayiSehirDagilimService : IBayiSehirDagilimService
{
    private readonly IBayiDonemPerformansReadRepository _performansRepository;

    public BayiSehirDagilimService(IBayiDonemPerformansReadRepository performansRepository)
    {
        ArgumentNullException.ThrowIfNull(performansRepository);
        _performansRepository = performansRepository;
    }

    public async Task<ServiceResult<BayiSehirDagilimiOutput>> GetirSehirDagilimiAsync(
        BayiSehirDagilimiQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dbQuery = new BayiDonemPerformansQuery(
            Donem: query.Donem,
            RiskSeviyesi: null,
            Bolge: query.Bolge,
            Segment: query.Segment,
            Sayfa: 1,
            SayfaBoyutu: 10000);

        var page = await _performansRepository.ListAsync(dbQuery, cancellationToken);
        if (!page.Donem.HasValue || page.Bayiler.Count == 0)
        {
            return ServiceResult<BayiSehirDagilimiOutput>.Fail(
                ServiceErrorKind.NotFound,
                "PERFORMANS_VERISI_YOK",
                "Döneme ait bayi performans verisi bulunamadı.");
        }

        var cityGroups = page.Bayiler
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Sehir) ? "Belirtilmemiş" : d.Sehir.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var bolge = g.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Bolge))?.Bolge ?? string.Empty;
                var totalCount = g.Count();
                var activeCount = g.Count(d => (d.Ciro ?? 0m) > 0m);
                var totalRevenue = g.Sum(d => d.Ciro ?? 0m);
                var highRisk = g.Count(d => string.Equals(d.RiskSeviyesi, "YUKSEK", StringComparison.OrdinalIgnoreCase));
                var midRisk = g.Count(d => string.Equals(d.RiskSeviyesi, "ORTA", StringComparison.OrdinalIgnoreCase));
                var lowRisk = g.Count(d => string.Equals(d.RiskSeviyesi, "DUSUK", StringComparison.OrdinalIgnoreCase));

                var dealers = g
                    .OrderBy(d => d.BayiKodu, StringComparer.Ordinal)
                    .Select(d => new BayiSehirDetayItemOutput(
                        BayiKodu: d.BayiKodu,
                        BayiAdi: d.BayiAdi,
                        Segment: d.Segment ?? string.Empty,
                        GerceklesenCiro: d.Ciro ?? 0m,
                        RiskSkoru: d.RiskSkoru ?? 0,
                        RiskSeviyesi: d.RiskSeviyesi ?? "DUSUK"))
                    .ToList();

                return new BayiSehirItemOutput(
                    Sehir: g.Key,
                    Bolge: bolge,
                    ToplamBayiSayisi: totalCount,
                    AktifBayiSayisi: activeCount,
                    ToplamCiro: totalRevenue,
                    YuksekRiskliBayiSayisi: highRisk,
                    OrtaRiskliBayiSayisi: midRisk,
                    DusukRiskliBayiSayisi: lowRisk,
                    Bayiler: dealers);
            })
            .ToList();

        return ServiceResult<BayiSehirDagilimiOutput>.Success(
            new BayiSehirDagilimiOutput(
                Donem: page.Donem.Value,
                ToplamSehirSayisi: cityGroups.Count,
                ToplamBayiSayisi: cityGroups.Sum(c => c.ToplamBayiSayisi),
                Sehirler: cityGroups));
    }

    public async Task<ServiceResult<BayiBolgeDagilimiOutput>> GetirBolgeDagilimiAsync(
        BayiBolgeDagilimiQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var dbQuery = new BayiDonemPerformansQuery(
            Donem: query.Donem,
            RiskSeviyesi: null,
            Bolge: null,
            Segment: null,
            Sayfa: 1,
            SayfaBoyutu: 10000);

        var page = await _performansRepository.ListAsync(dbQuery, cancellationToken);
        if (!page.Donem.HasValue || page.Bayiler.Count == 0)
        {
            return ServiceResult<BayiBolgeDagilimiOutput>.Fail(
                ServiceErrorKind.NotFound,
                "PERFORMANS_VERISI_YOK",
                "Döneme ait bayi performans verisi bulunamadı.");
        }

        var regionGroups = page.Bayiler
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Bolge) ? "Belirtilmemiş" : d.Bolge.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var cities = g
                    .Select(d => string.IsNullOrWhiteSpace(d.Sehir) ? "Belirtilmemiş" : d.Sehir.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var totalCount = g.Count();
                var activeCount = g.Count(d => (d.Ciro ?? 0m) > 0m);
                var totalRevenue = g.Sum(d => d.Ciro ?? 0m);
                var targetDealers = g.Where(d => d.Hedef.HasValue && d.Hedef.Value > 0m).ToList();
                var totalTarget = targetDealers.Sum(d => d.Hedef!.Value);
                var targetRevenue = targetDealers.Sum(d => d.Ciro ?? 0m);

                decimal? hedefeUlasmaPct = totalTarget > 0m
                    ? Math.Round(targetRevenue / totalTarget, 4, MidpointRounding.AwayFromZero)
                    : null;

                var highRisk = g.Count(d => string.Equals(d.RiskSeviyesi, "YUKSEK", StringComparison.OrdinalIgnoreCase));
                var midRisk = g.Count(d => string.Equals(d.RiskSeviyesi, "ORTA", StringComparison.OrdinalIgnoreCase));
                var lowRisk = g.Count(d => string.Equals(d.RiskSeviyesi, "DUSUK", StringComparison.OrdinalIgnoreCase));

                return new BayiBolgeItemOutput(
                    Bolge: g.Key,
                    Sehirler: cities,
                    ToplamBayiSayisi: totalCount,
                    AktifBayiSayisi: activeCount,
                    ToplamCiro: totalRevenue,
                    ToplamHedefCiro: totalTarget > 0m ? totalTarget : null,
                    GenelHedefeUlasmaPct: hedefeUlasmaPct,
                    YuksekRiskliBayiSayisi: highRisk,
                    OrtaRiskliBayiSayisi: midRisk,
                    DusukRiskliBayiSayisi: lowRisk);
            })
            .ToList();

        return ServiceResult<BayiBolgeDagilimiOutput>.Success(
            new BayiBolgeDagilimiOutput(
                Donem: page.Donem.Value,
                ToplamBolgeSayisi: regionGroups.Count,
                ToplamBayiSayisi: regionGroups.Sum(r => r.ToplamBayiSayisi),
                Bolgeler: regionGroups));
    }
}
