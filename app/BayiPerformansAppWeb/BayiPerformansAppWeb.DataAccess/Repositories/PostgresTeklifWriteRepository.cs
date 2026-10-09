using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Persistence;
using BayiPerformansAppWeb.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// Normalize edilmiş teklif başlığı, kalem, değerlendirme ve AI yorum
/// tablolarını birlikte yönetir. Bayi/ürün/depo açıklamaları master
/// tablolardan okunur ve teklif tablolarına tekrar yazılmaz.
/// </summary>
public sealed class PostgresTeklifWriteRepository : ITeklifWriteRepository
{
    private readonly PentaDbContext _context;
    private readonly ILogger<PostgresTeklifWriteRepository> _logger;

    public PostgresTeklifWriteRepository(
        PentaDbContext context,
        ILogger<PostgresTeklifWriteRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> TeklifKaydetAsync(
        TeklifModel teklif,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teklif);

        try
        {
            await _context.Teklifler.AddAsync(ToRow(teklif), ct);
            await _context.SaveChangesAsync(ct);
            return teklif.TeklifId;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Teklif veritabanına kaydedilemedi: {TeklifId}",
                teklif.TeklifId);
            throw;
        }
    }

    public async Task<TeklifModel?> TeklifGetirAsync(
        string teklifId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(teklifId))
        {
            return null;
        }

        var row = await BaseQuery()
            .SingleOrDefaultAsync(item => item.TeklifId == teklifId, ct);

        if (row is null)
        {
            return null;
        }

        var references = await LoadReferenceDataAsync([row], ct);
        return ToModel(row, references);
    }

    public async Task<IReadOnlyList<TeklifModel>> TeklifleriListeleAsync(
        TeklifListeFiltre filtre,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtre);

        IQueryable<TeklifRow> query = BaseQuery();

        if (!string.IsNullOrWhiteSpace(filtre.BayiKodu))
        {
            query = query.Where(item => item.BayiKodu == filtre.BayiKodu);
        }

        if (!string.IsNullOrWhiteSpace(filtre.Sehir))
        {
            string sehirPattern = EscapeLikePattern(filtre.Sehir);
            query = query.Where(item => _context.Bayiler.Any(bayi =>
                bayi.BayiKodu == item.BayiKodu
                && bayi.Sehir != null
                && EF.Functions.ILike(bayi.Sehir, sehirPattern, "\\")));
        }

        if (!string.IsNullOrWhiteSpace(filtre.Bolge))
        {
            string bolgePattern = EscapeLikePattern(filtre.Bolge);
            query = query.Where(item => _context.Bayiler.Any(bayi =>
                bayi.BayiKodu == item.BayiKodu
                && bayi.Bolge != null
                && EF.Functions.ILike(bayi.Bolge, bolgePattern, "\\")));
        }

        if (!string.IsNullOrWhiteSpace(filtre.Durum))
        {
            query = query.Where(item => item.Durum == filtre.Durum);
        }

        if (filtre.BaslangicTarihiUtc.HasValue)
        {
            query = query.Where(item =>
                item.OlusturulmaTarihi >= filtre.BaslangicTarihiUtc.Value);
        }

        if (filtre.BitisTarihiUtc.HasValue)
        {
            query = query.Where(item =>
                item.OlusturulmaTarihi <= filtre.BitisTarihiUtc.Value);
        }

        var rows = await query
            .OrderByDescending(item => item.OlusturulmaTarihi)
            .ThenBy(item => item.TeklifId)
            .ToListAsync(ct);

        var references = await LoadReferenceDataAsync(rows, ct);
        return rows.Select(row => ToModel(row, references)).ToArray();
    }

    public async Task<bool> TeklifGuncelleAsync(
        TeklifModel teklif,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(teklif);

        try
        {
            var existing = await _context.Teklifler
                .AsTracking()
                .Include(item => item.Kalemler)
                .Include(item => item.Degerlendirme)
                .Include(item => item.AiYorum)
                .SingleOrDefaultAsync(item => item.TeklifId == teklif.TeklifId, ct);

            if (existing is null)
            {
                return false;
            }

            existing.BayiKodu = teklif.BayiKodu;
            existing.Aciklama = teklif.Aciklama;
            existing.Durum = teklif.Durum;
            existing.ToplamNetTutar = teklif.ToplamNetTutar;
            existing.OlusturulmaTarihi = teklif.OlusturulmaTarihiUtc;

            var yeniSiraNumaralari = teklif.Kalemler
                .Select(item => item.SiraNo)
                .ToHashSet();
            var silinecekKalemler = existing.Kalemler
                .Where(item => !yeniSiraNumaralari.Contains(item.SiraNo))
                .ToArray();

            foreach (var silinecekKalem in silinecekKalemler)
            {
                existing.Kalemler.Remove(silinecekKalem);
            }

            foreach (var kalem in teklif.Kalemler)
            {
                var existingKalem = existing.Kalemler
                    .SingleOrDefault(item => item.SiraNo == kalem.SiraNo);

                if (existingKalem is null)
                {
                    existing.Kalemler.Add(ToRow(teklif.TeklifId, kalem));
                    continue;
                }

                existingKalem.UrunKodu = kalem.UrunKodu;
                existingKalem.DepoKodu = kalem.DepoKodu;
                existingKalem.Miktar = kalem.Miktar;
                existingKalem.BirimFiyat = kalem.BirimFiyat;
                existingKalem.IskontoOrani = kalem.IskontoOrani;
                existingKalem.NetTutar = kalem.NetTutar;
            }

            if (teklif.DegerlendirmeSonucu is not null)
            {
                existing.Degerlendirme ??= new TeklifDegerlendirmeRow
                {
                    TeklifId = teklif.TeklifId,
                    Teklif = existing
                };

                ApplyEvaluation(
                    existing.Degerlendirme,
                    teklif.BayiKodu,
                    teklif.DegerlendirmeSonucu);
            }

            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Teklif veritabanında güncellenemedi: {TeklifId}",
                teklif.TeklifId);
            throw;
        }
    }

    private IQueryable<TeklifRow> BaseQuery() => _context.Teklifler
        .AsNoTracking()
        .AsSplitQuery()
        .Include(item => item.Kalemler)
        .Include(item => item.Degerlendirme)
        .Include(item => item.AiYorum);

    private async Task<ReferenceData> LoadReferenceDataAsync(
        IReadOnlyCollection<TeklifRow> rows,
        CancellationToken ct)
    {
        string[] bayiKodlari = rows
            .Select(item => item.BayiKodu)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] urunKodlari = rows
            .SelectMany(item => item.Kalemler)
            .Select(item => item.UrunKodu)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] depoKodlari = rows
            .SelectMany(item => item.Kalemler)
            .Select(item => item.DepoKodu)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var bayiler = await _context.Bayiler
            .AsNoTracking()
            .Where(item => bayiKodlari.Contains(item.BayiKodu!))
            .ToListAsync(ct);
        var urunler = await _context.Urunler
            .AsNoTracking()
            .Where(item => urunKodlari.Contains(item.UrunKodu!))
            .ToListAsync(ct);
        var depolar = await _context.Depolar
            .AsNoTracking()
            .Where(item => depoKodlari.Contains(item.DepoKodu!))
            .ToListAsync(ct);

        return new ReferenceData(
            bayiler.Where(item => item.BayiKodu is not null).ToDictionary(
                item => item.BayiKodu!,
                StringComparer.OrdinalIgnoreCase),
            urunler.Where(item => item.UrunKodu is not null).ToDictionary(
                item => item.UrunKodu!,
                StringComparer.OrdinalIgnoreCase),
            depolar.Where(item => item.DepoKodu is not null).ToDictionary(
                item => item.DepoKodu!,
                StringComparer.OrdinalIgnoreCase));
    }

    private static TeklifRow ToRow(TeklifModel teklif)
    {
        var row = new TeklifRow
        {
            TeklifId = teklif.TeklifId,
            BayiKodu = teklif.BayiKodu,
            Aciklama = teklif.Aciklama,
            Durum = teklif.Durum,
            ToplamNetTutar = teklif.ToplamNetTutar,
            OlusturulmaTarihi = teklif.OlusturulmaTarihiUtc
        };

        foreach (var kalem in teklif.Kalemler)
        {
            row.Kalemler.Add(ToRow(teklif.TeklifId, kalem));
        }

        return row;
    }

    private static TeklifKalemRow ToRow(
        string teklifId,
        TeklifKalemModel kalem) =>
        new()
        {
            TeklifId = teklifId,
            SiraNo = kalem.SiraNo,
            UrunKodu = kalem.UrunKodu,
            DepoKodu = kalem.DepoKodu,
            Miktar = kalem.Miktar,
            BirimFiyat = kalem.BirimFiyat,
            IskontoOrani = kalem.IskontoOrani,
            NetTutar = kalem.NetTutar
        };

    private static TeklifModel ToModel(
        TeklifRow row,
        ReferenceData references)
    {
        references.Bayiler.TryGetValue(row.BayiKodu, out var bayi);

        return new TeklifModel(
            TeklifId: row.TeklifId,
            BayiKodu: row.BayiKodu,
            BayiAdi: bayi?.BayiAdi,
            Aciklama: row.Aciklama,
            Durum: row.Durum,
            ToplamNetTutar: row.ToplamNetTutar,
            OlusturulmaTarihiUtc: row.OlusturulmaTarihi,
            Kalemler: row.Kalemler
                .OrderBy(item => item.SiraNo)
                .Select(item =>
                {
                    references.Urunler.TryGetValue(item.UrunKodu, out var urun);
                    references.Depolar.TryGetValue(item.DepoKodu, out var depo);
                    return new TeklifKalemModel(
                        item.SiraNo,
                        item.UrunKodu,
                        urun?.UrunAdi,
                        item.DepoKodu,
                        depo?.DepoAdi,
                        item.Miktar,
                        item.BirimFiyat,
                        item.IskontoOrani,
                        item.NetTutar);
                })
                .ToArray(),
            DegerlendirmeSonucu: null,
            BayiSehir: bayi?.Sehir,
            BayiBolge: bayi?.Bolge,
            KayitliDegerlendirme: ToModel(row.Degerlendirme),
            AiYorum: ToModel(row.AiYorum));
    }

    private static TeklifDegerlendirmeKaydi? ToModel(
        TeklifDegerlendirmeRow? row) =>
        row is null
            ? null
            : new TeklifDegerlendirmeKaydi(
                FromStorageDecision(row.Karar),
                row.RiskSkoru,
                row.RiskSeviyesi,
                row.GerekceKodlari,
                row.BayiMarji,
                row.HedefTutturmaYuzdesi,
                row.OrtalamaIskontoOrani,
                row.IadeOrani,
                row.DegerlendirmeTarihi);

    private static TeklifAiYorum? ToModel(TeklifAiYorumRow? row) =>
        row is null
            ? null
            : new TeklifAiYorum(
                row.AiComment,
                row.AlertMessage,
                row.IsFallback,
                row.CreatedTime);

    private static void ApplyEvaluation(
        TeklifDegerlendirmeRow row,
        string bayiKodu,
        TeklifCokluDegerlendirmeOutput output)
    {
        row.BayiKodu = bayiKodu;
        row.Karar = ToStorageDecision(output.GenelKarar);
        row.GerekceKodlari = output.GenelGerekceKodlari.ToArray();
        row.BayiMarji = ToDatabasePercent(output.GenelMarjPct);
        row.DegerlendirmeTarihi = output.DegerlendirilmeTarihiUtc;
        row.RiskSkoru = output.RiskSkoru ?? 0;
        row.RiskSeviyesi = string.IsNullOrWhiteSpace(output.RiskSeviyesi) ? "DUSUK" : output.RiskSeviyesi;
        row.HedefTutturmaYuzdesi = ToDatabaseMetricPercent(output.HedefTutturmaYuzdesi);
        row.OrtalamaIskontoOrani = ToDatabaseMetricPercent(output.OrtalamaIskontoOrani);
        row.IadeOrani = ToDatabaseMetricPercent(output.IadeOrani);
    }

    private static decimal ToDatabaseMetricPercent(decimal? value) =>
        Math.Round(value ?? 0m, 2, MidpointRounding.AwayFromZero);

    private static decimal ToDatabasePercent(decimal ratio) =>
        Math.Round(ratio * 100m, 2, MidpointRounding.AwayFromZero);

    private static string ToStorageDecision(string decision) =>
        decision.Trim().ToUpperInvariant() switch
        {
            "APPROVE" or "APPROVED" => "APPROVED",
            "REJECT" or "REJECTED" => "REJECTED",
            _ => "NEED_APPROVAL"
        };

    private static string FromStorageDecision(string decision) =>
        decision.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => "APPROVE",
            "REJECTED" => "REJECT",
            _ => "NEED_APPROVAL"
        };

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record ReferenceData(
        IReadOnlyDictionary<string, BayiRow> Bayiler,
        IReadOnlyDictionary<string, UrunRow> Urunler,
        IReadOnlyDictionary<string, DepoRow> Depolar);
}
