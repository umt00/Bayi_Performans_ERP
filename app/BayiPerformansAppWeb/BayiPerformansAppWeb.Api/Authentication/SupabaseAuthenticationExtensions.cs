using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using BayiPerformansAppWeb.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Tokens;

namespace BayiPerformansAppWeb.Api.Authentication;

internal static class SupabaseAuthenticationExtensions
{
    public static IServiceCollection AddSupabaseAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SupabaseAuthOptions>()
            .Bind(configuration.GetSection(SupabaseAuthOptions.SectionName))
            .Validate(
                HasValidProjectUrl,
                "SupabaseAuth:ProjectUrl must be an https Supabase project URL.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.PublishableKey)
                    || options.PublishableKey.StartsWith("sb_publishable_", StringComparison.Ordinal)
                    || options.PublishableKey.StartsWith("eyJ", StringComparison.Ordinal),
                "SupabaseAuth:PublishableKey must be a valid publishable key or anon token.")
            .Validate(
                options => string.Equals(
                    options.Audience,
                    "authenticated",
                    StringComparison.Ordinal),
                "SupabaseAuth:Audience must be authenticated.")
            .Validate(
                options => options.HttpTimeoutSeconds is >= 1 and <= 60,
                "SupabaseAuth:HttpTimeoutSeconds must be between 1 and 60.")
            .ValidateOnStart();

        services.AddHttpClient(
            SupabaseAuthOptions.HttpClientName,
            (provider, client) =>
            {
                var settings = provider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<SupabaseAuthOptions>>()
                    .Value;

                client.BaseAddress = new Uri(
                    $"{settings.ProjectUrl.TrimEnd('/')}/auth/v1/");
                client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "apikey",
                    settings.PublishableKey);
            });

        services.Configure<HttpClientFactoryOptions>(
            SupabaseAuthOptions.HttpClientName,
            options => options.ShouldRedactHeaderValue = _ => true);

        services.AddScoped<ISupabaseAuthService>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new SupabaseAuthService(
                factory.CreateClient(SupabaseAuthOptions.HttpClientName));
        });

        var configured = configuration
            .GetSection(SupabaseAuthOptions.SectionName)
            .Get<SupabaseAuthOptions>()
            ?? new SupabaseAuthOptions();
        var issuer = BuildIssuerOrPlaceholder(configured.ProjectUrl);
        var audience = string.IsNullOrWhiteSpace(configured.Audience)
            ? "authenticated"
            : configured.Audience;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = issuer;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.AutomaticRefreshInterval = TimeSpan.FromMinutes(10);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "email",
                    RoleClaimType = "role",
                    ValidAlgorithms =
                    [
                        SecurityAlgorithms.EcdsaSha256,
                        SecurityAlgorithms.RsaSha256
                    ]
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSupabaseClaims,
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        return ApiProblemDetailsResponse.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Kimlik dogrulanamadi.",
                            "Gecerli bir Supabase access token gonderilmelidir.",
                            "KIMLIK_DOGRULANAMADI",
                            context.HttpContext.RequestAborted);
                    },
                    OnForbidden = context => ApiProblemDetailsResponse.WriteAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Isleme izin verilmedi.",
                        "Kullanici bu islem icin yetkili degil.",
                        "YETKISIZ_ISLEM",
                        context.HttpContext.RequestAborted)
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(
                SupabaseAuthOptions.RateLimitPolicyName,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"{httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown"}:{httpContext.GetEndpoint()?.DisplayName
                        ?? httpContext.Request.Path.Value?.ToLowerInvariant()
                        ?? "unknown-endpoint"}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds)
                            .ToString(CultureInfo.InvariantCulture);
                }

                await ApiProblemDetailsResponse.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Cok fazla istek.",
                    "Bir sure bekleyip tekrar deneyin.",
                    "COK_FAZLA_ISTEK",
                    cancellationToken);
            };
        });

        return services;
    }

    private static Task ValidateSupabaseClaims(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var hasUserId = Guid.TryParse(
            principal?.FindFirstValue("sub"),
            out _);
        var hasSessionId = Guid.TryParse(
            principal?.FindFirstValue("session_id"),
            out _);
        var hasAuthenticatedRole = string.Equals(
            principal?.FindFirstValue("role"),
            "authenticated",
            StringComparison.Ordinal);
        var hasNonAnonymousClaim = bool.TryParse(
                principal?.FindFirstValue("is_anonymous"),
                out var isAnonymous)
            && !isAnonymous;

        if (!hasUserId
            || !hasSessionId
            || !hasAuthenticatedRole
            || !hasNonAnonymousClaim)
        {
            context.Fail("Supabase user token claims are invalid.");
        }

        return Task.CompletedTask;
    }

    private static bool HasValidProjectUrl(SupabaseAuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProjectUrl))
        {
            return true;
        }

        return Uri.TryCreate(options.ProjectUrl, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && uri.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildIssuerOrPlaceholder(string projectUrl)
    {
        return Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri)
            ? $"{uri.AbsoluteUri.TrimEnd('/')}/auth/v1"
            : "https://invalid.local/auth/v1";
    }
}
