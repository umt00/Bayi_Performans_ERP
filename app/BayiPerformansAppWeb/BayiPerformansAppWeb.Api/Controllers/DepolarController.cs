using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Business.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

[ApiController]
[Route("depolar")]
public sealed class DepolarController(ITanimlarService service) : ApiControllerBase
{
    /// <summary>
    /// UI seçim alanlarında kullanılmak üzere depoları listeler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepoTanimDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<DepoTanimDto>>> ListeleAsync(
        CancellationToken cancellationToken)
    {
        var result = await service.ListeleDepolarAsync(cancellationToken);

        return FromService(
            result,
            depolar => (IReadOnlyList<DepoTanimDto>)depolar
                .Select(depo => new DepoTanimDto(
                    depo.DepoKodu,
                    depo.Bolge,
                    depo.DepoAdi,
                    depo.Sehir))
                .ToArray());
    }
}
