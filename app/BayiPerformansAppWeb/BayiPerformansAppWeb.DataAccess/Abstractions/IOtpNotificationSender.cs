using System;
using System.Threading;
using System.Threading.Tasks;

namespace BayiPerformansAppWeb.DataAccess.Abstractions;

/// <summary>
/// Uretilen tek kullanimlik kodu kullaniciya ulastiran altyapi sozlesmesi.
/// Kodun nasil tasindigini Business katmani bilmez.
/// </summary>
public interface IOtpNotificationSender
{
    Task SendAsync(
        string recipientEmail,
        string code,
        string purpose,
        TimeSpan validity,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bildirim saglayicisinin gecici olarak kullanilamadigini belirtir.
/// Hassas e-posta saglayicisi ayrintilari API cevabina tasinmaz.
/// </summary>
public sealed class OtpNotificationException : Exception
{
    public OtpNotificationException(string message)
        : base(message)
    {
    }

    public OtpNotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
