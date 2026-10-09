using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Configuration;
using BayiPerformansAppWeb.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;
using System;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace BayiPerformansAppWeb.DataAccess;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddPentaDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Database:ConnectionString is required. Configure it with .NET User Secrets.")
            .Validate(
                HasValidConnectionString,
                "Database:ConnectionString must be a valid Npgsql connection string with Host, Database and Username.")
            .Validate(
                UsesVerifiedTls,
                "Database:ConnectionString must use SSL Mode=VerifyFull or VerifyCA.")
            .Validate(
                HasExistingRootCertificate,
                "Database:ConnectionString must reference an existing Root Certificate file. Configure the local path with .NET User Secrets.")
            .Validate(
                options => options.CommandTimeoutSeconds is >= 1 and <= 300,
                "Database:CommandTimeoutSeconds must be between 1 and 300.")
            .Validate(
                options => options.MaximumPoolSize is >= 1 and <= 10,
                "Database:MaximumPoolSize must be between 1 and 10.")
            .Validate(
                options => options.MinimumPoolSize >= 0
                    && options.MinimumPoolSize <= options.MaximumPoolSize,
                "Database:MinimumPoolSize must be between 0 and MaximumPoolSize.")
            .Validate(
                options => options.ConnectionIdleLifetimeSeconds is >= 30 and <= 300,
                "Database:ConnectionIdleLifetimeSeconds must be between 30 and 300.")
            .ValidateOnStart();

        services.AddDbContextPool<PentaDbContext>((provider, dbContextOptions) =>
        {
            var settings = provider
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;
            string runtimeConnectionString =
                BuildRuntimeConnectionString(settings);

            dbContextOptions
                .UseNpgsql(
                    runtimeConnectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.CommandTimeout(settings.CommandTimeoutSeconds);
                        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    })
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddScoped<IBayiReadRepository, BayiReadRepository>();
        services.AddScoped<IDepoReadRepository, DepoReadRepository>();
        services.AddScoped<ISatisReadRepository, SatisReadRepository>();
        services.AddScoped<IUrunReadRepository, UrunReadRepository>();
        services.AddScoped<IStokReadRepository, StokReadRepository>();
        services.AddScoped<IAylikHedefReadRepository, AylikHedefReadRepository>();
        services.AddScoped<IBayiDonemPerformansReadRepository, BayiDonemPerformansReadRepository>();

        services
            .AddOptions<ElasticsearchDataAccessOptions>()
            .Bind(configuration.GetSection(ElasticsearchDataAccessOptions.SectionName))
            .Validate(
                options => !options.Enabled || IsValidElasticsearchUrl(options.Url),
                "Elasticsearch:Url must be an absolute http or https base URL without credentials.")
            .Validate(
                options => !options.Enabled || IsValidElasticsearchIndexName(options.IndexName),
                "Elasticsearch:IndexName must be a lowercase Elasticsearch index name.")
            .Validate(
                options => !options.Enabled || options.TimeoutSeconds is >= 1 and <= 60,
                "Elasticsearch:TimeoutSeconds must be between 1 and 60.")
            .ValidateOnStart();

        services.AddHttpClient<IElasticsearchSalesSearch, ElasticsearchSalesSearch>(
            (provider, client) =>
            {
                var settings = provider
                    .GetRequiredService<IOptions<ElasticsearchDataAccessOptions>>()
                    .Value;

                client.BaseAddress = Uri.TryCreate(
                    settings.Url.TrimEnd('/') + "/",
                    UriKind.Absolute,
                    out var configuredUri)
                    ? configuredUri
                    : new Uri("http://localhost/");
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("ApiKey", settings.ApiKey.Trim());
                }
            });

        // Write Repositories (CRUD & Teklif Yönetimi)
        services.AddScoped<IBayiWriteRepository, BayiWriteRepository>();
        services.AddScoped<IUrunWriteRepository, UrunWriteRepository>();
        services.AddScoped<IStokWriteRepository, StokWriteRepository>();
        services.AddScoped<ITeklifWriteRepository, PostgresTeklifWriteRepository>();

        // Redis & OTP Storage Kaydı. Production ortamında Redis zorunludur;
        // bellek fallback'i yalnız açıkça izin verilen geliştirme ortamında kullanılır.
        services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .Validate(
                options => options.AllowInMemoryFallback
                    || !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Redis:ConnectionString is required when in-memory fallback is disabled.")
            .ValidateOnStart();

        var redisSettings = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>()
            ?? new RedisOptions();
        string? redisConnectionString = redisSettings.ConnectionString;

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                var redisOptions = ParseRedisConfiguration(redisConnectionString);
                redisOptions.AbortOnConnectFail = !redisSettings.AllowInMemoryFallback;
                redisOptions.ConnectTimeout = 3000;
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            }
            catch when (redisSettings.AllowInMemoryFallback)
            {
                // Yerel geliştirmede Redis yoksa açıkça izin verilen bellek deposu kullanılır.
            }
        }

        services.AddSingleton<IOtpStore, RedisOtpStore>();

        services
            .AddOptions<BrevoEmailOptions>()
            .Bind(configuration.GetSection(BrevoEmailOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey),
                "BrevoEmail:ApiKey is required when Brevo email is enabled. Configure it with .NET User Secrets.")
            .Validate(
                options => !options.Enabled || IsValidEmail(options.FromEmail),
                "BrevoEmail:FromEmail must be a valid, verified Brevo sender address.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FromName),
                "BrevoEmail:FromName is required when Brevo email is enabled.")
            .Validate(
                options => !options.Enabled || options.TimeoutSeconds is >= 1 and <= 120,
                "BrevoEmail:TimeoutSeconds must be between 1 and 120.")
            .ValidateOnStart();

        services.AddHttpClient<IOtpNotificationSender, BrevoOtpNotificationSender>(
            (provider, client) =>
            {
                var settings = provider
                    .GetRequiredService<IOptions<BrevoEmailOptions>>()
                    .Value;

                client.BaseAddress = BrevoOtpNotificationSender.ServiceBaseAddress;
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("BayiPerformansAppWeb/1.0");
            });

        return services;
    }

    internal static string BuildRuntimeConnectionString(
        DatabaseOptions options)
    {
        if (!TryBuildConnectionString(options.ConnectionString, out var builder))
        {
            builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        }

        builder.Pooling = true;
        builder.MaxPoolSize = options.MaximumPoolSize;
        builder.MinPoolSize = options.MinimumPoolSize;
        builder.ConnectionIdleLifetime = options.ConnectionIdleLifetimeSeconds;
        builder.ConnectionPruningInterval = 10;

        return builder.ConnectionString;
    }

    private static bool HasValidConnectionString(DatabaseOptions options)
    {
        if (!TryBuildConnectionString(options.ConnectionString, out var builder))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(builder.Host)
            && !string.IsNullOrWhiteSpace(builder.Database)
            && !string.IsNullOrWhiteSpace(builder.Username);
    }

    private static bool UsesVerifiedTls(DatabaseOptions options)
    {
        return TryBuildConnectionString(options.ConnectionString, out _);
    }

    private static bool HasExistingRootCertificate(DatabaseOptions options)
    {
        if (!TryBuildConnectionString(options.ConnectionString, out var builder))
        {
            return false;
        }

        if (builder.SslMode is not (SslMode.VerifyCA or SslMode.VerifyFull))
        {
            return true;
        }

        string? configuredPath = builder.RootCertificate;
        string? environmentPath =
            Environment.GetEnvironmentVariable("PGSSLROOTCERT");
        string? certificatePath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : environmentPath;

        return string.IsNullOrWhiteSpace(certificatePath) || File.Exists(certificatePath);
    }

    private static bool TryBuildConnectionString(
        string connectionString,
        out NpgsqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder = new NpgsqlConnectionStringBuilder();
            return false;
        }

        if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(connectionString);
                string[] userInfoParts = uri.UserInfo.Split(':', 2);
                builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort ? 5432 : uri.Port,
                    Database = uri.AbsolutePath.TrimStart('/'),
                    Username = Uri.UnescapeDataString(userInfoParts[0]),
                    Password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty,
                    SslMode = SslMode.Prefer,
                    TrustServerCertificate = true
                };
                return true;
            }
            catch
            {
                builder = new NpgsqlConnectionStringBuilder();
                return false;
            }
        }

        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
            return true;
        }
        catch (ArgumentException)
        {
            builder = new NpgsqlConnectionStringBuilder();
            return false;
        }
    }

    private static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && MailAddress.TryCreate(value, out _);

    private static bool IsValidElasticsearchUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/");
    }

    private static bool IsValidElasticsearchIndexName(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 255
            && Regex.IsMatch(
                value,
                "^[a-z0-9][a-z0-9._-]*$",
                RegexOptions.CultureInvariant);
    }

    private static ConfigurationOptions ParseRedisConfiguration(string value)
    {
        if (!value.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigurationOptions.Parse(value);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return ConfigurationOptions.Parse(value);
        }

        var options = new ConfigurationOptions
        {
            Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)
        };
        options.EndPoints.Add(uri.Host, uri.IsDefaultPort ? 6379 : uri.Port);

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            string[] credentials = uri.UserInfo.Split(':', 2);
            if (credentials.Length == 2)
            {
                options.User = Uri.UnescapeDataString(credentials[0]);
                options.Password = Uri.UnescapeDataString(credentials[1]);
            }
            else
            {
                options.Password = Uri.UnescapeDataString(credentials[0]);
            }
        }

        if (uri.AbsolutePath.Length > 1
            && int.TryParse(uri.AbsolutePath[1..], out int database))
        {
            options.DefaultDatabase = database;
        }

        return options;
    }
}
