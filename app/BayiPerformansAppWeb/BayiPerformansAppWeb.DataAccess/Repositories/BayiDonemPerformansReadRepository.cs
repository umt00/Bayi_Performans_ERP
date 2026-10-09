using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Persistence;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// Data/Automation ekibinin aylık olarak doldurduğu bayi_donem_performans
/// tablosunu salt okunur biçimde sunar. Segment, bölge ve bayi adı master
/// bayiler tablosundan alınır; API hiçbir metriği yeniden hesaplamaz.
/// </summary>
internal sealed class BayiDonemPerformansReadRepository(PentaDbContext dbContext)
    : IBayiDonemPerformansReadRepository
{
    public async Task<BayiDonemPerformansPage> ListAsync(
        BayiDonemPerformansQuery query,
        CancellationToken cancellationToken)
    {
        DateOnly? effectivePeriod = query.Donem
            ?? await GetLatestPeriodAsync(null, cancellationToken);

        if (!effectivePeriod.HasValue)
        {
            return new BayiDonemPerformansPage(
                null,
                0,
                query.Sayfa,
                query.SayfaBoyutu,
                []);
        }

        IQueryable<JoinedPerformanceRow> rows = CreateJoinedQuery()
            .Where(item => item.Performans.Donem == effectivePeriod.Value);

        if (!string.IsNullOrWhiteSpace(query.RiskSeviyesi))
        {
            string riskSeviyesi = query.RiskSeviyesi.Trim().ToUpperInvariant();
            rows = rows.Where(item => item.Performans.RiskSeviyesi == riskSeviyesi);
        }

        if (!string.IsNullOrWhiteSpace(query.Bolge))
        {
            string bolge = query.Bolge.Trim().ToUpperInvariant();
            rows = rows.Where(item =>
                item.Bayi != null
                && item.Bayi.Bolge != null
                && item.Bayi.Bolge.ToUpper() == bolge);
        }

        if (!string.IsNullOrWhiteSpace(query.Segment))
        {
            string segment = query.Segment.Trim().ToUpperInvariant();
            rows = rows.Where(item =>
                item.Bayi != null
                && item.Bayi.Segment != null
                && item.Bayi.Segment.ToUpper() == segment);
        }

        if (!string.IsNullOrWhiteSpace(query.Sehir))
        {
            string sehir = query.Sehir.Trim().ToUpperInvariant();
            rows = rows.Where(item =>
                item.Bayi != null
                && item.Bayi.Sehir != null
                && item.Bayi.Sehir.ToUpper() == sehir);
        }

        int total = await rows.CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(item => item.Performans.RiskSkoru)
            .ThenBy(item => item.Performans.BayiKodu)
            .Skip((query.Sayfa - 1) * query.SayfaBoyutu)
            .Take(query.SayfaBoyutu)
            .Select(ToReadModelExpression())
            .ToListAsync(cancellationToken);

        return new BayiDonemPerformansPage(
            effectivePeriod,
            total,
            query.Sayfa,
            query.SayfaBoyutu,
            items);
    }

    public async Task<BayiDonemPerformansReadModel?> GetAsync(
        string bayiKodu,
        DateOnly? donem,
        CancellationToken cancellationToken)
    {
        DateOnly? effectivePeriod = donem
            ?? await GetLatestPeriodAsync(bayiKodu, cancellationToken);

        if (!effectivePeriod.HasValue)
        {
            return null;
        }

        return await CreateJoinedQuery()
            .Where(item =>
                item.Performans.BayiKodu == bayiKodu
                && item.Performans.Donem == effectivePeriod.Value)
            .Select(ToReadModelExpression())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PerformansDashboardStatsReadModel> GetDashboardStatsAsync(
        DateOnly? donem,
        CancellationToken cancellationToken)
    {
        DateOnly? effectivePeriod = donem
            ?? await GetLatestPeriodAsync(null, cancellationToken);

        if (!effectivePeriod.HasValue)
        {
            return new PerformansDashboardStatsReadModel(
                null, 0, 0, 0, 0, 0, null, null, null);
        }

        var rows = dbContext.BayiDonemPerformanslari
            .AsNoTracking()
            .Where(row => row.Donem == effectivePeriod.Value);

        int total = await rows.CountAsync(cancellationToken);
        decimal toplamCiro = await rows.SumAsync(
            row => row.Ciro ?? 0m,
            cancellationToken);
        decimal toplamHedef = await rows.SumAsync(
            row => row.Hedef ?? 0m,
            cancellationToken);
        int yuksekRiskli = await rows.CountAsync(
            row => row.RiskSeviyesi == "YUKSEK",
            cancellationToken);
        int kritikStoklu = await rows.CountAsync(
            row => (row.KritikStokKalanUrunSayisi ?? 0) > 0,
            cancellationToken);
        decimal? hedefOrtalamasi = await rows
            .Where(row => row.HedefTutmaOrani != null)
            .AverageAsync(row => row.HedefTutmaOrani, cancellationToken);
        decimal? marjOrtalamasi = await rows
            .Where(row => row.OrtalamaKarMarji != null)
            .AverageAsync(row => row.OrtalamaKarMarji, cancellationToken);
        decimal? riskOrtalamasi = await rows
            .Where(row => row.RiskSkoru != null)
            .AverageAsync(row => (decimal?)row.RiskSkoru, cancellationToken);

        return new PerformansDashboardStatsReadModel(
            effectivePeriod,
            total,
            toplamCiro,
            toplamHedef,
            yuksekRiskli,
            kritikStoklu,
            hedefOrtalamasi,
            marjOrtalamasi,
            riskOrtalamasi);
    }

    public async Task<IReadOnlyList<BayiDonemPerformansReadModel>> GetHistoryAsync(
        string bayiKodu,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = await CreateJoinedQuery()
            .Where(item => item.Performans.BayiKodu == bayiKodu)
            .OrderByDescending(item => item.Performans.Donem)
            .Take(limit)
            .Select(ToReadModelExpression())
            .ToListAsync(cancellationToken);

        items.Reverse();
        return items;
    }

    private IQueryable<JoinedPerformanceRow> CreateJoinedQuery()
    {
        return
            from performans in dbContext.BayiDonemPerformanslari.AsNoTracking()
            join bayi in dbContext.Bayiler.AsNoTracking()
                on performans.BayiKodu equals bayi.BayiKodu into bayiGroup
            from bayi in bayiGroup.DefaultIfEmpty()
            select new JoinedPerformanceRow
            {
                Performans = performans,
                Bayi = bayi
            };
    }

    private Task<DateOnly?> GetLatestPeriodAsync(
        string? bayiKodu,
        CancellationToken cancellationToken)
    {
        var rows = dbContext.BayiDonemPerformanslari.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(bayiKodu))
        {
            rows = rows.Where(row => row.BayiKodu == bayiKodu);
        }

        return rows
            .Select(row => (DateOnly?)row.Donem)
            .MaxAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<
        Func<JoinedPerformanceRow, BayiDonemPerformansReadModel>>
        ToReadModelExpression() =>
        item => new BayiDonemPerformansReadModel(
            item.Performans.BayiKodu,
            item.Performans.Donem,
            item.Bayi == null ? null : item.Bayi.BayiAdi,
            item.Bayi == null ? null : item.Bayi.Segment,
            item.Bayi == null ? null : item.Bayi.Bolge,
            item.Performans.Ciro,
            item.Performans.Hedef,
            item.Performans.HedefTutmaOrani,
            item.Performans.OrtalamaKarMarji,
            item.Performans.OrtalamaIskontoOrani,
            item.Performans.OrtalamaIadeOrani,
            item.Performans.KritikStokKalanUrunSayisi,
            item.Performans.RiskSkoru,
            item.Performans.RiskSeviyesi,
            item.Performans.GerekceKodlari,
            item.Bayi == null ? null : item.Bayi.Sehir);

    private sealed class JoinedPerformanceRow
    {
        public required BayiDonemPerformansRow Performans { get; init; }
        public BayiRow? Bayi { get; init; }
    }
}
