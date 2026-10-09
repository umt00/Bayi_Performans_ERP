using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Configuration;

internal static class ApiProblemDetailsResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static Task WriteAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["hata_kodu"] = errorCode;
        problem.Extensions["trace_id"] = httpContext.TraceIdentifier;

        return httpContext.Response.WriteAsJsonAsync(
            problem,
            JsonOptions,
            "application/problem+json",
            cancellationToken);
    }
}
