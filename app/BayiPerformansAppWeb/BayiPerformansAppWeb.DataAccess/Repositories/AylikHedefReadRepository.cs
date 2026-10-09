using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Persistence;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class AylikHedefReadRepository(PentaDbContext dbContext)
    : IAylikHedefReadRepository
{
    public async Task<AylikHedefReadModel?> GetAsync(
        string bayiKodu,
        int yil,
        int ay,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Hedefler
            .Where(item =>
                item.BayiKodu == bayiKodu
                && item.Yil == yil
                && item.Ay == ay)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<AylikHedefReadModel>> ListByPeriodAsync(
        int yil,
        int ay,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Hedefler
            .Where(row =>
                row.BayiKodu != null
                && row.Yil == yil
                && row.Ay == ay)
            .OrderBy(row => row.BayiKodu)
            .ToListAsync(cancellationToken);

        return rows
            .Select(Map)
            .OfType<AylikHedefReadModel>()
            .ToArray();
    }

    private static AylikHedefReadModel? Map(HedefRow row)
    {
        if (string.IsNullOrWhiteSpace(row.BayiKodu)
            || row.Yil is null
            || row.Ay is null)
        {
            return null;
        }

        return new AylikHedefReadModel(
            row.BayiKodu,
            row.Yil.Value,
            row.Ay.Value,
            row.Hedef);
    }
}
