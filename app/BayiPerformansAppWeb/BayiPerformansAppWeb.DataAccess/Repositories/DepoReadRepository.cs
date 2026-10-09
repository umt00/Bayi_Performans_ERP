using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class DepoReadRepository(PentaDbContext dbContext)
    : IDepoReadRepository
{
    public Task<DepoReadModel?> GetByCodeAsync(
        string depoKodu,
        CancellationToken cancellationToken)
    {
        return dbContext.Depolar
            .Where(row => row.DepoKodu == depoKodu)
            .Select(row => new DepoReadModel(
                row.DepoKodu!,
                row.Bolge,
                row.DepoAdi,
                row.Sehir))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DepoReadModel>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Depolar
            .Where(row => row.DepoKodu != null)
            .OrderBy(row => row.DepoKodu)
            .Select(row => new DepoReadModel(
                row.DepoKodu!,
                row.Bolge,
                row.DepoAdi,
                row.Sehir))
            .ToListAsync(cancellationToken);
    }
}
