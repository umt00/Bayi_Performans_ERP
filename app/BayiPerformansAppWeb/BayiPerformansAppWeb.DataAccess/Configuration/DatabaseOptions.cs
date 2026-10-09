namespace BayiPerformansAppWeb.DataAccess.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; init; } = string.Empty;

    public int CommandTimeoutSeconds { get; init; } = 30;

    // Supabase'in paylasilan baglanti limitini tek bir API instance'inin
    // tuketmesini engellemek icin uygulama havuzu bilerek kucuk tutulur.
    public int MaximumPoolSize { get; init; } = 5;

    public int MinimumPoolSize { get; init; } = 0;

    public int ConnectionIdleLifetimeSeconds { get; init; } = 60;
}
