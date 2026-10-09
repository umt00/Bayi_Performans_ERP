using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

namespace BayiPerformansAppWeb.Api.Configuration;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ApiCorsOptions>()
            .Bind(configuration.GetSection(ApiCorsOptions.SectionName))
            .Validate(
                options => options.AllowedOrigins.All(IsValidOrigin),
                "Cors:AllowedOrigins must contain absolute http or https origins.")
            .ValidateOnStart();

        var allowedOrigins = configuration
            .GetSection($"{ApiCorsOptions.SectionName}:AllowedOrigins")
            .Get<string[]>()
            ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(ApiCorsOptions.PolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    policy
                        .SetIsOriginAllowed(_ => false)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    return;
                }

                policy
                    .WithOrigins(allowedOrigins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Istek dogrulanamadi.",
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["hata_kodu"] = "GECERSIZ_ISTEK";
                problem.Extensions["trace_id"] = context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problem);
            };
        });

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions.TryAdd(
                    "trace_id",
                    context.HttpContext.TraceIdentifier);

                if (!context.ProblemDetails.Extensions.ContainsKey("hata_kodu"))
                {
                    context.ProblemDetails.Extensions["hata_kodu"] =
                        context.ProblemDetails.Status == StatusCodes.Status404NotFound
                            ? "KAYNAK_BULUNAMADI"
                            : "BEKLENMEYEN_HATA";
                }
            };
        });
        services.AddExceptionHandler<DatabaseUnavailableExceptionHandler>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(
                AuthorizeOperationFilter.SchemeName,
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Supabase login cevabindaki access_token degerini girin."
                });
            options.OperationFilter<AuthorizeOperationFilter>();
        });

        return services;
    }

    private static bool IsValidOrigin(string origin)
    {
        return origin != "*"
            && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
