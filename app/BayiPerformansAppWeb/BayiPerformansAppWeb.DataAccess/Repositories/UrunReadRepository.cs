using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class UrunReadRepository(PentaDbContext dbContext)
    : IUrunReadRepository
{
    public async Task<UrunReadModel?> GetByCodeAsync(
        string urunKodu,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Urunler
            .Where(row => row.UrunKodu == urunKodu)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null || string.IsNullOrWhiteSpace(row.UrunKodu)
            ? null
            : new UrunReadModel(
                row.UrunKodu,
                row.Kategori,
                row.AltKategori,
                row.BirimMaliyet,
                row.UrunAdi,
                row.Marka);
    }

    public async Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(
        IReadOnlyCollection<string> urunKodlari,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = urunKodlari
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return [];
        }

        var rows = await dbContext.Urunler
            .Where(row =>
                row.UrunKodu != null
                && normalizedCodes.Contains(row.UrunKodu))
            .OrderBy(row => row.UrunKodu)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new UrunReadModel(
                row.UrunKodu!,
                row.Kategori,
                row.AltKategori,
                row.BirimMaliyet,
                row.UrunAdi,
                row.Marka))
            .ToArray();
    }

    public async Task<IReadOnlyList<UrunReadModel>> ListAllAsync(
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Urunler
            .Where(row => row.UrunKodu != null)
            .OrderBy(row => row.UrunKodu)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new UrunReadModel(
                row.UrunKodu!,
                row.Kategori,
                row.AltKategori,
                row.BirimMaliyet,
                row.UrunAdi,
                row.Marka))
            .ToArray();
    }
}
