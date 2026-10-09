using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Authentication;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading;
using System.Threading.Tasks;

namespace BayiPerformansAppWeb.Api.Controllers;

/// <summary>
/// OTP (One-Time Password) ve İki Aşamalı Doğrulama (2FA) API controller'ı.
/// </summary>
[ApiController]
[Route("auth/otp")]
[Produces("application/json")]
public sealed class OtpController : ApiControllerBase
{
    private readonly IOtpService _otpService;

    public OtpController(IOtpService otpService)
    {
        _otpService = otpService;
    }

    /// <summary>
    /// Belirtilen hedef adresine (e-posta veya telefon) yeni bir 6 haneli OTP kodu üretir ve gönderim başlatır.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpPost("gonder")]
    [HttpPost("/auth/2fa/send")]
    [ProducesResponseType(typeof(OtpGonderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OtpGonderResponseDto>> GonderAsync(
        [FromBody] OtpGonderRequestDto request,
        CancellationToken cancellationToken)
    {
        var hedef = request?.EffectiveHedef;
        if (request is null || string.IsNullOrWhiteSpace(hedef))
        {
            return InvalidInput<OtpGonderResponseDto>("hedef", "Hedef e-posta veya telefon adresi belirtilmelidir.");
        }

        string amac = string.IsNullOrWhiteSpace(request.EffectiveAmac) ? OtpAmaclari.Login2Fa : request.EffectiveAmac;

        if (string.Equals(
            amac.Trim(),
            OtpAmaclari.Login2Fa,
            StringComparison.OrdinalIgnoreCase))
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Giris OTP istegi baslatilamadi.",
                Detail = "Login OTP kodu yalnizca e-posta ve sifre /auth/login uzerinden dogrulandiktan sonra gonderilebilir.",
                Instance = Request?.Path
            };
            problem.Extensions["hata_kodu"] =
                "LOGIN_OTP_SIFRE_DOGRULAMASI_GEREKLI";
            problem.Extensions["trace_id"] =
                HttpContext?.TraceIdentifier ?? string.Empty;
            return BadRequest(problem);
        }

        var result = await _otpService.UretAsync(hedef, amac, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == "OTP_COOLDOWN_AKTIF")
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Cok fazla istek gonderildi.",
                    Detail = result.ErrorDetail ?? "Lutfen yeni kod talep etmeden once bekleyin.",
                    Instance = Request?.Path
                };
                problem.Extensions["hata_kodu"] = "OTP_COOLDOWN_AKTIF";
                problem.Extensions["trace_id"] = HttpContext?.TraceIdentifier ?? string.Empty;
                return StatusCode(StatusCodes.Status429TooManyRequests, problem);
            }

            return FromService(result, (OtpUretimSonucu val) => MapUretimResponse(val));
        }

        return Ok(MapUretimResponse(result.Value!));
    }

    /// <summary>
    /// İşlem ID ve kullanıcının girdiği 6 haneli OTP kodunu doğrular.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpPost("dogrula")]
    [HttpPost("/auth/2fa/verify")]
    [ProducesResponseType(typeof(OtpDogrulaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OtpDogrulaResponseDto>> DogrulaAsync(
        [FromBody] OtpDogrulaRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidInput<OtpDogrulaResponseDto>("istek", "Gecersiz istek govdesi.");
        }

        var islemId = request.EffectiveIslemId;
        if (string.IsNullOrWhiteSpace(islemId))
        {
            return InvalidInput<OtpDogrulaResponseDto>("islem_id", "Islem ID zorunludur.");
        }

        var kod = request.EffectiveKod;
        if (string.IsNullOrWhiteSpace(kod) || kod.Length != 6)
        {
            return InvalidInput<OtpDogrulaResponseDto>("kod", "OTP kodu 6 haneli olmalidir.");
        }

        var result = await _otpService.DogrulaAsync(islemId, kod, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return FromService(result, (OtpDogrulamaSonucu val) => MapDogrulamaResponse(val));
        }

        return Ok(MapDogrulamaResponse(result.Value));
    }

    /// <summary>
    /// Devam eden bir OTP sürecinin kalan geçerlilik süresini ve kalan deneme hakkını sorgular.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(SupabaseAuthOptions.RateLimitPolicyName)]
    [HttpGet("durum/{islemId}")]
    [ProducesResponseType(typeof(OtpDurumResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OtpDurumResponseDto>> GetirDurumAsync(
        [FromRoute] string islemId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(islemId))
        {
            return InvalidInput<OtpDurumResponseDto>("islemId", "Islem ID belirtilmelidir.");
        }

        var result = await _otpService.GetirDurumAsync(islemId, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return FromService(result, (OtpDurumBilgisi val) => MapDurumResponse(val));
        }

        return Ok(MapDurumResponse(result.Value));
    }

    private static OtpGonderResponseDto MapUretimResponse(OtpUretimSonucu output) =>
        new(
            IslemId: output.IslemId,
            Hedef: output.Hedef,
            MaskelenmisHedef: output.MaskelenmisHedef,
            Amac: output.Amac,
            GecerlilikSaniyesi: output.GecerlilikSaniyesi,
            YenidenGonderimSaniyesi: output.YenidenGonderimBeklemeSaniyesi);

    private static OtpDogrulaResponseDto MapDogrulamaResponse(OtpDogrulamaSonucu output) =>
        new(
            Dogrulandi: output.Dogrulandi,
            DogrulamaToken: output.DogrulamaToken,
            KalanDenemeHakki: output.KalanDenemeHakki,
            HataKodu: output.HataKodu,
            HataMesaji: output.HataMesaji);

    private static OtpDurumResponseDto MapDurumResponse(OtpDurumBilgisi output) =>
        new(
            IslemId: output.IslemId,
            MaskelenmisHedef: output.MaskelenmisHedef,
            Amac: output.Amac,
            KalanGecerlilikSaniyesi: output.KalanGecerlilikSaniyesi,
            KalanDenemeHakki: output.KalanDenemeHakki,
            SuresiDolduMu: output.SuresiDolduMu,
            KilitlendiMi: output.KilitlendiMi);
}
