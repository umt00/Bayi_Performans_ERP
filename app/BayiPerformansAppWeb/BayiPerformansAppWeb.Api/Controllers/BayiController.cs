using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Validation;
using BayiPerformansAppWeb.Business.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

[ApiController]
[Route("bayi")]
public sealed class BayiController(
    IBayiPerformansService service,
    IBayiGecmisService gecmisService) : ApiControllerBase
{
    [HttpGet("{kod}/performans")]
    [ProducesResponseType<BayiPerformansResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BayiPerformansResponseDto>> GetirAsync(
        string kod,
        [FromQuery] string? donem,
        CancellationToken cancellationToken)
    {
        if (!ApiInput.IsValidCode(kod))
        {
            return InvalidInput<BayiPerformansResponseDto>(
                "kod",
                "kod alani 2-32 karakterlik bir bayi kodu olmalidir.");
        }

        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<BayiPerformansResponseDto>(
                "donem",
                "donem YYYY-MM biciminde olmalidir.");
        }

        var result = await service.GetirAsync(
            ApiInput.NormalizeCode(kod),
            parsedPeriod,
            cancellationToken);

        return FromService(result, BayiPerformansResponseDto.From);
    }

    [HttpGet("{kod}/gecmis")]
    [ProducesResponseType<BayiGecmisResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BayiGecmisResponseDto>> GetirGecmisAsync(
        string kod,
        [FromQuery(Name = "ay_sayisi")] int aySayisi = 6,
        CancellationToken cancellationToken = default)
    {
        if (!ApiInput.IsValidCode(kod))
        {
            return InvalidInput<BayiGecmisResponseDto>(
                "kod",
                "kod alani 2-32 karakterlik bir bayi kodu olmalidir.");
        }

        if (aySayisi is < 1 or > 60)
        {
            return InvalidInput<BayiGecmisResponseDto>(
                "ay_sayisi",
                "ay_sayisi 1 ile 60 arasinda bir deger olmalidir.");
        }

        var query = new BayiGecmisQuery(ApiInput.NormalizeCode(kod), aySayisi);
        var result = await gecmisService.GetirGecmisAsync(query, cancellationToken);

        return FromService(result, BayiGecmisResponseDto.From);
    }
}
