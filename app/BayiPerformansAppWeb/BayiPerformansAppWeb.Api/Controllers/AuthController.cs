using System.Security.Claims;
using System.Net.Http.Headers;
using BayiPerformansAppWeb.Api.Authentication;
using BayiPerformansAppWeb.Api.Contracts.Requests;
using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BayiPerformansAppWeb.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    ISupabaseAuthService authService,
    ILoginOtpDogrulamaService loginOtpDogrulamaService,
    IOtpService otpService) : ControllerBase
{
    private readonly ISupabaseAuthService _authService = authService;
    private readonly ILoginOtpDogrulamaService _loginOtpDogrulamaService =
        loginOtpDogrulamaService;
    private readonly IOtpService _otpService = otpService;

    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpPost("kayit")]
    [ProducesResponseType(typeof(KayitResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<KayitResponseDto>> KayitAsync(
        KayitRequestDto request,
        CancellationToken cancellationToken)
    {
        DisableAuthResponseCaching();

        var result = await _authService.RegisterAsync(
            request.Email!.Trim(),
            request.Sifre!,
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return FromFailure(result.Failure);
        }

        var registration = result.Value;
        var response = new KayitResponseDto(
            registration.UserId,
            registration.Email,
            registration.EmailConfirmationRequired,
            registration.Session is null
                ? null
                : MapSession(registration.Session));

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginOtpChallengeResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LoginResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        DisableAuthResponseCaching();

        string email = request.Email!.Trim();
        if (string.IsNullOrWhiteSpace(request.DogrulamaToken))
        {
            var credentialResult = await _authService.LoginAsync(
                email,
                request.Sifre!,
                cancellationToken);

            if (!credentialResult.IsSuccess || credentialResult.Value is null)
            {
                return FromFailure(credentialResult.Failure);
            }

            // Ilk parola kontrolunde olusan Supabase oturumu istemciye verilmez.
            // Refresh tokeni kullanilabilir durumda birakmamak icin yerel oturum kapatilir.
            var logoutResult = await _authService.LogoutAsync(
                credentialResult.Value.AccessToken,
                cancellationToken);
            if (!logoutResult.IsSuccess)
            {
                return FromFailure(logoutResult.Failure);
            }

            var challengeResult = await _otpService.UretAsync(
                email,
                OtpAmaclari.Login2Fa,
                cancellationToken);
            if (!challengeResult.IsSuccess || challengeResult.Value is null)
            {
                return FromOtpChallengeFailure(challengeResult);
            }

            var challenge = challengeResult.Value;
            return StatusCode(
                StatusCodes.Status202Accepted,
                new LoginOtpChallengeResponseDto(
                    OtpGerekli: true,
                    IslemId: challenge.IslemId,
                    MaskelenmisEmail: challenge.MaskelenmisHedef,
                    GecerlilikSaniyesi: challenge.GecerlilikSaniyesi,
                    YenidenGonderimSaniyesi:
                        challenge.YenidenGonderimBeklemeSaniyesi));
        }

        var otpResult = await _loginOtpDogrulamaService.DogrulamaTokeniniTuketAsync(
            email,
            request.DogrulamaToken.Trim(),
            OtpAmaclari.Login2Fa,
            cancellationToken);

        if (!otpResult.IsSuccess || otpResult.Value is not true)
        {
            return FromOtpFailure(otpResult);
        }

        var result = await _authService.LoginAsync(
            email,
            request.Sifre!,
            cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? Ok(MapSession(result.Value))
            : FromFailure(result.Failure);
    }

    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpPost("token-yenile")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LoginResponseDto>> TokenYenileAsync(
        TokenYenileRequestDto request,
        CancellationToken cancellationToken)
    {
        DisableAuthResponseCaching();

        var result = await _authService.RefreshAsync(
            request.RefreshToken!.Trim(),
            cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? Ok(MapSession(result.Value))
            : FromFailure(result.Failure);
    }

    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpPost("onay-mailini-yeniden-gonder")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> OnayMailiniYenidenGonderAsync(
        OnayMailiYenidenGonderRequestDto request,
        CancellationToken cancellationToken)
    {
        DisableAuthResponseCaching();

        var result = await _authService.ResendSignupConfirmationAsync(
            request.Email!.Trim(),
            cancellationToken);

        return result.IsSuccess
            ? Accepted()
            : FromFailure(result.Failure);
    }

    [HttpPost("cikis")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CikisAsync(
        CancellationToken cancellationToken)
    {
        DisableAuthResponseCaching();

        var accessToken = GetBearerAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return FromFailure(
                new SupabaseAuthFailure(
                    StatusCodes.Status401Unauthorized,
                    "KIMLIK_DOGRULANAMADI",
                    "Gecerli bir Supabase access token gonderilmelidir.",
                    RetryAfter: null));
        }

        var result = await _authService.LogoutAsync(
            accessToken,
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : FromFailure(result.Failure);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<MeResponseDto> Me()
    {
        DisableAuthResponseCaching();

        var userIdText = User.FindFirstValue("sub");
        var email = User.FindFirstValue("email");

        if (!Guid.TryParse(userIdText, out var userId)
            || string.IsNullOrWhiteSpace(email))
        {
            return FromFailure(
                new SupabaseAuthFailure(
                    StatusCodes.Status401Unauthorized,
                    "KIMLIK_DOGRULANAMADI",
                    "Kullanici bilgileri dogrulanamadi.",
                    RetryAfter: null));
        }

        return Ok(new MeResponseDto(userId, email));
    }

    private ObjectResult FromFailure(SupabaseAuthFailure? failure)
    {
        failure ??= new SupabaseAuthFailure(
            StatusCodes.Status503ServiceUnavailable,
            "KIMLIK_SERVISI_KULLANILAMIYOR",
            "Kimlik servisi gecici olarak kullanilamiyor.",
            RetryAfter: null);

        if (!string.IsNullOrWhiteSpace(failure.RetryAfter))
        {
            Response.Headers["Retry-After"] = failure.RetryAfter;
        }

        var problem = new ProblemDetails
        {
            Status = failure.StatusCode,
            Title = GetTitle(failure.StatusCode),
            Detail = failure.Detail,
            Instance = Request.Path
        };

        problem.Extensions["hata_kodu"] = failure.ErrorCode;
        problem.Extensions["trace_id"] = HttpContext.TraceIdentifier;

        return StatusCode(failure.StatusCode, problem);
    }

    private ObjectResult FromOtpFailure(ServiceResult<bool> result)
    {
        int statusCode = result.ErrorKind == ServiceErrorKind.Unavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status401Unauthorized;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status503ServiceUnavailable
                ? "Ikinci faktor servisi kullanilamiyor."
                : "Ikinci faktor dogrulanamadi.",
            Detail = result.ErrorDetail ?? "Gecerli ve kullanilmamis bir OTP dogrulama tokeni gereklidir.",
            Instance = Request.Path
        };

        problem.Extensions["hata_kodu"] =
            result.ErrorCode ?? "OTP_DOGRULAMA_TOKENI_GECERSIZ";
        problem.Extensions["trace_id"] = HttpContext.TraceIdentifier;

        return StatusCode(statusCode, problem);
    }

    private ObjectResult FromOtpChallengeFailure(
        ServiceResult<OtpUretimSonucu> result)
    {
        int statusCode = result.ErrorCode == "OTP_COOLDOWN_AKTIF"
            ? StatusCodes.Status429TooManyRequests
            : result.ErrorKind == ServiceErrorKind.Unavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status400BadRequest;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status429TooManyRequests
                ? "Yeni kod istemeden once beklenmelidir."
                : statusCode == StatusCodes.Status503ServiceUnavailable
                    ? "Ikinci faktor servisi kullanilamiyor."
                    : "Ikinci faktor islemi baslatilamadi.",
            Detail = result.ErrorDetail ?? "OTP kodu gonderilemedi.",
            Instance = Request.Path
        };

        problem.Extensions["hata_kodu"] =
            result.ErrorCode ?? "OTP_GONDERILEMEDI";
        problem.Extensions["trace_id"] = HttpContext.TraceIdentifier;

        return StatusCode(statusCode, problem);
    }

    private string? GetBearerAccessToken()
    {
        var authorization = Request.Headers.Authorization;
        if (authorization.Count != 1
            || !AuthenticationHeaderValue.TryParse(
                authorization.ToString(),
                out var header)
            || !string.Equals(
                header.Scheme,
                "Bearer",
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return null;
        }

        return header.Parameter;
    }

    private void DisableAuthResponseCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    private static LoginResponseDto MapSession(SupabaseSession session)
    {
        return new LoginResponseDto(
            session.UserId,
            session.Email,
            session.AccessToken,
            session.RefreshToken,
            session.TokenType,
            session.ExpiresIn);
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Kimlik istegi dogrulanamadi.",
            StatusCodes.Status401Unauthorized => "Kimlik dogrulanamadi.",
            StatusCodes.Status403Forbidden => "Isleme izin verilmedi.",
            StatusCodes.Status409Conflict => "Oturum islemi cakisti.",
            StatusCodes.Status429TooManyRequests => "Cok fazla istek.",
            _ => "Kimlik servisi kullanilamiyor."
        };
    }
}
