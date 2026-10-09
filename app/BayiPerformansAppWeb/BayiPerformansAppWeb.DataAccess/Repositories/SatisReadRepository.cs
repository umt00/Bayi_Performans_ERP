using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Persistence;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class SatisReadRepository(PentaDbContext dbContext)
    : ISatisReadRepository
{
    public async Task<DateOnly?> GetLatestSaleDateAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Satislar
            .Where(row => row.Tarih != null)
            .MaxAsync(row => row.Tarih, cancellationToken);
    }

    public async Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(
        CancellationToken cancellationToken)
    {
        var dates = await dbContext.Satislar
            .Where(row => row.Tarih != null)
            .Select(row => new DateOnly(row.Tarih!.Value.Year, row.Tarih.Value.Month, 1))
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(cancellationToken);

        return dates;
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            QueryPeriod(periodStart, periodEndExclusive),
            cancellationToken);
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(
        string bayiKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var query = QueryPeriod(periodStart, periodEndExclusive)
            .Where(row => row.BayiKodu == bayiKodu);

        return ExecuteAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(
        string urunKodu,
        string depoKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var query = QueryPeriod(periodStart, periodEndExclusive)
            .Where(row =>
                row.UrunKodu == urunKodu
                && row.DepoKodu == depoKodu);

        return ExecuteAsync(query, cancellationToken);
    }

    public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesByPeriodAsync(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        return ExecuteAggregationAsync(
            QueryPeriod(periodStart, periodEndExclusive),
            cancellationToken);
    }

    public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesForDealerAsync(
            string bayiKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bayiKodu);

        var query = QueryPeriod(periodStart, periodEndExclusive)
            .Where(row => row.BayiKodu == bayiKodu);

        return ExecuteAggregationAsync(query, cancellationToken);
    }

    private IQueryable<SatisRow> QueryPeriod(
        DateOnly periodStart,
        DateOnly periodEndExclusive)
    {
        if (periodEndExclusive <= periodStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periodEndExclusive),
                "Period end must be later than period start.");
        }

        return dbContext.Satislar.Where(row =>
            row.Tarih != null
            && row.Tarih >= periodStart
            && row.Tarih < periodEndExclusive);
    }

    private static async Task<IReadOnlyList<SatisReadModel>> ExecuteAsync(
        IQueryable<SatisRow> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .OrderBy(row => row.Tarih)
            .ThenBy(row => row.SatisId)
            .ToListAsync(cancellationToken);

        return rows
            .Select(Map)
            .OfType<SatisReadModel>()
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        ExecuteAggregationAsync(
            IQueryable<SatisRow> query,
            CancellationToken cancellationToken)
    {
        // İade işareti bellekte uygulanır; SQL tarafında yalnız GroupBy + Sum kalır.
        // Bu, Npgsql/EF çevirisini sade tutarken mevcut iade davranışını korur.
        var groupedRows = await query
            .Where(row =>
                row.UrunKodu != null
                && row.DepoKodu != null
                && row.Miktar != null)
            .GroupBy(row => new
            {
                row.UrunKodu,
                row.DepoKodu,
                row.IsIade
            })
            .Select(group => new
            {
                UrunKodu = group.Key.UrunKodu!,
                DepoKodu = group.Key.DepoKodu!,
                group.Key.IsIade,
                TotalMiktar = group.Sum(row => row.Miktar)
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<(string UrunKodu, string DepoKodu), decimal>();

        foreach (var row in groupedRows)
        {
            decimal miktar = row.TotalMiktar ?? 0m;
            decimal signedQuantity = row.IsIade == true
                ? -Math.Abs(miktar)
                : miktar;
            var key = (row.UrunKodu, row.DepoKodu);

            result[key] = result.TryGetValue(key, out decimal current)
                ? current + signedQuantity
                : signedQuantity;
        }

        return result;
    }

    private static SatisReadModel? Map(SatisRow row)
    {
        if (row.Tarih is null
            || string.IsNullOrWhiteSpace(row.BayiKodu)
            || string.IsNullOrWhiteSpace(row.UrunKodu)
            || string.IsNullOrWhiteSpace(row.DepoKodu))
        {
            return null;
        }

        return new SatisReadModel(
            row.SatisId,
            row.Tarih.Value,
            row.BayiKodu,
            row.UrunKodu,
            row.DepoKodu,
            row.Miktar,
            row.BirimFiyat,
            row.IskontoOrani,
            row.SatisKanali,
            row.IsIade);
    }
}
