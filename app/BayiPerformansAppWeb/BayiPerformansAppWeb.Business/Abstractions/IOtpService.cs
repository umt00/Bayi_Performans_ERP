using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;

namespace BayiPerformansAppWeb.Business.Abstractions;

/// <summary>
/// OTP (Tek Kullanımlık Şifre) üretim, yönetim ve doğrulama servisi sözleşmesi.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Belirtilen hedef (e-posta veya telefon) ve amaç için yeni bir OTP üretir, TTL ile saklar ve üretim sonucunu döner.
    /// </summary>
    Task<ServiceResult<OtpUretimSonucu>> UretAsync(
        string hedef,
        string amac,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// İşlem ID'sine gönderilen OTP kodunu doğrular. Başarılı ise tek kullanımlık doğrulama token'ı döner.
    /// </summary>
    Task<ServiceResult<OtpDogrulamaSonucu>> DogrulaAsync(
        string islemId,
        string kod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Devam eden bir OTP işleminin kalan süresini ve kalan deneme hakkını sorgular.
    /// </summary>
    Task<ServiceResult<OtpDurumBilgisi>> GetirDurumAsync(
        string islemId,
        CancellationToken cancellationToken = default);
}
