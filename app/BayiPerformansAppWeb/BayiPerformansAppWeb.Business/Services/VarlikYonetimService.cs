using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Bayi ekleme ve güncelleme işlemlerini yöneten servis.
/// </summary>
public sealed class BayiYonetimService : IBayiYonetimService
{
    private readonly IBayiReadRepository _readRepository;
    private readonly IBayiWriteRepository _writeRepository;

    public BayiYonetimService(
        IBayiReadRepository readRepository,
        IBayiWriteRepository writeRepository)
    {
        _readRepository = readRepository ?? throw new ArgumentNullException(nameof(readRepository));
        _writeRepository = writeRepository ?? throw new ArgumentNullException(nameof(writeRepository));
    }

    public async Task<ServiceResult<BayiReadModel>> EkleAsync(
        YeniBayiCommand command,
        CancellationToken ct = default)
    {
        if (command is null)
        {
            return ServiceResult<BayiReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_KOMUT", "Bayi gövdesi boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(command.BayiKodu))
        {
            return ServiceResult<BayiReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_BAYI_KODU", "Bayi kodu zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(command.BayiAdi))
        {
            return ServiceResult<BayiReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_BAYI_ADI", "Bayi adı zorunludur.");
        }

        string normalizedKod = command.BayiKodu.Trim().ToUpperInvariant();
        var existing = await _readRepository.GetByCodeAsync(normalizedKod, ct);
        if (existing is not null)
        {
            return ServiceResult<BayiReadModel>.Fail(
                ServiceErrorKind.Conflict,
                "BAYI_ZATEN_MEVCUT",
                $"'{normalizedKod}' kodlu bayi sistemde zaten kayıtlıdır.");
        }

        var model = new BayiReadModel(
            BayiKodu: normalizedKod,
            BayiAdi: command.BayiAdi.Trim(),
            Sehir: string.IsNullOrWhiteSpace(command.Sehir) ? "Belirtilmemiş" : command.Sehir.Trim(),
            Bolge: string.IsNullOrWhiteSpace(command.Bolge) ? "Belirtilmemiş" : command.Bolge.Trim(),
            Segment: string.IsNullOrWhiteSpace(command.Segment) ? "Standart" : command.Segment.Trim());

        await _writeRepository.EkleAsync(model, ct);
        return ServiceResult<BayiReadModel>.Success(model);
    }

    public async Task<ServiceResult<BayiReadModel>> GuncelleAsync(
        string bayiKodu,
        GuncelleBayiCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(bayiKodu))
        {
            return ServiceResult<BayiReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_BAYI_KODU", "Bayi kodu zorunludur.");
        }

        if (command is null)
        {
            return ServiceResult<BayiReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_KOMUT", "Güncelleme gövdesi boş olamaz.");
        }

        string normalizedKod = bayiKodu.Trim().ToUpperInvariant();
        var existing = await _readRepository.GetByCodeAsync(normalizedKod, ct);
        if (existing is null)
        {
            return ServiceResult<BayiReadModel>.Fail(
                ServiceErrorKind.NotFound,
                "BAYI_BULUNAMADI",
                $"'{normalizedKod}' kodlu bayi bulunamadı.");
        }

        var updated = existing with
        {
            BayiAdi = string.IsNullOrWhiteSpace(command.BayiAdi) ? existing.BayiAdi : command.BayiAdi.Trim(),
            Sehir = string.IsNullOrWhiteSpace(command.Sehir) ? existing.Sehir : command.Sehir.Trim(),
            Bolge = string.IsNullOrWhiteSpace(command.Bolge) ? existing.Bolge : command.Bolge.Trim(),
            Segment = string.IsNullOrWhiteSpace(command.Segment) ? existing.Segment : command.Segment.Trim()
        };

        await _writeRepository.GuncelleAsync(updated, ct);
        return ServiceResult<BayiReadModel>.Success(updated);
    }
}

/// <summary>
/// Ürün ekleme ve listeleme işlemlerini yöneten servis.
/// </summary>
public sealed class UrunYonetimService : IUrunYonetimService
{
    private readonly IUrunReadRepository _readRepository;
    private readonly IUrunWriteRepository _writeRepository;

    public UrunYonetimService(
        IUrunReadRepository readRepository,
        IUrunWriteRepository writeRepository)
    {
        _readRepository = readRepository ?? throw new ArgumentNullException(nameof(readRepository));
        _writeRepository = writeRepository ?? throw new ArgumentNullException(nameof(writeRepository));
    }

    public async Task<ServiceResult<UrunReadModel>> EkleAsync(
        YeniUrunCommand command,
        CancellationToken ct = default)
    {
        if (command is null)
        {
            return ServiceResult<UrunReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_KOMUT", "Ürün gövdesi boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(command.UrunKodu))
        {
            return ServiceResult<UrunReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_URUN_KODU", "Ürün kodu zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(command.UrunAdi))
        {
            return ServiceResult<UrunReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_URUN_ADI", "Ürün adı zorunludur.");
        }

        if (command.BirimMaliyet < 0)
        {
            return ServiceResult<UrunReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_MALIYET", "Birim maliyet 0 veya pozitif olmalıdır.");
        }

        string normalizedKod = command.UrunKodu.Trim().ToUpperInvariant();
        var existing = await _readRepository.GetByCodeAsync(normalizedKod, ct);
        if (existing is not null)
        {
            return ServiceResult<UrunReadModel>.Fail(
                ServiceErrorKind.Conflict,
                "URUN_ZATEN_MEVCUT",
                $"'{normalizedKod}' kodlu ürün sistemde zaten mevcuttur.");
        }

        var model = new UrunReadModel(
            UrunKodu: normalizedKod,
            UrunAdi: command.UrunAdi.Trim(),
            Kategori: string.IsNullOrWhiteSpace(command.Kategori) ? "Genel" : command.Kategori.Trim(),
            AltKategori: string.IsNullOrWhiteSpace(command.AltKategori) ? "Genel" : command.AltKategori.Trim(),
            Marka: string.IsNullOrWhiteSpace(command.Marka) ? "Genel" : command.Marka.Trim(),
            BirimMaliyet: command.BirimMaliyet);

        await _writeRepository.EkleAsync(model, ct);
        return ServiceResult<UrunReadModel>.Success(model);
    }

    public async Task<ServiceResult<IReadOnlyList<UrunReadModel>>> ListeleAsync(
        string? kategori = null,
        string? arama = null,
        CancellationToken ct = default)
    {
        var list = await _readRepository.ListAllAsync(ct);
        var query = list.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(kategori))
        {
            query = query.Where(u => string.Equals(u.Kategori, kategori.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(arama))
        {
            string cleanSearch = arama.Trim();
            query = query.Where(u =>
                (u.UrunAdi?.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
                u.UrunKodu.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                (u.Marka?.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return ServiceResult<IReadOnlyList<UrunReadModel>>.Success(query.ToList());
    }

    public async Task<ServiceResult<UrunReadModel>> GetirByKodAsync(
        string urunKodu,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(urunKodu))
        {
            return ServiceResult<UrunReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_URUN_KODU", "Ürün kodu zorunludur.");
        }

        var urun = await _readRepository.GetByCodeAsync(urunKodu.Trim().ToUpperInvariant(), ct);
        if (urun is null)
        {
            return ServiceResult<UrunReadModel>.Fail(ServiceErrorKind.NotFound, "URUN_BULUNAMADI", $"'{urunKodu}' kodlu ürün bulunamadı.");
        }

        return ServiceResult<UrunReadModel>.Success(urun);
    }
}

/// <summary>
/// Stok kaydetme ve güncelleme işlemlerini yöneten servis.
/// </summary>
public sealed class StokYonetimService : IStokYonetimService
{
    private readonly IUrunReadRepository _urunReadRepository;
    private readonly IDepoReadRepository _depoReadRepository;
    private readonly IStokWriteRepository _stokWriteRepository;

    public StokYonetimService(
        IUrunReadRepository urunReadRepository,
        IDepoReadRepository depoReadRepository,
        IStokWriteRepository stokWriteRepository)
    {
        _urunReadRepository = urunReadRepository ?? throw new ArgumentNullException(nameof(urunReadRepository));
        _depoReadRepository = depoReadRepository ?? throw new ArgumentNullException(nameof(depoReadRepository));
        _stokWriteRepository = stokWriteRepository ?? throw new ArgumentNullException(nameof(stokWriteRepository));
    }

    public async Task<ServiceResult<StokReadModel>> KaydetVeyaGuncelleAsync(
        YeniStokCommand command,
        CancellationToken ct = default)
    {
        if (command is null)
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_KOMUT", "Stok gövdesi boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(command.UrunKodu))
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_URUN_KODU", "Ürün kodu zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(command.DepoKodu))
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_DEPO_KODU", "Depo kodu zorunludur.");
        }

        if (command.MevcutStok < 0)
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_STOK", "Mevcut stok 0 veya pozitif olmalıdır.");
        }

        if (command.MinStokSeviyesi < 0)
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_MIN_STOK", "Minimum stok seviyesi 0 veya pozitif olmalıdır.");
        }

        string normalizedUrun = command.UrunKodu.Trim().ToUpperInvariant();
        string normalizedDepo = command.DepoKodu.Trim().ToUpperInvariant();

        var urun = await _urunReadRepository.GetByCodeAsync(normalizedUrun, ct);
        if (urun is null)
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.NotFound, "URUN_BULUNAMADI", $"'{normalizedUrun}' kodlu ürün bulunamadı.");
        }

        var depo = await _depoReadRepository.GetByCodeAsync(normalizedDepo, ct);
        if (depo is null)
        {
            return ServiceResult<StokReadModel>.Fail(ServiceErrorKind.NotFound, "DEPO_BULUNAMADI", $"'{normalizedDepo}' kodlu depo bulunamadı.");
        }

        var model = new StokReadModel(
            UrunKodu: normalizedUrun,
            DepoKodu: normalizedDepo,
            MevcutStok: command.MevcutStok,
            MinStokSeviyesi: command.MinStokSeviyesi,
            SonSayimTarihi: DateOnly.FromDateTime(DateTime.UtcNow));

        await _stokWriteRepository.KaydetVeyaGuncelleAsync(model, ct);
        return ServiceResult<StokReadModel>.Success(model);
    }
}
