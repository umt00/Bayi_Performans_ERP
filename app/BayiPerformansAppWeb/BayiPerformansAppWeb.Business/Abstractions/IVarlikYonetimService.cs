using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Business.Abstractions;

/// <summary>
/// Bayi tanımlama ve güncelleme servis sözleşmesi.
/// </summary>
public interface IBayiYonetimService
{
    Task<ServiceResult<BayiReadModel>> EkleAsync(YeniBayiCommand command, CancellationToken ct = default);
    Task<ServiceResult<BayiReadModel>> GuncelleAsync(string bayiKodu, GuncelleBayiCommand command, CancellationToken ct = default);
}

/// <summary>
/// Ürün tanımlama ve listeleme servis sözleşmesi.
/// </summary>
public interface IUrunYonetimService
{
    Task<ServiceResult<UrunReadModel>> EkleAsync(YeniUrunCommand command, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<UrunReadModel>>> ListeleAsync(string? kategori = null, string? arama = null, CancellationToken ct = default);
    Task<ServiceResult<UrunReadModel>> GetirByKodAsync(string urunKodu, CancellationToken ct = default);
}

/// <summary>
/// Stok ekleme ve güncelleme servis sözleşmesi.
/// </summary>
public interface IStokYonetimService
{
    Task<ServiceResult<StokReadModel>> KaydetVeyaGuncelleAsync(YeniStokCommand command, CancellationToken ct = default);
}
