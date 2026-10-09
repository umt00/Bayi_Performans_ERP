using BayiPerformansAppWeb.Business.Models;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<TResponse> FromService<TOutput, TResponse>(
        ServiceResult<TOutput> result,
        Func<TOutput, TResponse> map)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return Ok(map(result.Value));
        }

        var statusCode = result.ErrorKind switch
        {
            ServiceErrorKind.Validation => StatusCodes.Status400BadRequest,
            ServiceErrorKind.NotFound => StatusCodes.Status404NotFound,
            ServiceErrorKind.Conflict => StatusCodes.Status409Conflict,
            ServiceErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status503ServiceUnavailable
                ? "Servis gecici olarak kullanilamiyor."
                : "Islem tamamlanamadi.",
            Detail = result.ErrorDetail ?? "Islem tamamlanamadi.",
            Instance = Request?.Path
        };

        problem.Extensions["hata_kodu"] = result.ErrorCode ?? "BEKLENMEYEN_HATA";
        problem.Extensions["trace_id"] = HttpContext?.TraceIdentifier ?? string.Empty;

        return StatusCode(statusCode, problem);
    }

    protected ActionResult<T> InvalidInput<T>(string field, string message)
    {
        var errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Istek dogrulanamadi.",
            Instance = Request?.Path
        };

        problem.Extensions["hata_kodu"] = "GECERSIZ_ISTEK";
        problem.Extensions["trace_id"] = HttpContext?.TraceIdentifier ?? string.Empty;

        return BadRequest(problem);
    }
}
