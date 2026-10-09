using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using BayiPerformansAppWeb.Api.Authentication;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BayiPerformansAppWeb.Api.Tests.Authentication;

public sealed class AuthenticationPipelineTests
    : IClassFixture<AuthenticationPipelineTests.ApiFactory>
{
    private const string ValidOtpProof =
        "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationPipelineTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
    }

    [Fact]
    public void Startup_LoadsThresholdsAndUsesRealBusinessServices()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var options = services.GetRequiredService<BusinessRulesOptions>();

        Assert.Equal(7m, options.StokGunEsigi);
        Assert.IsType<BayiPerformansService>(
            services.GetRequiredService<IBayiPerformansService>());
        Assert.IsType<RiskListesiService>(
            services.GetRequiredService<IRiskListesiService>());
        Assert.IsType<KritikStokService>(
            services.GetRequiredService<IKritikStokService>());
        Assert.IsType<TeklifDegerlendirmeService>(
            services.GetRequiredService<ITeklifDegerlendirmeService>());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsProblemDetails401()
    {
        var response = await _client.GetAsync(
            "/bayi/B0012/performans?donem=2025-05");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "KIMLIK_DOGRULANAMADI",
            document.RootElement.GetProperty("hata_kodu").GetString());
        Assert.True(document.RootElement.TryGetProperty("trace_id", out _));
    }

    [Fact]
    public async Task Login_WithValidRequest_IsAnonymousAndMapsServiceResponse()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "stajyer@example.com",
                sifre = "Guvenli-Sifre-123!",
                dogrulama_token = ValidOtpProof
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains(
            response.Headers.Pragma,
            value => string.Equals(value.Name, "no-cache", StringComparison.OrdinalIgnoreCase));

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ApiFactory.UserId,
            document.RootElement.GetProperty("kullanici_id").GetGuid());
        Assert.Equal(
            "test-access-token",
            document.RootElement.GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task Login_WithoutOtpProof_VerifiesPasswordAndStartsOtpChallenge202()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "stajyer@example.com",
                sifre = "Guvenli-Sifre-123!"
            });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("otp_gerekli").GetBoolean());
        Assert.Equal(
            "login-islem-123",
            document.RootElement.GetProperty("islem_id").GetString());
    }

    [Fact]
    public async Task Login_WithEmptyOtpProof_StartsOtpChallenge202()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "stajyer@example.com",
                sifre = "Guvenli-Sifre-123!",
                dogrulama_token = string.Empty
            });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("otp_gerekli").GetBoolean());
    }

    [Fact]
    public async Task Login_WithoutOtpProof_WithInvalidPassword_DoesNotStartOtp()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "stajyer@example.com",
                sifre = "Yanlis-Sifre-123!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "GECERSIZ_KIMLIK_BILGISI",
            document.RootElement.GetProperty("hata_kodu").GetString());
    }

    [Fact]
    public async Task Login_WithRejectedOtpProof_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "stajyer@example.com",
                sifre = "Guvenli-Sifre-123!",
                dogrulama_token = new string('F', 64)
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "OTP_DOGRULAMA_TOKENI_GECERSIZ",
            document.RootElement.GetProperty("hata_kodu").GetString());
    }

    [Fact]
    public async Task Refresh_WithValidToken_IsAnonymousAndReturnsRotatedSession()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/token-yenile",
            new { refresh_token = "old-refresh-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "rotated-access-token",
            document.RootElement.GetProperty("access_token").GetString());
        Assert.Equal(
            "rotated-refresh-token",
            document.RootElement.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task Refresh_WithWhitespaceToken_ReturnsValidationProblem400()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/token-yenile",
            new { refresh_token = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "GECERSIZ_ISTEK",
            document.RootElement.GetProperty("hata_kodu").GetString());
    }

    [Fact]
    public async Task ResendConfirmation_WithValidEmail_IsAnonymousAndReturns202()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/onay-mailini-yeniden-gonder",
            new { email = "stajyer@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Logout_WithValidJwt_Returns204AndNoStore()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/cikis");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiFactory.CreateJwt("authenticated"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        var response = await _client.PostAsync("/auth/cikis", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidInput_ReturnsValidationProblem400()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/kayit",
            new
            {
                email = "gecersiz",
                sifre = "kisa"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "GECERSIZ_ISTEK",
            document.RootElement.GetProperty("hata_kodu").GetString());
    }

    [Fact]
    public async Task Me_WithValidSupabaseJwt_ReturnsVerifiedIdentity()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiFactory.CreateJwt("authenticated"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ApiFactory.UserId,
            document.RootElement.GetProperty("kullanici_id").GetGuid());
        Assert.Equal(
            "stajyer@example.com",
            document.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_WithAnonRoleJwt_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiFactory.CreateJwt("anon"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithWrongIssuer_Returns401()
    {
        using var request = CreateAuthenticatedRequest(
            ApiFactory.CreateJwt(
                "authenticated",
                issuer: "https://another-project.supabase.co/auth/v1"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithWrongAudience_Returns401()
    {
        using var request = CreateAuthenticatedRequest(
            ApiFactory.CreateJwt(
                "authenticated",
                audience: "anon"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        using var request = CreateAuthenticatedRequest(
            ApiFactory.CreateJwt(
                "authenticated",
                expires: DateTime.UtcNow.AddMinutes(-2)));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithWrongSignature_Returns401()
    {
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa)
        {
            KeyId = "other-test-key"
        };
        using var request = CreateAuthenticatedRequest(
            ApiFactory.CreateJwt(
                "authenticated",
                signingKey: otherKey));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("true")]
    [InlineData(null)]
    [InlineData("not-a-boolean")]
    public async Task Me_WithoutExplicitNonAnonymousClaim_Returns401(
        string? anonymousClaim)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ApiFactory.CreateJwt("authenticated", anonymousClaim));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_RequiresBearerOnlyForProtectedOperations()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        var login = paths
            .GetProperty("/auth/login")
            .GetProperty("post");
        Assert.False(login.TryGetProperty("security", out _));

        var refresh = paths
            .GetProperty("/auth/token-yenile")
            .GetProperty("post");
        Assert.False(refresh.TryGetProperty("security", out _));

        var resend = paths
            .GetProperty("/auth/onay-mailini-yeniden-gonder")
            .GetProperty("post");
        Assert.False(resend.TryGetProperty("security", out _));

        var logout = paths
            .GetProperty("/auth/cikis")
            .GetProperty("post");
        Assert.True(logout.TryGetProperty("security", out _));

        var performance = paths
            .GetProperty("/bayi/{kod}/performans")
            .GetProperty("get");
        Assert.True(performance.TryGetProperty("security", out var security));
        Assert.Equal("Bearer", security[0].EnumerateObject().Single().Name);
    }

    [Fact]
    public async Task Login_AfterTenRequestsInOneMinute_Returns429()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

        for (var requestNumber = 1; requestNumber <= 10; requestNumber++)
        {
            using var allowed = await client.PostAsJsonAsync(
                "/auth/login",
                new
                {
                    email = "stajyer@example.com",
                    sifre = "Guvenli-Sifre-123!",
                    dogrulama_token = ValidOtpProof
                });
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync(
            "/auth/login",
            new
            {
                email = "stajyer@example.com",
                sifre = "Guvenli-Sifre-123!",
                dogrulama_token = ValidOtpProof
            });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        using var document = JsonDocument.Parse(
            await rejected.Content.ReadAsStringAsync());
        Assert.Equal(
            "COK_FAZLA_ISTEK",
            document.RootElement.GetProperty("hata_kodu").GetString());
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token);
        return request;
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private const string Issuer =
            "https://project.supabase.co/auth/v1";
        private static readonly RsaSecurityKey SigningKey =
            new(RSA.Create(2048))
            {
                KeyId = "test-key"
            };

        public static readonly Guid UserId =
            Guid.Parse("11111111-2222-3333-4444-555555555555");
        private static readonly Guid SessionId =
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        public static string CreateJwt(
            string role,
            string? anonymousClaim = "false",
            string? issuer = null,
            string? audience = null,
            DateTime? expires = null,
            SecurityKey? signingKey = null)
        {
            var claims = new List<Claim>
            {
                new("sub", UserId.ToString()),
                new("session_id", SessionId.ToString()),
                new("role", role),
                new("email", "stajyer@example.com")
            };
            if (anonymousClaim is not null)
            {
                claims.Add(new Claim("is_anonymous", anonymousClaim));
            }

            var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
            var notBefore = expiresAt > DateTime.UtcNow
                ? DateTime.UtcNow.AddMinutes(-1)
                : expiresAt.AddMinutes(-5);
            var token = new JwtSecurityToken(
                issuer: issuer ?? Issuer,
                audience: audience ?? "authenticated",
                claims: claims,
                notBefore: notBefore,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(
                    signingKey ?? SigningKey,
                    SecurityAlgorithms.RsaSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            string testCertificatePath =
                typeof(AuthenticationPipelineTests).Assembly.Location;

            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.UseSetting(
                "Database:ConnectionString",
                $"Host=localhost;Database=test;Username=test;Password=test;SSL Mode=VerifyFull;Root Certificate={testCertificatePath}");
            builder.UseSetting("Database:CommandTimeoutSeconds", "30");
            builder.UseSetting(
                "SupabaseAuth:ProjectUrl",
                "https://project.supabase.co");
            builder.UseSetting(
                "SupabaseAuth:PublishableKey",
                "sb_publishable_test_key");
            builder.UseSetting("SupabaseAuth:Audience", "authenticated");
            builder.UseSetting("SupabaseAuth:HttpTimeoutSeconds", "15");

            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<ISupabaseAuthService>(
                    _ => new FakeAuthService());
                services.AddScoped<ILoginOtpDogrulamaService>(
                    _ => new FakeLoginOtpDogrulamaService());
                services.AddScoped<IOtpService>(
                    _ => new FakeOtpService());
                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        var configuration = new OpenIdConnectConfiguration
                        {
                            Issuer = Issuer
                        };
                        configuration.SigningKeys.Add(SigningKey);
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                configuration);
                    });
            });
        }

        private sealed class FakeAuthService : ISupabaseAuthService
        {
            public Task<SupabaseAuthResult<SupabaseRegistration>> RegisterAsync(
                string email,
                string password,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    SupabaseAuthResult<SupabaseRegistration>.Success(
                        new SupabaseRegistration(
                            UserId,
                            email,
                            EmailConfirmationRequired: true,
                            Session: null)));
            }

            public Task<SupabaseAuthResult<SupabaseSession>> LoginAsync(
                string email,
                string password,
                CancellationToken cancellationToken)
            {
                if (string.Equals(password, "Yanlis-Sifre-123!", StringComparison.Ordinal))
                {
                    return Task.FromResult(
                        SupabaseAuthResult<SupabaseSession>.Fail(
                            new SupabaseAuthFailure(
                                StatusCodes.Status401Unauthorized,
                                "GECERSIZ_KIMLIK_BILGISI",
                                "E-posta veya sifre gecersiz.",
                                RetryAfter: null)));
                }

                return Task.FromResult(
                    SupabaseAuthResult<SupabaseSession>.Success(
                        new SupabaseSession(
                            UserId,
                            email,
                            "test-access-token",
                            "test-refresh-token",
                            "bearer",
                            3600)));
            }

            public Task<SupabaseAuthResult<SupabaseSession>> RefreshAsync(
                string refreshToken,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    SupabaseAuthResult<SupabaseSession>.Success(
                        new SupabaseSession(
                            UserId,
                            "stajyer@example.com",
                            "rotated-access-token",
                            "rotated-refresh-token",
                            "bearer",
                            3600)));
            }

            public Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>> LogoutAsync(
                string accessToken,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    SupabaseAuthResult<SupabaseAuthAcknowledgement>.Success(
                        new SupabaseAuthAcknowledgement()));
            }

            public Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>>
                ResendSignupConfirmationAsync(
                    string email,
                    CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    SupabaseAuthResult<SupabaseAuthAcknowledgement>.Success(
                        new SupabaseAuthAcknowledgement()));
            }
        }

        private sealed class FakeLoginOtpDogrulamaService : ILoginOtpDogrulamaService
        {
            public Task<ServiceResult<bool>> DogrulamaTokeniniTuketAsync(
                string email,
                string dogrulamaToken,
                string amac,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    string.Equals(dogrulamaToken, ValidOtpProof, StringComparison.Ordinal)
                        ? ServiceResult<bool>.Success(true)
                        : ServiceResult<bool>.Fail(
                            ServiceErrorKind.Validation,
                            "OTP_DOGRULAMA_TOKENI_GECERSIZ",
                            "OTP dogrulama tokeni gecersizdir."));
            }
        }

        private sealed class FakeOtpService : IOtpService
        {
            public Task<ServiceResult<OtpUretimSonucu>> UretAsync(
                string hedef,
                string amac,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(
                    ServiceResult<OtpUretimSonucu>.Success(
                        new OtpUretimSonucu(
                            "login-islem-123",
                            hedef,
                            amac,
                            180,
                            60,
                            "s***r@example.com")));
            }

            public Task<ServiceResult<OtpDogrulamaSonucu>> DogrulaAsync(
                string islemId,
                string kod,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ServiceResult<OtpDurumBilgisi>> GetirDurumAsync(
                string islemId,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
