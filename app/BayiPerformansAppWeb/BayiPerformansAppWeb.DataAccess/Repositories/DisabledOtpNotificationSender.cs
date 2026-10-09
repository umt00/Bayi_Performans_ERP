using BayiPerformansAppWeb.DataAccess.Abstractions;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// DataAccess katmani olmadan yapilan Business testlerinde acik bir hata verir.
/// Gercek uygulamada e-posta sender kaydi bu fallback'i ezer.
/// </summary>
public sealed class DisabledOtpNotificationSender : IOtpNotificationSender
{
    public Task SendAsync(
        string recipientEmail,
        string code,
        string purpose,
        TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        throw new OtpNotificationException(
            "OTP e-posta gonderimi yapilandirilmadi.");
    }
}
