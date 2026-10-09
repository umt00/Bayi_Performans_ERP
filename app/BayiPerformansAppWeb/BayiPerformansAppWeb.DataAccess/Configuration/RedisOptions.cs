namespace BayiPerformansAppWeb.DataAccess.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; init; } = string.Empty;

    public bool AllowInMemoryFallback { get; init; }
}
