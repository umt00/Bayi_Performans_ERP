using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Admin panelindeki filtre seçenekleri ve tanımları getiren Business servisidir.
/// </summary>
public sealed class TanimlarService : ITanimlarService
{
    private readonly IBayiReadRepository _bayiRepository;
    private readonly IDepoReadRepository _depoRepository;
    private readonly IUrunReadRepository _urunRepository;
    private readonly ISatisReadRepository _satisRepository;

    public TanimlarService(
        IBayiReadRepository bayiRepository,
        IDepoReadRepository depoRepository,
        IUrunReadRepository urunRepository,
        ISatisReadRepository satisRepository)
    {
        ArgumentNullException.ThrowIfNull(bayiRepository);
        ArgumentNullException.ThrowIfNull(depoRepository);
        ArgumentNullException.ThrowIfNull(urunRepository);
        ArgumentNullException.ThrowIfNull(satisRepository);

        _bayiRepository = bayiRepository;
        _depoRepository = depoRepository;
        _urunRepository = urunRepository;
        _satisRepository = satisRepository;
    }

    public async Task<ServiceResult<IReadOnlyList<BayiReadModel>>> ListeleBayilerAsync(
        string? segment,
        string? bolge,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BayiReadModel> bayiler = await _bayiRepository.ListAsync(
            segment,
            bolge,
            cancellationToken);

        return ServiceResult<IReadOnlyList<BayiReadModel>>.Success(bayiler);
    }

    public async Task<ServiceResult<IReadOnlyList<DepoReadModel>>> ListeleDepolarAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DepoReadModel> depolar = await _depoRepository.ListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<DepoReadModel>>.Success(depolar);
    }

    public async Task<ServiceResult<FiltreSecenekleriOutput>> GetirFiltreSecenekleriAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<BayiReadModel> bayiler = await _bayiRepository.ListAsync(null, null, cancellationToken);
        IReadOnlyList<DepoReadModel> depolar = await _depoRepository.ListAsync(cancellationToken);
        IReadOnlyList<UrunReadModel> urunler = await _urunRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<DateOnly> donemTarihleri = await _satisRepository.ListDistinctPeriodsAsync(cancellationToken);

        var bolgeler = bayiler
            .Select(b => b.Bolge)
            .Concat(depolar.Select(d => d.Bolge))
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(b => b)
            .ToArray();

        var segmentler = bayiler
            .Select(b => b.Segment)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToArray();

        var depoTanimlari = depolar
            .Select(d => new DepoTanimOutput(d.DepoKodu, d.Bolge, d.DepoAdi, d.Sehir))
            .OrderBy(d => d.DepoKodu)
            .ToArray();

        var sehirTanimlari = bayiler
            .Where(b => !string.IsNullOrWhiteSpace(b.Sehir))
            .GroupBy(b => b.Sehir!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new SehirTanimOutput(
                Sehir: g.Key,
                Bolge: g.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.Bolge))?.Bolge ?? string.Empty,
                BayiSayisi: g.Count()))
            .ToArray();

        var kategoriler = urunler
            .Where(u => !string.IsNullOrWhiteSpace(u.Kategori))
            .GroupBy(u => u.Kategori!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new KategoriTanimOutput(
                Kategori: g.Key,
                AltKategoriler: g
                    .Select(u => u.AltKategori)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a)
                    .ToArray()!))
            .ToArray();

        var donemler = donemTarihleri
            .Select(d => d.ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var riskSeviyeleri = new[] { "DUSUK", "ORTA", "YUKSEK" };
        var bayiRiskKodlari = new[] { "MARJ_ESIK_ALTI", "HEDEF_ALTINDA", "YUKSEK_ISKONTO", "STOK_YETERSIZ", "YUKSEK_IADE" };
        var stokRiskKodlari = new[] { "STOK_YETERSIZ" };

        var output = new FiltreSecenekleriOutput(
            Bolgeler: bolgeler!,
            Segmentler: segmentler!,
            Depolar: depoTanimlari,
            Kategoriler: kategoriler,
            Donemler: donemler,
            RiskSeviyeleri: riskSeviyeleri,
            BayiRiskGerekceKodlari: bayiRiskKodlari,
            StokRiskGerekceKodlari: stokRiskKodlari,
            Sehirler: sehirTanimlari);

        return ServiceResult<FiltreSecenekleriOutput>.Success(output);
    }
}
