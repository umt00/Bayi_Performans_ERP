using System.Net.Http.Json;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

/// <summary>
/// OTP kodunu Brevo'nun HTTPS transactional email API'siyle iletir.
/// API anahtari, OTP kodu ve saglayici cevap govdesi loglanmaz.
/// </summary>
public sealed class BrevoOtpNotificationSender : IOtpNotificationSender
{
    internal static readonly Uri ServiceBaseAddress =
        new("https://api.brevo.com/v3/");

    private readonly HttpClient _httpClient;
    private readonly BrevoEmailOptions _options;
    private readonly ILogger<BrevoOtpNotificationSender>? _logger;

    public BrevoOtpNotificationSender(
        HttpClient httpClient,
        IOptions<BrevoEmailOptions> options,
        ILogger<BrevoOtpNotificationSender>? logger = null)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value
            ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task SendAsync(
        string recipientEmail,
        string code,
        string purpose,
        TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new OtpNotificationException(
                "OTP e-posta gonderimi etkin degil.");
        }

        var payload = new
        {
            sender = new
            {
                name = _options.FromName,
                email = _options.FromEmail
            },
            to = new[]
            {
                new { email = recipientEmail }
            },
            subject = "Bayi Performans dogrulama kodu",
            textContent = $"Dogrulama kodunuz: {code}\n\n"
                + $"Kod {Math.Ceiling(validity.TotalMinutes):0} dakika gecerlidir.\n"
                + "Bu kodu kimseyle paylasmayin. Bu islemi siz baslatmadiysaniz e-postayi yok sayin."
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // Saglayici cevap govdesi hassas ayrinti icerebilir; loglamadan tuket.
            await response.Content.CopyToAsync(Stream.Null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "Brevo OTP gonderimini reddetti. HTTP: {StatusCode}, Hedef: {MaskedEmail}, Amac: {Purpose}",
                    (int)response.StatusCode,
                    MaskEmail(recipientEmail),
                    purpose);

                throw new OtpNotificationException(
                    "OTP e-posta saglayicisi gecici olarak kullanilamiyor.");
            }

            _logger?.LogInformation(
                "OTP e-postasi gonderildi. Hedef: {MaskedEmail}, Amac: {Purpose}",
                MaskEmail(recipientEmail),
                purpose);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OtpNotificationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger?.LogWarning(
                ex,
                "Brevo OTP servisine ulasilamadi. Hedef: {MaskedEmail}, Amac: {Purpose}",
                MaskEmail(recipientEmail),
                purpose);

            throw new OtpNotificationException(
                "OTP e-posta saglayicisi gecici olarak kullanilamiyor.",
                ex);
        }
    }

    private static string MaskEmail(string email)
    {
        int separatorIndex = email.IndexOf('@');
        if (separatorIndex <= 0)
        {
            return "***";
        }

        string localPart = email[..separatorIndex];
        string domain = email[(separatorIndex + 1)..];
        return $"{localPart[0]}***@{domain}";
    }
}
