using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Validation;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

/// <summary>
/// Data/Automation ekibinin hazırladığı aylık bayi performans karnelerini
/// salt-okunur olarak UI ve Power BI katmanlarına sunar.
/// </summary>
[ApiController]
[Route("api/v1/performance")]
public sealed class PerformanceController(
    IBayiDonemPerformansReadRepository repository) : ApiControllerBase
{
    private static readonly HashSet<string> AllowedRiskLevels = new(
        ["DUSUK", "ORTA", "YUKSEK"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Seçilen dönemdeki bayi performanslarını filtreli ve sayfalı listeler.
    /// Dönem verilmezse tablodaki en son dönem kullanılır.
    /// </summary>
    [HttpGet("dealers")]
    [ProducesResponseType<PerformansBayiListesiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformansBayiListesiResponseDto>> ListeleAsync(
        [FromQuery] string? donem,
        [FromQuery(Name = "risk_seviyesi")] string? riskSeviyesi,
        [FromQuery] string? bolge,
        [FromQuery] string? segment,
        [FromQuery] int sayfa = 1,
        [FromQuery(Name = "sayfa_boyutu")] int sayfaBoyutu = 50,
        [FromQuery] string? sehir = null,
        CancellationToken cancellationToken = default)
    {
        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "donem",
                "donem YYYY-MM biciminde olmalidir.");
        }

        string? normalizedRiskLevel = ApiInput.NormalizeOptionalText(riskSeviyesi)?.ToUpperInvariant();
        if (normalizedRiskLevel is not null && !AllowedRiskLevels.Contains(normalizedRiskLevel))
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "risk_seviyesi",
                "risk_seviyesi DUSUK, ORTA veya YUKSEK olmalidir.");
        }

        if (sayfa < 1)
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "sayfa",
                "sayfa 1 veya daha buyuk olmalidir.");
        }

        if (sayfaBoyutu is < 1 or > 100)
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "sayfa_boyutu",
                "sayfa_boyutu 1 ile 100 arasinda olmalidir.");
        }

        string? normalizedBolge = ApiInput.NormalizeOptionalText(bolge);
        string? normalizedSegment = ApiInput.NormalizeOptionalText(segment);
        string? normalizedSehir = ApiInput.NormalizeOptionalText(sehir);
        if (normalizedBolge?.Length > 100)
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "bolge",
                "bolge en fazla 100 karakter olabilir.");
        }

        if (normalizedSegment?.Length > 100)
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "segment",
                "segment en fazla 100 karakter olabilir.");
        }

        if (normalizedSehir?.Length > 100)
        {
            return InvalidInput<PerformansBayiListesiResponseDto>(
                "sehir",
                "sehir en fazla 100 karakter olabilir.");
        }

        var query = new BayiDonemPerformansQuery(
            parsedPeriod,
            normalizedRiskLevel,
            normalizedBolge,
            normalizedSegment,
            sayfa,
            sayfaBoyutu,
            normalizedSehir);

        var result = await repository.ListAsync(query, cancellationToken);
        return Ok(PerformansBayiListesiResponseDto.From(result));
    }

    /// <summary>
    /// Bir bayinin seçilen dönem aylık performans karnesini getirir.
    /// Dönem verilmezse bayinin en son dönemi kullanılır.
    /// </summary>
    [HttpGet("dealers/{bayiKodu}")]
    [ProducesResponseType<PerformansBayiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformansBayiResponseDto>> GetirAsync(
        [FromRoute] string bayiKodu,
        [FromQuery] string? donem,
        CancellationToken cancellationToken = default)
    {
        if (!ApiInput.IsValidCode(bayiKodu))
        {
            return InvalidInput<PerformansBayiResponseDto>(
                "bayi_kodu",
                "bayi_kodu 2-32 karakterlik gecerli bir kod olmalidir.");
        }

        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<PerformansBayiResponseDto>(
                "donem",
                "donem YYYY-MM biciminde olmalidir.");
        }

        string normalizedCode = ApiInput.NormalizeCode(bayiKodu);
        var result = await repository.GetAsync(
            normalizedCode,
            parsedPeriod,
            cancellationToken);

        if (result is null)
        {
            return PerformansBulunamadi(normalizedCode);
        }

        return Ok(PerformansBayiResponseDto.From(result));
    }

    /// <summary>
    /// Seçilen dönem için dashboard kartlarını dolduran toplu istatistikleri getirir.
    /// </summary>
    [HttpGet("dashboard-stats")]
    [ProducesResponseType<PerformansDashboardStatsResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PerformansDashboardStatsResponseDto>> GetirIstatistikAsync(
        [FromQuery] string? donem,
        CancellationToken cancellationToken = default)
    {
        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<PerformansDashboardStatsResponseDto>(
                "donem",
                "donem YYYY-MM biciminde olmalidir.");
        }

        var result = await repository.GetDashboardStatsAsync(
            parsedPeriod,
            cancellationToken);
        return Ok(PerformansDashboardStatsResponseDto.From(result));
    }

    private ObjectResult PerformansBulunamadi(string bayiKodu)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Performans kaydi bulunamadi.",
            Detail = $"{bayiKodu} icin istenen donemde performans kaydi bulunamadi.",
            Instance = Request.Path
        };
        problem.Extensions["hata_kodu"] = "PERFORMANS_BULUNAMADI";
        problem.Extensions["trace_id"] = HttpContext.TraceIdentifier;
        return NotFound(problem);
    }
}
