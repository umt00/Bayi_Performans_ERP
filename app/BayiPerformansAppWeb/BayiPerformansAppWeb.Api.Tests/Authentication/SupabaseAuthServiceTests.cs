using System.Net;
using System.Text;
using System.Text.Json;
using BayiPerformansAppWeb.Api.Authentication;

namespace BayiPerformansAppWeb.Api.Tests.Authentication;

public sealed class SupabaseAuthServiceTests
{
    [Fact]
    public async Task LoginAsync_MapsSuccessfulSession()
    {
        var userId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            $$"""
            {
              "access_token": "access-token",
              "refresh_token": "refresh-token",
              "token_type": "bearer",
              "expires_in": 3600,
              "user": {
                "id": "{{userId}}",
                "email": "stajyer@example.com"
              }
            }
            """);
        var service = CreateService(handler);

        var result = await service.LoginAsync(
            "stajyer@example.com",
            "Guvenli-Sifre-123!",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("stajyer@example.com", result.Value.Email);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.Equal(3600, result.Value.ExpiresIn);
        Assert.Equal("/auth/v1/token?grant_type=password", handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailConfirmationIsEnabled_ReturnsPendingRegistration()
    {
        var userId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            $$"""
            {
              "id": "{{userId}}",
              "email": "stajyer@example.com"
            }
            """);
        var service = CreateService(handler);

        var result = await service.RegisterAsync(
            "stajyer@example.com",
            "Guvenli-Sifre-123!",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId, result.Value.UserId);
        Assert.True(result.Value.EmailConfirmationRequired);
        Assert.Null(result.Value.Session);
        Assert.Equal("/auth/v1/signup", handler.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsSanitizedFailure()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """
            {
              "code": "invalid_credentials",
              "message": "upstream detail must not be exposed"
            }
            """);
        var service = CreateService(handler);

        var result = await service.LoginAsync(
            "stajyer@example.com",
            "yanlis-sifre",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(401, result.Failure.StatusCode);
        Assert.Equal("GECERSIZ_KIMLIK_BILGISI", result.Failure.ErrorCode);
        Assert.DoesNotContain("upstream", result.Failure.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("yanlis-sifre", result.Failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginAsync_RateLimited_PreservesRetryAfter()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.TooManyRequests,
            """{"code":"over_request_rate_limit"}""",
            retryAfterSeconds: 30);
        var service = CreateService(handler);

        var result = await service.LoginAsync(
            "stajyer@example.com",
            "Guvenli-Sifre-123!",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(429, result.Failure.StatusCode);
        Assert.Equal("COK_FAZLA_ISTEK", result.Failure.ErrorCode);
        Assert.Contains("30", result.Failure.RetryAfter);
    }

    [Fact]
    public async Task LoginAsync_InvalidJson_ReturnsControlledFailure()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            "not-json");
        var service = CreateService(handler);

        var result = await service.LoginAsync(
            "stajyer@example.com",
            "Guvenli-Sifre-123!",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(503, result.Failure.StatusCode);
        Assert.Equal("KIMLIK_SERVISI_GECERSIZ_CEVAP", result.Failure.ErrorCode);
    }

    [Fact]
    public async Task LoginAsync_InvalidPublishableKey_ReturnsControlled503()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            """{"message":"Invalid API key"}""");
        var service = CreateService(handler);

        var result = await service.LoginAsync(
            "stajyer@example.com",
            "Guvenli-Sifre-123!",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(503, result.Failure.StatusCode);
        Assert.Equal(
            "KIMLIK_SERVISI_YAPILANDIRILAMADI",
            result.Failure.ErrorCode);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_DoesNotRevealAccountExistence()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """{"code":"user_already_exists"}""");
        var service = CreateService(handler);

        var result = await service.RegisterAsync(
            "stajyer@example.com",
            "Guvenli-Sifre-123!",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(400, result.Failure.StatusCode);
        Assert.Equal("KIMLIK_ISTEGI_REDEDILDI", result.Failure.ErrorCode);
        Assert.DoesNotContain(
            "zaten",
            result.Failure.Detail,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_MapsRotatedSession()
    {
        var userId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            $$"""
            {
              "access_token": "new-access-token",
              "refresh_token": "new-refresh-token",
              "token_type": "bearer",
              "expires_in": 3600,
              "user": {
                "id": "{{userId}}",
                "email": "stajyer@example.com"
              }
            }
            """);
        var service = CreateService(handler);

        var result = await service.RefreshAsync(
            "old-refresh-token",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("new-access-token", result.Value.AccessToken);
        Assert.Equal("new-refresh-token", result.Value.RefreshToken);
        Assert.Equal(
            "/auth/v1/token?grant_type=refresh_token",
            handler.RequestUri?.PathAndQuery);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(
            "old-refresh-token",
            request.RootElement.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task RefreshAsync_ReusedToken_ReturnsGeneric401()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """{"code":"refresh_token_already_used"}""");
        var service = CreateService(handler);

        var result = await service.RefreshAsync(
            "used-refresh-token",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(401, result.Failure.StatusCode);
        Assert.Equal("OTURUM_GECERSIZ", result.Failure.ErrorCode);
        Assert.DoesNotContain(
            "refresh",
            result.Failure.Detail,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_UpstreamFailure_Returns503()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadGateway,
            """{"code":"refresh_token_already_used"}""");
        var service = CreateService(handler);

        var result = await service.RefreshAsync(
            "refresh-token",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(503, result.Failure.StatusCode);
        Assert.Equal(
            "KIMLIK_SERVISI_KULLANILAMIYOR",
            result.Failure.ErrorCode);
    }

    [Fact]
    public async Task LogoutAsync_UsesLocalScopeAndBearerToken()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.NoContent,
            string.Empty);
        var service = CreateService(handler);

        var result = await service.LogoutAsync(
            "access-token",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/auth/v1/logout?scope=local", handler.RequestUri?.PathAndQuery);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("access-token", handler.Authorization?.Parameter);
    }

    [Fact]
    public async Task LogoutAsync_MissingSession_IsIdempotent()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            """{"code":"session_not_found"}""");
        var service = CreateService(handler);

        var result = await service.LogoutAsync(
            "already-signed-out-token",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResendSignupConfirmationAsync_SendsSignupRequest()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            "{}");
        var service = CreateService(handler);

        var result = await service.ResendSignupConfirmationAsync(
            "bilinmeyen@example.com",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("/auth/v1/resend", handler.RequestUri?.AbsolutePath);

        using var request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("signup", request.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            "bilinmeyen@example.com",
            request.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ResendSignupConfirmationAsync_CaptchaFailureIsNotHidden()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.BadRequest,
            """{"code":"captcha_failed"}""");
        var service = CreateService(handler);

        var result = await service.ResendSignupConfirmationAsync(
            "stajyer@example.com",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Failure);
        Assert.Equal(400, result.Failure.StatusCode);
        Assert.Equal("KIMLIK_ISTEGI_REDEDILDI", result.Failure.ErrorCode);
    }

    private static SupabaseAuthService CreateService(HttpMessageHandler handler)
    {
        return new SupabaseAuthService(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://project.supabase.co/auth/v1/")
            });
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string body,
        int? retryAfterSeconds = null) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? RequestBody { get; private set; }

        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization
        {
            get;
            private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization;

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            if (retryAfterSeconds.HasValue)
            {
                response.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(
                        TimeSpan.FromSeconds(retryAfterSeconds.Value));
            }

            return response;
        }
    }
}
