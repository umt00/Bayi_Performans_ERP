using System.Net;
using System.Text;
using BayiPerformansAppWeb.Api.Configuration;
using BayiPerformansAppWeb.Api.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.Tests.Infrastructure;

public sealed class ElasticsearchHealthCheckTests
{
    [Fact]
    public async Task GreenClusterAndExistingIndex_ReturnsHealthy()
    {
        var check = CreateCheck(HttpStatusCode.OK);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task MissingIndex_ReturnsDegraded()
    {
        var check = CreateCheck(HttpStatusCode.NotFound);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("satislar_index", result.Description);
    }

    [Fact]
    public async Task UnavailableCluster_ReturnsUnhealthy()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var check = CreateCheck(handler);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static ElasticsearchHealthCheck CreateCheck(
        HttpStatusCode indexStatusCode)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/_cluster/health")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"status\":\"green\"}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(indexStatusCode);
        });

        return CreateCheck(handler);
    }

    private static ElasticsearchHealthCheck CreateCheck(
        HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9200/")
        };
        var options = Options.Create(new ElasticsearchOptions
        {
            Enabled = true,
            Url = "http://localhost:9200",
            IndexName = "satislar_index",
            TimeoutSeconds = 5
        });

        return new ElasticsearchHealthCheck(client, options);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
