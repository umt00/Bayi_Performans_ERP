using System.Text.Json;
using BayiPerformansAppWeb.Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BayiPerformansAppWeb.Api.Tests.Infrastructure;

public sealed class DatabaseUnavailableExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_BaglantiHatasini503ProblemDetailsOlarakDondurur()
    {
        var handler = new DatabaseUnavailableExceptionHandler(
            NullLogger<DatabaseUnavailableExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/teklifler";
        context.Response.Body = new MemoryStream();

        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException(
                "Retry limit reached.",
                new NpgsqlException("Connection pool is unavailable.")),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            "VERITABANI_KULLANILAMIYOR",
            document.RootElement.GetProperty("hata_kodu").GetString());
        Assert.Equal(
            "/teklifler",
            document.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_VeritabaniDisiHatayiGenelIsleyiciyeBirakir()
    {
        var handler = new DatabaseUnavailableExceptionHandler(
            NullLogger<DatabaseUnavailableExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        bool handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("Unrelated failure."),
            CancellationToken.None);

        Assert.False(handled);
    }
}
