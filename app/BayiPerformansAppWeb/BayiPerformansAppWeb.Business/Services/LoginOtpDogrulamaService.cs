using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities;
using Microsoft.Extensions.Logging;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// OTP dogrulamasindan uretilen kisa omurlu giris kanitini tek kullanimlik
/// olarak tuketir ve e-posta/amac bagini dogrular.
/// </summary>
public sealed class LoginOtpDogrulamaService : ILoginOtpDogrulamaService
{
    private readonly IOtpStore _otpStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LoginOtpDogrulamaService>? _logger;

    public LoginOtpDogrulamaService(
        IOtpStore otpStore,
        TimeProvider? timeProvider = null,
        ILogger<LoginOtpDogrulamaService>? logger = null)
    {
        _otpStore = otpStore ?? throw new ArgumentNullException(nameof(otpStore));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public async Task<ServiceResult<bool>> DogrulamaTokeniniTuketAsync(
        string email,
        string dogrulamaToken,
        string amac,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(dogrulamaToken))
        {
            return Invalid("E-posta adresi ve dogrulama tokeni zorunludur.");
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        string normalizedAmac = string.IsNullOrWhiteSpace(amac)
            ? OtpAmaclari.Login2Fa
            : amac.Trim().ToUpperInvariant();
        string tokenHash = OtpService.HashVerificationToken(dogrulamaToken);

        var record = await _otpStore.ConsumeVerificationTokenAsync(
            tokenHash,
            cancellationToken);

        if (record is null
            || _timeProvider.GetUtcNow() > record.BitisZamaniUtc
            || !string.Equals(
                record.Hedef,
                normalizedEmail,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                record.Amac,
                normalizedAmac,
                StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning(
                "Gecersiz, suresi dolmus veya kullanilmis OTP dogrulama tokeni. Email: {Email}",
                normalizedEmail);
            return Invalid(
                "Dogrulama tokeni gecersiz, suresi dolmus veya daha once kullanilmistir.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private static ServiceResult<bool> Invalid(string detail) =>
        ServiceResult<bool>.Fail(
            ServiceErrorKind.Validation,
            "OTP_DOGRULAMA_TOKENI_GECERSIZ",
            detail);
}
