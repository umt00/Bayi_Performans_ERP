namespace BayiPerformansAppWeb.Api.Configuration;

public sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public bool Enabled { get; set; }

    public string Url { get; set; } = "http://localhost:9200";

    public string IndexName { get; set; } = "satislar_index";

    public int TimeoutSeconds { get; set; } = 5;

    public string? ApiKey { get; set; }
}
