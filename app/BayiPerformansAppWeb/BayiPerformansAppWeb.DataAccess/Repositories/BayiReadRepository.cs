using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class BayiReadRepository(PentaDbContext dbContext)
    : IBayiReadRepository
{
    public Task<BayiReadModel?> GetByCodeAsync(
        string bayiKodu,
        CancellationToken cancellationToken)
    {
        return dbContext.Bayiler
            .Where(row => row.BayiKodu == bayiKodu)
            .Select(row => new BayiReadModel(
                row.BayiKodu!,
                row.Segment,
                row.Bolge,
                row.Sehir,
                row.BayiAdi))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BayiReadModel>> ListAsync(
        string? segment,
        string? bolge,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Bayiler
            .Where(row => row.BayiKodu != null);

        if (!string.IsNullOrWhiteSpace(segment))
        {
            var normalizedSegment = segment.Trim().ToUpperInvariant();
            query = query.Where(row =>
                row.Segment != null
                && row.Segment.ToUpper() == normalizedSegment);
        }

        if (!string.IsNullOrWhiteSpace(bolge))
        {
            var normalizedBolge = bolge.Trim().ToUpperInvariant();
            query = query.Where(row =>
                row.Bolge != null
                && row.Bolge.ToUpper() == normalizedBolge);
        }

        return await query
            .OrderBy(row => row.BayiKodu)
            .Select(row => new BayiReadModel(
                row.BayiKodu!,
                row.Segment,
                row.Bolge,
                row.Sehir,
                row.BayiAdi))
            .ToListAsync(cancellationToken);
    }
}
