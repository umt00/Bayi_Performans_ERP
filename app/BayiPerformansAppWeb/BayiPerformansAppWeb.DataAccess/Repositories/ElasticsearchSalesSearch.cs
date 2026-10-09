using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Configuration;
using BayiPerformansAppWeb.Entities.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.DataAccess.Repositories;

internal sealed class ElasticsearchSalesSearch(
    HttpClient httpClient,
    IOptions<ElasticsearchDataAccessOptions> options,
    ILogger<ElasticsearchSalesSearch> logger)
    : IElasticsearchSalesSearch
{
    private static readonly string[] SearchFields =
    [
        "BayiAdi",
        "BayiSehir",
        "UrunAdi",
        "Kategori",
        "Marka",
        "DepoAdi"
    ];

    private static readonly string[] SourceFields =
    [
        "SatisID", "Tarih", "BayiKodu", "BayiAdi", "BayiSehir",
        "Bolge", "Segment", "UrunKodu", "UrunAdi", "Kategori", "Marka",
        "DepoKodu", "DepoAdi", "Miktar", "BirimFiyat", "IskontoOrani",
        "SatisKanali", "IsIade", "Ciro", "BrutKar"
    ];

    private readonly ElasticsearchDataAccessOptions _options = options.Value;

    public async Task<SatisSearchPage> SearchAsync(
        SatisSearchQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_options.Enabled)
        {
            throw new ElasticsearchSearchException(
                "ELASTICSEARCH_DEVRE_DISI",
                "Elasticsearch arama servisi bu ortamda etkin degil.");
        }

        var payload = BuildPayload(query);
        string requestPath = $"{Uri.EscapeDataString(_options.IndexName)}/_search";

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                requestPath,
                payload,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ElasticsearchSearchException(
                    "ELASTICSEARCH_INDEX_BULUNAMADI",
                    "Elasticsearch satis indexi bulunamadi.");
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Elasticsearch sales search returned HTTP {StatusCode}.",
                    (int)response.StatusCode);

                throw new ElasticsearchSearchException(
                    "ELASTICSEARCH_SORGU_HATASI",
                    "Elasticsearch arama istegi tamamlanamadi.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            return ParseResponse(document.RootElement, query);
        }
        catch (ElasticsearchSearchException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ElasticsearchSearchException(
                "ELASTICSEARCH_ZAMAN_ASIMI",
                "Elasticsearch arama istegi zaman asimina ugradi.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Elasticsearch sales search is unavailable.");
            throw new ElasticsearchSearchException(
                "ELASTICSEARCH_KULLANILAMIYOR",
                "Elasticsearch arama servisine su anda ulasilamiyor.",
                exception);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Elasticsearch returned an invalid search response.");
            throw new ElasticsearchSearchException(
                "ELASTICSEARCH_GECERSIZ_YANIT",
                "Elasticsearch gecersiz bir arama yaniti dondurdu.",
                exception);
        }
    }

    private static object BuildPayload(SatisSearchQuery query)
    {
        var must = new List<object>();
        var filters = new List<object>();

        if (!string.IsNullOrWhiteSpace(query.Arama))
        {
            must.Add(new
            {
                multi_match = new
                {
                    query = query.Arama.Trim(),
                    fields = SearchFields,
                    analyzer = "turkish_ngram_analyzer",
                    type = "best_fields",
                    @operator = "and"
                }
            });
        }

        AddTermFilter(filters, "BayiKodu", query.BayiKodu);
        AddTermFilter(filters, "UrunKodu", query.UrunKodu);
        AddTermFilter(filters, "DepoKodu", query.DepoKodu);
        AddTermFilter(filters, "Bolge", query.Bolge);
        AddTermFilter(filters, "Segment", query.Segment);

        if (query.BaslangicTarihi.HasValue || query.BitisTarihi.HasValue)
        {
            var rangeValues = new Dictionary<string, string>();
            if (query.BaslangicTarihi.HasValue)
            {
                rangeValues["gte"] = query.BaslangicTarihi.Value
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (query.BitisTarihi.HasValue)
            {
                rangeValues["lte"] = query.BitisTarihi.Value
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            filters.Add(new
            {
                range = new Dictionary<string, object>
                {
                    ["Tarih"] = rangeValues
                }
            });
        }

        return new
        {
            from = (query.Sayfa - 1) * query.SayfaBoyutu,
            size = query.SayfaBoyutu,
            track_total_hits = true,
            _source = SourceFields,
            sort = new object[]
            {
                new { Tarih = new { order = "desc" } },
                new { SatisID = new { order = "asc" } }
            },
            query = new
            {
                @bool = new
                {
                    must,
                    filter = filters
                }
            }
        };
    }

    private static void AddTermFilter(
        ICollection<object> filters,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        filters.Add(new
        {
            term = new Dictionary<string, string>
            {
                [field] = value.Trim()
            }
        });
    }

    private static SatisSearchPage ParseResponse(
        JsonElement root,
        SatisSearchQuery query)
    {
        JsonElement hitsContainer = root.GetProperty("hits");
        long total = ReadTotal(hitsContainer.GetProperty("total"));
        var items = new List<SatisSearchItem>();

        foreach (JsonElement hit in hitsContainer.GetProperty("hits").EnumerateArray())
        {
            if (!hit.TryGetProperty("_source", out JsonElement source))
            {
                continue;
            }

            items.Add(new SatisSearchItem(
                GetString(source, "SatisID"),
                GetDateTimeOffset(source, "Tarih"),
                GetString(source, "BayiKodu"),
                GetString(source, "BayiAdi"),
                GetString(source, "BayiSehir"),
                GetString(source, "Bolge"),
                GetString(source, "Segment"),
                GetString(source, "UrunKodu"),
                GetString(source, "UrunAdi"),
                GetString(source, "Kategori"),
                GetString(source, "Marka"),
                GetString(source, "DepoKodu"),
                GetString(source, "DepoAdi"),
                GetDecimal(source, "Miktar"),
                GetDecimal(source, "BirimFiyat"),
                GetDecimal(source, "IskontoOrani"),
                GetString(source, "SatisKanali"),
                GetBoolean(source, "IsIade"),
                GetDecimal(source, "Ciro"),
                GetDecimal(source, "BrutKar")));
        }

        return new SatisSearchPage(
            total,
            query.Sayfa,
            query.SayfaBoyutu,
            items);
    }

    private static long ReadTotal(JsonElement totalElement)
    {
        if (totalElement.ValueKind == JsonValueKind.Number)
        {
            return totalElement.GetInt64();
        }

        return totalElement.GetProperty("value").GetInt64();
    }

    private static string? GetString(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText().Trim('"');
    }

    private static decimal? GetDecimal(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetDecimal(out decimal number))
        {
            return number;
        }

        return decimal.TryParse(
            GetString(source, propertyName),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal parsed)
            ? parsed
            : null;
    }

    private static bool? GetBoolean(JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ when bool.TryParse(GetString(source, propertyName), out bool parsed) => parsed,
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(
        JsonElement source,
        string propertyName)
    {
        string? value = GetString(source, propertyName);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed)
            ? parsed
            : null;
    }
}
