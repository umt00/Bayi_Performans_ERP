using BayiPerformansAppWeb.DataAccess;
using BayiPerformansAppWeb.DataAccess.Configuration;
using Npgsql;

namespace BayiPerformansAppWeb.Api.Tests.Infrastructure;

public sealed class DatabaseConnectionPoolConfigurationTests
{
    [Fact]
    public void BuildRuntimeConnectionString_UygulamaHavuzuSinirlariniUygular()
    {
        var options = new DatabaseOptions
        {
            ConnectionString =
                "Host=localhost;Port=6543;Database=test;Username=test;Password=test",
            MaximumPoolSize = 5,
            MinimumPoolSize = 0,
            ConnectionIdleLifetimeSeconds = 60
        };

        string connectionString =
            DataAccessServiceCollectionExtensions
                .BuildRuntimeConnectionString(options);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.True(builder.Pooling);
        Assert.Equal(5, builder.MaxPoolSize);
        Assert.Equal(0, builder.MinPoolSize);
        Assert.Equal(60, builder.ConnectionIdleLifetime);
        Assert.Equal(10, builder.ConnectionPruningInterval);
    }

    [Fact]
    public void BuildRuntimeConnectionString_GizliBaglantiBilgileriniDegistirmez()
    {
        var options = new DatabaseOptions
        {
            ConnectionString =
                "Host=pooler.example;Port=6543;Database=postgres;Username=app_user;Password=secret;SSL Mode=VerifyFull",
            MaximumPoolSize = 4
        };

        string connectionString =
            DataAccessServiceCollectionExtensions
                .BuildRuntimeConnectionString(options);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.Equal("pooler.example", builder.Host);
        Assert.Equal(6543, builder.Port);
        Assert.Equal("postgres", builder.Database);
        Assert.Equal("app_user", builder.Username);
        Assert.Equal("secret", builder.Password);
        Assert.Equal(SslMode.VerifyFull, builder.SslMode);
        Assert.Equal(4, builder.MaxPoolSize);
    }
}
