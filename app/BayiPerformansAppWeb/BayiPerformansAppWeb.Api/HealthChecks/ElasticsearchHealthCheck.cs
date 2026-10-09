using System.Net;
using System.Text.Json;
using BayiPerformansAppWeb.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.HealthChecks;

public sealed class ElasticsearchHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ElasticsearchOptions _options;

    public ElasticsearchHealthCheck(
        HttpClient httpClient,
        IOptions<ElasticsearchOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var clusterResponse = await _httpClient.GetAsync(
                "_cluster/health",
                cancellationToken);

            if (!clusterResponse.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy(
                    $"Elasticsearch cluster health request returned HTTP {(int)clusterResponse.StatusCode}.");
            }

            string clusterStatus = await ReadClusterStatusAsync(
                clusterResponse,
                cancellationToken);

            if (clusterStatus is not ("green" or "yellow"))
            {
                return HealthCheckResult.Unhealthy(
                    "Elasticsearch cluster status is not green or yellow.");
            }

            using var indexRequest = new HttpRequestMessage(
                HttpMethod.Head,
                Uri.EscapeDataString(_options.IndexName));
            using var indexResponse = await _httpClient.SendAsync(
                indexRequest,
                cancellationToken);

            if (indexResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return HealthCheckResult.Degraded(
                    $"Elasticsearch is reachable but index '{_options.IndexName}' does not exist.");
            }

            if (!indexResponse.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy(
                    $"Elasticsearch index check returned HTTP {(int)indexResponse.StatusCode}.");
            }

            return HealthCheckResult.Healthy(
                $"Elasticsearch cluster is {clusterStatus} and index '{_options.IndexName}' is available.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy(
                "Elasticsearch health check timed out.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Unhealthy(
                "Elasticsearch could not be reached.");
        }
        catch (JsonException)
        {
            return HealthCheckResult.Unhealthy(
                "Elasticsearch returned an invalid cluster health response.");
        }
    }

    private static async Task<string> ReadClusterStatusAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("status", out var statusElement))
        {
            throw new JsonException("Cluster status property is missing.");
        }

        return statusElement.GetString()?.Trim().ToLowerInvariant()
            ?? throw new JsonException("Cluster status is empty.");
    }
}
