using System.Net;
using System.Text;
using System.Text.Json;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Configuration;
using BayiPerformansAppWeb.DataAccess.Repositories;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.Tests.Infrastructure;

public sealed class BrevoOtpNotificationSenderTests
{
    private const string ApiKey = "test-api-key-never-use-in-production";

    [Fact]
    public async Task SendAsync_UsesBrevoHttpsContractWithoutLeakingApiKeyIntoBody()
    {
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.Created,
            "{\"messageId\":\"test-message-id\"}");
        var sender = CreateSender(handler);

        await sender.SendAsync(
            "recipient@example.com",
            "763686",
            "LOGIN_2FA",
            TimeSpan.FromMinutes(3));

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://api.brevo.com/v3/smtp/email",
            handler.RequestUri?.ToString());
        Assert.Equal(ApiKey, handler.ApiKey);
        Assert.NotNull(handler.Body);
        Assert.DoesNotContain(ApiKey, handler.Body, StringComparison.Ordinal);

        using JsonDocument json = JsonDocument.Parse(handler.Body!);
        JsonElement root = json.RootElement;
        Assert.Equal(
            "verified-sender@example.com",
            root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal(
            "recipient@example.com",
            root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Contains(
            "763686",
            root.GetProperty("textContent").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenProviderRejectsRequest_ThrowsSanitizedException()
    {
        const string providerDetail = "provider-internal-secret-detail";
        var handler = new RecordingHttpMessageHandler(
            HttpStatusCode.BadRequest,
            $"{{\"message\":\"{providerDetail}\"}}");
        var sender = CreateSender(handler);

        OtpNotificationException exception = await Assert.ThrowsAsync<OtpNotificationException>(
            () => sender.SendAsync(
                "recipient@example.com",
                "763686",
                "LOGIN_2FA",
                TimeSpan.FromMinutes(3)));

        Assert.DoesNotContain(providerDetail, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("763686", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            "OTP e-posta saglayicisi gecici olarak kullanilamiyor.",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_WhenDisabled_DoesNotCallProvider()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Created, "{}");
        var sender = CreateSender(handler, enabled: false);

        await Assert.ThrowsAsync<OtpNotificationException>(
            () => sender.SendAsync(
                "recipient@example.com",
                "763686",
                "LOGIN_2FA",
                TimeSpan.FromMinutes(3)));

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancels_PropagatesCancellation()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.Created, "{}")
        {
            WaitForCancellation = true
        };
        var sender = CreateSender(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(
                "recipient@example.com",
                "763686",
                "LOGIN_2FA",
                TimeSpan.FromMinutes(3),
                cancellation.Token));
    }

    private static BrevoOtpNotificationSender CreateSender(
        HttpMessageHandler handler,
        bool enabled = true)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/")
        };
        var options = Options.Create(new BrevoEmailOptions
        {
            Enabled = enabled,
            ApiKey = ApiKey,
            FromEmail = "verified-sender@example.com",
            FromName = "Bayi Performans",
            TimeoutSeconds = 15
        });

        return new BrevoOtpNotificationSender(client, options);
    }

    private sealed class RecordingHttpMessageHandler(
        HttpStatusCode statusCode,
        string responseBody)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        public string? Body { get; private set; }

        public bool WaitForCancellation { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("api-key", out var values)
                ? values.SingleOrDefault()
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
