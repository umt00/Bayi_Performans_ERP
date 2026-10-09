using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Business.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

[ApiController]
[Route("tanimlar")]
public sealed class TanimlarController(ITanimlarService service) : ApiControllerBase
{
    [HttpGet("filtre-secenekleri")]
    [ProducesResponseType<FiltreSecenekleriResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<FiltreSecenekleriResponseDto>> GetirFiltreSecenekleriAsync(
        CancellationToken cancellationToken)
    {
        var result = await service.GetirFiltreSecenekleriAsync(cancellationToken);
        return FromService(result, FiltreSecenekleriResponseDto.From);
    }
}
