using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Business.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

/// <summary>
/// Zamanlanmış ve arka plan skor hesaplama görevlerini yöneten API controller'ı.
/// </summary>
[ApiController]
[Route("jobs")]
[Produces("application/json")]
public sealed class JobsController : ApiControllerBase
{
    private readonly IScheduledScoreJobService _jobService;

    public JobsController(IScheduledScoreJobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>
    /// Zamanlanmış arka plan skor hesaplama görevinin son çalışma durumunu ve işlenen kayıt bilgisini getirir.
    /// Görev API üzerinden manuel tetiklenemez.
    /// </summary>
    [HttpGet("durum")]
    [HttpGet("durum/{jobId}")]
    [HttpGet("{jobId}")]
    [ProducesResponseType(typeof(JobDurumResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JobDurumResponseDto>> GetirDurumAsync(CancellationToken cancellationToken)
    {
        var result = await _jobService.GetirJobDurumuAsync(cancellationToken);
        return FromService(result, JobDurumResponseDto.From);
    }
}
