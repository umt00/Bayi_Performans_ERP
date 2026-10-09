using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Persistence;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// Bayi ekleme ve güncelleme repository'si.
/// </summary>
public sealed class BayiWriteRepository : IBayiWriteRepository
{
    private readonly PentaDbContext _context;
    private readonly ILogger<BayiWriteRepository> _logger;

    public BayiWriteRepository(PentaDbContext context, ILogger<BayiWriteRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> EkleAsync(BayiReadModel bayi, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bayi);

        try
        {
            var row = new BayiRow
            {
                BayiKodu = bayi.BayiKodu,
                BayiAdi = bayi.BayiAdi,
                Sehir = bayi.Sehir,
                Bolge = bayi.Bolge,
                Segment = bayi.Segment
            };

            await _context.Bayiler.AddAsync(row, ct);
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanına bayi eklenirken hata oluştu: {BayiKodu}", bayi.BayiKodu);
            throw;
        }
    }

    public async Task<bool> GuncelleAsync(BayiReadModel bayi, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bayi);

        try
        {
            var existing = await _context.Bayiler
                .AsTracking()
                .SingleOrDefaultAsync(b => b.BayiKodu == bayi.BayiKodu, ct);

            if (existing is null)
            {
                throw new DbUpdateConcurrencyException(
                    $"'{bayi.BayiKodu}' kodlu bayi güncelleme sırasında bulunamadı.");
            }

            _context.Entry(existing).CurrentValues.SetValues(new BayiRow
            {
                BayiKodu = bayi.BayiKodu,
                BayiAdi = bayi.BayiAdi,
                Sehir = bayi.Sehir,
                Bolge = bayi.Bolge,
                Segment = bayi.Segment
            });
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanında bayi güncellenirken hata oluştu: {BayiKodu}", bayi.BayiKodu);
            throw;
        }
    }
}

/// <summary>
/// Ürün ekleme ve güncelleme repository'si.
/// </summary>
public sealed class UrunWriteRepository : IUrunWriteRepository
{
    private readonly PentaDbContext _context;
    private readonly ILogger<UrunWriteRepository> _logger;

    public UrunWriteRepository(PentaDbContext context, ILogger<UrunWriteRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> EkleAsync(UrunReadModel urun, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(urun);

        try
        {
            var row = new UrunRow
            {
                UrunKodu = urun.UrunKodu,
                UrunAdi = urun.UrunAdi,
                Kategori = urun.Kategori,
                AltKategori = urun.AltKategori,
                Marka = urun.Marka,
                BirimMaliyet = urun.BirimMaliyet
            };

            await _context.Urunler.AddAsync(row, ct);
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanına ürün eklenirken hata oluştu: {UrunKodu}", urun.UrunKodu);
            throw;
        }
    }

    public async Task<bool> GuncelleAsync(UrunReadModel urun, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(urun);

        try
        {
            var existing = await _context.Urunler
                .AsTracking()
                .SingleOrDefaultAsync(u => u.UrunKodu == urun.UrunKodu, ct);

            if (existing is null)
            {
                throw new DbUpdateConcurrencyException(
                    $"'{urun.UrunKodu}' kodlu ürün güncelleme sırasında bulunamadı.");
            }

            _context.Entry(existing).CurrentValues.SetValues(new UrunRow
            {
                UrunKodu = urun.UrunKodu,
                UrunAdi = urun.UrunAdi,
                Kategori = urun.Kategori,
                AltKategori = urun.AltKategori,
                Marka = urun.Marka,
                BirimMaliyet = urun.BirimMaliyet
            });
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Veritabanında ürün güncellenirken hata oluştu: {UrunKodu}", urun.UrunKodu);
            throw;
        }
    }
}

/// <summary>
/// Stok kaydetme ve güncelleme repository'si.
/// </summary>
public sealed class StokWriteRepository : IStokWriteRepository
{
    private readonly PentaDbContext _context;
    private readonly ILogger<StokWriteRepository> _logger;

    public StokWriteRepository(PentaDbContext context, ILogger<StokWriteRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> KaydetVeyaGuncelleAsync(StokReadModel stok, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stok);
        string key = $"{stok.UrunKodu}:{stok.DepoKodu}";

        try
        {
            var existing = await _context.Stoklar
                .AsTracking()
                .SingleOrDefaultAsync(
                    s => s.UrunKodu == stok.UrunKodu && s.DepoKodu == stok.DepoKodu,
                    ct);

            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(new StokRow
                {
                    UrunKodu = stok.UrunKodu,
                    DepoKodu = stok.DepoKodu,
                    MevcutStok = stok.MevcutStok,
                    MinStokSeviyesi = stok.MinStokSeviyesi,
                    SonSayimTarihi = stok.SonSayimTarihi
                });
            }
            else
            {
                await _context.Stoklar.AddAsync(new StokRow
                {
                    UrunKodu = stok.UrunKodu,
                    DepoKodu = stok.DepoKodu,
                    MevcutStok = stok.MevcutStok,
                    MinStokSeviyesi = stok.MinStokSeviyesi,
                    SonSayimTarihi = stok.SonSayimTarihi
                }, ct);
            }

            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stok veritabanına kaydedilirken hata oluştu: {Key}", key);
            throw;
        }
    }
}

/// <summary>
/// Teklif yönetim ve saklama repository'si.
/// </summary>
public sealed class TeklifWriteRepository : ITeklifWriteRepository
{
    private static readonly ConcurrentDictionary<string, TeklifModel> Store = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> TeklifKaydetAsync(TeklifModel teklif, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teklif);
        Store[teklif.TeklifId] = teklif;
        return Task.FromResult(teklif.TeklifId);
    }

    public Task<TeklifModel?> TeklifGetirAsync(string teklifId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(teklifId))
        {
            return Task.FromResult<TeklifModel?>(null);
        }

        Store.TryGetValue(teklifId, out var teklif);
        return Task.FromResult(teklif);
    }

    public Task<IReadOnlyList<TeklifModel>> TeklifleriListeleAsync(
        TeklifListeFiltre filtre,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtre);
        var query = Store.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filtre.BayiKodu))
        {
            query = query.Where(t => string.Equals(t.BayiKodu, filtre.BayiKodu, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(filtre.Sehir))
        {
            query = query.Where(t => string.Equals(t.BayiSehir, filtre.Sehir, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(filtre.Bolge))
        {
            query = query.Where(t => string.Equals(t.BayiBolge, filtre.Bolge, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(filtre.Durum))
        {
            query = query.Where(t => string.Equals(t.Durum, filtre.Durum, StringComparison.OrdinalIgnoreCase));
        }
        if (filtre.BaslangicTarihiUtc.HasValue)
        {
            query = query.Where(t => t.OlusturulmaTarihiUtc >= filtre.BaslangicTarihiUtc.Value);
        }
        if (filtre.BitisTarihiUtc.HasValue)
        {
            query = query.Where(t => t.OlusturulmaTarihiUtc <= filtre.BitisTarihiUtc.Value);
        }

        var list = query
            .OrderByDescending(t => t.OlusturulmaTarihiUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<TeklifModel>>(list);
    }

    public Task<bool> TeklifGuncelleAsync(TeklifModel teklif, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teklif);
        Store[teklif.TeklifId] = teklif;
        return Task.FromResult(true);
    }
}
