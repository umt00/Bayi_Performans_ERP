namespace BayiPerformansAppWeb.DataAccess.Configuration;

/// <summary>
/// OTP e-postalari icin Brevo HTTPS API ayarlari. API anahtari yalnizca
/// .NET User Secrets veya backend ortam degiskenlerinden gelmelidir.
/// </summary>
public sealed class BrevoEmailOptions
{
    public const string SectionName = "BrevoEmail";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Bayi Performans";

    public int TimeoutSeconds { get; set; } = 15;
}
