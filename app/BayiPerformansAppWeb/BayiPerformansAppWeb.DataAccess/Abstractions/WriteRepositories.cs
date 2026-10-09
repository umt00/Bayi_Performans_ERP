using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.DataAccess.Abstractions;

/// <summary>
/// Bayi yazma işlemleri sözleşmesi.
/// </summary>
public interface IBayiWriteRepository
{
    Task<bool> EkleAsync(BayiReadModel bayi, CancellationToken ct = default);
    Task<bool> GuncelleAsync(BayiReadModel bayi, CancellationToken ct = default);
}

/// <summary>
/// Ürün yazma işlemleri sözleşmesi.
/// </summary>
public interface IUrunWriteRepository
{
    Task<bool> EkleAsync(UrunReadModel urun, CancellationToken ct = default);
    Task<bool> GuncelleAsync(UrunReadModel urun, CancellationToken ct = default);
}

/// <summary>
/// Stok yazma işlemleri sözleşmesi.
/// </summary>
public interface IStokWriteRepository
{
    Task<bool> KaydetVeyaGuncelleAsync(StokReadModel stok, CancellationToken ct = default);
}

/// <summary>
/// Teklif saklama ve yönetim sözleşmesi.
/// </summary>
public interface ITeklifWriteRepository
{
    Task<string> TeklifKaydetAsync(TeklifModel teklif, CancellationToken ct = default);
    Task<TeklifModel?> TeklifGetirAsync(string teklifId, CancellationToken ct = default);
    Task<IReadOnlyList<TeklifModel>> TeklifleriListeleAsync(
        TeklifListeFiltre filtre,
        CancellationToken ct = default);
    Task<bool> TeklifGuncelleAsync(TeklifModel teklif, CancellationToken ct = default);
}
