using BayiPerformansAppWeb.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BayiPerformansAppWeb.DataAccess.Abstractions;

/// <summary>
/// OTP ve rate-limit / cooldown durumlarını saklayan ve yöneten depolama arayüzü.
/// Redis veya In-Memory implementasyonları bu arayüzü uygular.
/// </summary>
public interface IOtpStore
{
    /// <summary>
    /// Yeni üretilen OTP kaydını belirtilen TTL süresiyle saklar.
    /// </summary>
    Task SetOtpAsync(string islemId, OtpRecord record, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// İşlem ID'sine ait güncel OTP kaydını getirir. Kayıt bulunamaz veya süresi dolmuşsa null döner.
    /// </summary>
    Task<OtpRecord?> GetOtpAsync(string islemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Doğrulanmış veya iptal edilmiş OTP kaydını siler.
    /// </summary>
    Task DeleteOtpAsync(string islemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hatalı deneme sayısını 1 artırır ve güncel deneme sayısını döner.
    /// </summary>
    Task<int> IncrementAttemptCountAsync(string islemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hedef ve amaç için yeniden gönderim bekleme kilidi (cooldown) koyar.
    /// Eğer kilit zaten varsa false, yeni oluşturulabildiyse true döner.
    /// </summary>
    Task<bool> TrySetCooldownAsync(string hedef, string amac, TimeSpan cooldownDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hedef ve amaç için kalan cooldown süresini döner. Kilit yoksa null döner.
    /// </summary>
    Task<TimeSpan?> GetRemainingCooldownAsync(string hedef, string amac, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gonderim basarisiz oldugunda hedef ve amaca ait cooldown kilidini temizler.
    /// Boylece kullanici servis duzeldikten sonra yeniden kod isteyebilir.
    /// </summary>
    Task DeleteCooldownAsync(string hedef, string amac, CancellationToken cancellationToken = default);

    /// <summary>
    /// OTP dogrulamasindan uretilen token ozetini kisa bir TTL ile saklar.
    /// </summary>
    Task SetVerificationTokenAsync(
        string tokenHash,
        OtpVerificationTokenRecord record,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Token ozetini atomik olarak getirir ve siler. Boylece token tek kullanimliktir.
    /// </summary>
    Task<OtpVerificationTokenRecord?> ConsumeVerificationTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);
}
