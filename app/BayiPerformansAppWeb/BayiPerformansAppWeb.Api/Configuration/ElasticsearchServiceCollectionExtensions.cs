using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using BayiPerformansAppWeb.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.Configuration;

public static partial class ElasticsearchServiceCollectionExtensions
{
    public static IServiceCollection AddElasticsearchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(ElasticsearchOptions.SectionName);

        services
            .AddOptions<ElasticsearchOptions>()
            .Bind(section)
            .Validate(
                options => !options.Enabled || IsValidBaseUrl(options.Url),
                "Elasticsearch:Url must be an absolute http or https base URL without path, query, fragment or credentials.")
            .Validate(
                options => !options.Enabled || IsValidIndexName(options.IndexName),
                "Elasticsearch:IndexName must be a lowercase Elasticsearch index name.")
            .Validate(
                options => !options.Enabled || options.TimeoutSeconds is >= 1 and <= 60,
                "Elasticsearch:TimeoutSeconds must be between 1 and 60.")
            .ValidateOnStart();

        var healthChecks = services.AddHealthChecks();
        var configuredOptions = section.Get<ElasticsearchOptions>() ?? new();

        if (!configuredOptions.Enabled)
        {
            return services;
        }

        services.AddHttpClient<ElasticsearchHealthCheck>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<IOptions<ElasticsearchOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.Url.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("ApiKey", options.ApiKey.Trim());
            }
        });

        healthChecks.AddCheck<ElasticsearchHealthCheck>(
            "elasticsearch",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "search"]);

        return services;
    }

    private static bool IsValidBaseUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/");
    }

    private static bool IsValidIndexName(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 255
            && ElasticsearchIndexNamePattern().IsMatch(value);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ElasticsearchIndexNamePattern();
}
