using System.Net;
using System.Text;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Configuration;
using BayiPerformansAppWeb.DataAccess.Repositories;
using BayiPerformansAppWeb.Entities.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Api.Tests.DataAccess;

public sealed class ElasticsearchSalesSearchTests
{
    [Fact]
    public async Task SearchAsync_MapsCanonicalSalesDocument()
    {
        string? requestJson = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            requestJson = await request.Content!.ReadAsStringAsync();
            return JsonResponse(
                """
                {
                  "hits": {
                    "total": { "value": 1, "relation": "eq" },
                    "hits": [
                      {
                        "_source": {
                          "SatisID": "100234",
                          "Tarih": "2026-08-20T00:00:00Z",
                          "BayiKodu": "B001",
                          "BayiAdi": "Tech Market Istanbul",
                          "BayiSehir": "Istanbul",
                          "Bolge": "Marmara",
                          "Segment": "Platinum",
                          "UrunKodu": "U002",
                          "UrunAdi": "Gaming Laptop X",
                          "Kategori": "Bilgisayar",
                          "Marka": "XBrand",
                          "DepoKodu": "D01",
                          "DepoAdi": "Ana Depo",
                          "Miktar": 15.0,
                          "BirimFiyat": 35000.0,
                          "IskontoOrani": 0.05,
                          "SatisKanali": "B2B",
                          "IsIade": false,
                          "Ciro": 498750.0,
                          "BrutKar": 48750.0
                        }
                      }
                    ]
                  }
                }
                """);
        });
        var search = CreateSearch(handler);

        SatisSearchPage result = await search.SearchAsync(
            new SatisSearchQuery(
                "gaming laptop",
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                "B001",
                null,
                null,
                "Marmara",
                null,
                1,
                25),
            CancellationToken.None);

        Assert.Equal(1, result.ToplamKayitSayisi);
        var item = Assert.Single(result.Kayitlar);
        Assert.Equal("100234", item.SatisId);
        Assert.Equal("B001", item.BayiKodu);
        Assert.Equal(498750m, item.Ciro);
        Assert.False(item.IsIade);
        Assert.Contains("multi_match", requestJson);
        Assert.Contains("turkish_ngram_analyzer", requestJson);
        Assert.Contains("BayiKodu", requestJson);
        Assert.Contains("2026-08-01", requestJson);
    }

    [Fact]
    public async Task SearchAsync_WhenIndexIsMissing_ReturnsStableErrorCode()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var search = CreateSearch(handler);

        var exception = await Assert.ThrowsAsync<ElasticsearchSearchException>(
            () => search.SearchAsync(CreateQuery(), CancellationToken.None));

        Assert.Equal("ELASTICSEARCH_INDEX_BULUNAMADI", exception.ErrorCode);
    }

    [Fact]
    public async Task SearchAsync_WhenDisabled_DoesNotCallElasticsearch()
    {
        bool called = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            called = true;
            return Task.FromResult(JsonResponse("{}"));
        });
        var search = CreateSearch(handler, enabled: false);

        var exception = await Assert.ThrowsAsync<ElasticsearchSearchException>(
            () => search.SearchAsync(CreateQuery(), CancellationToken.None));

        Assert.False(called);
        Assert.Equal("ELASTICSEARCH_DEVRE_DISI", exception.ErrorCode);
    }

    private static ElasticsearchSalesSearch CreateSearch(
        HttpMessageHandler handler,
        bool enabled = true)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9200/")
        };
        var options = Options.Create(new ElasticsearchDataAccessOptions
        {
            Enabled = enabled,
            Url = "http://localhost:9200",
            IndexName = "satislar_index",
            TimeoutSeconds = 5
        });

        return new ElasticsearchSalesSearch(
            client,
            options,
            NullLogger<ElasticsearchSalesSearch>.Instance);
    }

    private static SatisSearchQuery CreateQuery() =>
        new(null, null, null, null, null, null, null, null, 1, 50);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(request);
    }
}
