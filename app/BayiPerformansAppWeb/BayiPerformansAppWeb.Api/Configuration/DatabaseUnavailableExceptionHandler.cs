using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace BayiPerformansAppWeb.Api.Configuration;

internal sealed class DatabaseUnavailableExceptionHandler(
    ILogger<DatabaseUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!IsConnectionFailure(exception))
        {
            return false;
        }

        logger.LogError(
            exception,
            "Database connection failed. TraceId: {TraceId}",
            httpContext.TraceIdentifier);

        await ApiProblemDetailsResponse.WriteAsync(
            httpContext,
            StatusCodes.Status503ServiceUnavailable,
            "Veritabani gecici olarak kullanilamiyor.",
            "Veritabani baglanti kapasitesi veya ag erisimi gecici olarak kullanilamiyor. Lutfen daha sonra yeniden deneyin.",
            "VERITABANI_KULLANILAMIYOR",
            cancellationToken);

        return true;
    }

    private static bool IsConnectionFailure(Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException.SqlState.StartsWith(
                        "08",
                        StringComparison.Ordinal)
                    || postgresException.MessageText.Contains(
                        "EMAXCONNSESSION",
                        StringComparison.OrdinalIgnoreCase);
            }

            if (current is NpgsqlException)
            {
                return true;
            }
        }

        return false;
    }
}
