using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;

namespace BayiPerformansAppWeb.Business.Abstractions;

/// <summary>
/// Çoklu ürün içeren teklif oluşturma, saklama ve değerlendirme işlemlerini yöneten servis sözleşmesi.
/// </summary>
public interface ITeklifYonetimService
{
    /// <summary>
    /// Verilen bayi ve çoklu ürün kalemleri için yeni bir teklif oluşturur.
    /// Bu metot hesaplama yapmaz; teklifi doğrular, tekil TeklifId üretir ve TASLAK olarak kaydeder.
    /// </summary>
    Task<ServiceResult<TeklifModel>> TeklifOlusturAsync(YeniTeklifCommand command, CancellationToken ct = default);

    /// <summary>
    /// Daha önceden oluşturulmuş bir teklifi TeklifId üzerinden getirir ve deterministik karar motoruyla değerlendirir.
    /// </summary>
    Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> TeklifDegerlendirByIdAsync(string teklifId, CancellationToken ct = default);

    /// <summary>
    /// Kaydetmeden doğrudan çoklu ürün teklifini anlık olarak değerlendirir.
    /// </summary>
    Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> TeklifAnlikDegerlendirAsync(YeniTeklifCommand command, CancellationToken ct = default);

    /// <summary>
    /// TeklifId ile teklif detayını ve varsa değerlendirme sonucunu getirir.
    /// </summary>
    Task<ServiceResult<TeklifModel>> TeklifGetirAsync(string teklifId, CancellationToken ct = default);

    /// <summary>
    /// Teklifleri bayi, şehir, bölge, durum ve tarih aralığı filtreleriyle listeler.
    /// </summary>
    Task<ServiceResult<IReadOnlyList<TeklifModel>>> TeklifleriListeleAsync(
        TeklifListeFiltre filtre,
        CancellationToken ct = default);
}
