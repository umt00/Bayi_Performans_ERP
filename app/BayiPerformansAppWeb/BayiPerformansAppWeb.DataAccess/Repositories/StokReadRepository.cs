using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Persistence;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class StokReadRepository(PentaDbContext dbContext)
    : IStokReadRepository
{
    public async Task<IReadOnlyList<StokReadModel>> ListAsync(
        string? depoKodu,
        string? urunKodu,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Stoklar
            .Where(row => row.UrunKodu != null && row.DepoKodu != null);

        if (!string.IsNullOrWhiteSpace(depoKodu))
        {
            query = query.Where(row => row.DepoKodu == depoKodu);
        }

        if (!string.IsNullOrWhiteSpace(urunKodu))
        {
            query = query.Where(row => row.UrunKodu == urunKodu);
        }

        var rows = await query
            .OrderBy(row => row.DepoKodu)
            .ThenBy(row => row.UrunKodu)
            .ToListAsync(cancellationToken);

        return rows
            .Select(Map)
            .OfType<StokReadModel>()
            .ToArray();
    }

    public async Task<StokReadModel?> GetAsync(
        string urunKodu,
        string depoKodu,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Stoklar
            .Where(item =>
                item.UrunKodu == urunKodu
                && item.DepoKodu == depoKodu)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : Map(row);
    }

    private static StokReadModel? Map(StokRow row)
    {
        if (string.IsNullOrWhiteSpace(row.UrunKodu)
            || string.IsNullOrWhiteSpace(row.DepoKodu))
        {
            return null;
        }

        return new StokReadModel(
            row.UrunKodu,
            row.DepoKodu,
            row.MevcutStok,
            row.MinStokSeviyesi,
            row.SonSayimTarihi);
    }
}
