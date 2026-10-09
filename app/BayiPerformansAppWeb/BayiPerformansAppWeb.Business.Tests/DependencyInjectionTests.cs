using BayiPerformansAppWeb.Business;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BayiPerformansAppWeb.Business.Tests;

/// <summary>
/// Business katmanının Dependency Injection kayıtlarını test eder.
///
/// Bu testlerin amacı gerçek database'e bağlanmak değildir.
///
/// Amaç:
///
/// 1. Business configuration'ın okunabildiğini,
/// 2. Business servislerinin DI container'a kaydedildiğini,
/// 3. Calculator ve engine sınıflarının çözülebildiğini,
/// 4. Business servislerinin ihtiyaç duyduğu repository
///    dependency'lerinin mevcut olduğunu,
/// 5. Scoped servislerin doğru şekilde oluşturulabildiğini
/// kontrol etmektir.
/// </summary>
public sealed class DependencyInjectionTests
{
    /// <summary>
    /// Testlerde kullanılacak örnek thresholds configuration'ını oluşturur.
    ///
    /// Gerçek thresholds.json dosyasına bağlı kalmak yerine
    /// test içerisinde memory configuration kullanıyoruz.
    ///
    /// Böylece test:
    ///
    /// - dosya yoluna,
    /// - çalışma dizinine,
    /// - publish klasörüne
    ///
    /// bağımlı olmaz.
    /// </summary>
    private static IConfiguration CreateConfiguration()
    {
        Dictionary<string, string?> configurationValues =
            new()
            {
                ["thresholds:marj_esigi_pct"] = "0.15",
                ["thresholds:max_iskonto_pct"] = "0.15",
                ["thresholds:hedef_risk_esigi_pct"] = "0.80",
                ["thresholds:stok_gun_esigi"] = "7",
                ["thresholds:min_marj_red_pct"] = "0.00",
                ["thresholds:iade_risk_esigi_pct"] = "0.10",

                ["thresholds:risk_weights:MARJ_ESIK_ALTI"] = "40",
                ["thresholds:risk_weights:HEDEF_ALTINDA"] = "30",
                ["thresholds:risk_weights:YUKSEK_ISKONTO"] = "20",
                ["thresholds:risk_weights:STOK_YETERSIZ"] = "20",
                ["thresholds:risk_weights:YUKSEK_IADE"] = "20",

                ["thresholds:risk_levels:DUSUK_max"] = "30",
                ["thresholds:risk_levels:ORTA_max"] = "60",
                ["thresholds:risk_levels:YUKSEK_max"] = "900"
            };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
    }

    /// <summary>
    /// Business servislerinin constructor'larında kullanılan
    /// repository dependency'lerini test container'ına ekler.
    ///
    /// Burada gerçek repository kullanmıyoruz.
    ///
    /// Çünkü bu test:
    ///
    /// - PostgreSQL,
    /// - EF Core,
    /// - PentaDbContext
    /// - gerçek connection string
    ///
    /// test etmek için yazılmadı.
    ///
    /// Sadece DI graph'ın kurulup kurulamadığını test ediyoruz.
    /// </summary>
    private static void RegisterRepositoryStubs(
        IServiceCollection services)
    {
        // --------------------------------------------------
        // BAYI REPOSITORY
        // --------------------------------------------------
        //
        // BayiPerformansService,
        // RiskListesiService ve
        // TeklifDegerlendirmeService
        //
        // IBayiReadRepository kullanıyor.
        //
        // Testte gerçek BayiReadRepository yerine
        // boş bir stub kullanıyoruz.
        // --------------------------------------------------

        services.AddScoped<IBayiReadRepository, StubBayiReadRepository>();

        // --------------------------------------------------
        // STOK REPOSITORY
        // --------------------------------------------------
        //
        // KritikStokService IStokReadRepository kullanıyor.
        // --------------------------------------------------

        services.AddScoped<IStokReadRepository, StubStokReadRepository>();

        // --------------------------------------------------
        // SATIS REPOSITORY
        // --------------------------------------------------
        //
        // Business servislerinden herhangi biri
        // ISatisReadRepository kullanıyorsa burada
        // dependency'sini karşılıyoruz.
        // --------------------------------------------------

        services.AddScoped<ISatisReadRepository, StubSatisReadRepository>();

        // --------------------------------------------------
        // URUN REPOSITORY
        // --------------------------------------------------

        services.AddScoped<IUrunReadRepository, StubUrunReadRepository>();

        // --------------------------------------------------
        // DEPO REPOSITORY
        // --------------------------------------------------

        services.AddScoped<IDepoReadRepository, StubDepoReadRepository>();

        // --------------------------------------------------
        // AYLIK HEDEF REPOSITORY
        // --------------------------------------------------

        services.AddScoped<
            IAylikHedefReadRepository,
            StubAylikHedefReadRepository>();

        services.AddScoped<IBayiWriteRepository, StubBayiWriteRepository>();
        services.AddScoped<IUrunWriteRepository, StubUrunWriteRepository>();
        services.AddScoped<IStokWriteRepository, StubStokWriteRepository>();
        services.AddScoped<IBayiDonemPerformansReadRepository, StubBayiDonemPerformansReadRepository>();
    }

    /// <summary>
    /// AddBayiPerformansBusiness çağrısından sonra
    /// bütün Business dependency graph'ının çözülebildiğini test eder.
    ///
    /// Bu test gerçek database bağlantısı kurmaz.
    ///
    /// Repository dependency'leri yukarıdaki stub sınıflar tarafından
    /// karşılanır.
    /// </summary>
    [Fact]
    public void AddBayiPerformansBusiness_ShouldResolveBusinessGraph()
    {
        // --------------------------------------------------
        // 1. Configuration oluştur
        // --------------------------------------------------

        IConfiguration configuration =
            CreateConfiguration();

        // --------------------------------------------------
        // 2. DI service collection oluştur
        // --------------------------------------------------

        ServiceCollection services =
            new();

        // --------------------------------------------------
        // 3. Repository dependency'lerini kaydet
        // --------------------------------------------------
        //
        // Business servislerinin constructor'larının istediği
        // repository interface'lerini burada karşılıyoruz.
        // --------------------------------------------------

        RegisterRepositoryStubs(services);

        // --------------------------------------------------
        // 4. Business katmanını kaydet
        // --------------------------------------------------

        services.AddBayiPerformansBusiness(configuration);

        // --------------------------------------------------
        // 5. DI container oluştur
        // --------------------------------------------------
        //
        // ValidateOnBuild = true:
        // Constructor dependency'lerinin gerçekten çözülebilir
        // olup olmadığını build aşamasında kontrol eder.
        //
        // ValidateScopes = true:
        // Scoped dependency'nin yanlışlıkla Singleton içerisinde
        // kullanılmasını yakalamaya yardımcı olur.
        // --------------------------------------------------

        using ServiceProvider provider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });

        // --------------------------------------------------
        // 6. BusinessRulesOptions çözülebiliyor mu?
        // --------------------------------------------------

        BusinessRulesOptions options =
            provider.GetRequiredService<BusinessRulesOptions>();

        Assert.NotNull(options);

        Assert.Equal(
            0.15m,
            options.MarjEsigiPct);

        Assert.Equal(
            7m,
            options.StokGunEsigi);

        // --------------------------------------------------
        // 7. Calculator'lar çözülebiliyor mu?
        // --------------------------------------------------

        using IServiceScope scope =
            provider.CreateScope();

        IServiceProvider scopedProvider =
            scope.ServiceProvider;

        SalesMetricsCalculator salesCalculator =
            scopedProvider.GetRequiredService<SalesMetricsCalculator>();

        StockMetricsCalculator stockCalculator =
            scopedProvider.GetRequiredService<StockMetricsCalculator>();

        RiskEngine riskEngine =
            scopedProvider.GetRequiredService<RiskEngine>();

        OfferDecisionEngine offerDecisionEngine =
            scopedProvider.GetRequiredService<OfferDecisionEngine>();

        Assert.NotNull(salesCalculator);
        Assert.NotNull(stockCalculator);
        Assert.NotNull(riskEngine);
        Assert.NotNull(offerDecisionEngine);

        // --------------------------------------------------
        // 8. Business servisleri çözülebiliyor mu?
        // --------------------------------------------------

        IBayiPerformansService bayiPerformansService =
            scopedProvider.GetRequiredService<IBayiPerformansService>();

        IRiskListesiService riskListesiService =
            scopedProvider.GetRequiredService<IRiskListesiService>();

        IKritikStokService kritikStokService =
            scopedProvider.GetRequiredService<IKritikStokService>();

        ITeklifDegerlendirmeService teklifDegerlendirmeService =
            scopedProvider.GetRequiredService<ITeklifDegerlendirmeService>();

        Assert.NotNull(bayiPerformansService);
        Assert.NotNull(riskListesiService);
        Assert.NotNull(kritikStokService);
        Assert.NotNull(teklifDegerlendirmeService);
    }

    /// <summary>
    /// StockMetricsCalculator'ın DI üzerinden
    /// BusinessRulesOptions.StokGunEsigi değerini aldığını test eder.
    ///
    /// thresholds configuration:
    ///
    /// stok_gun_esigi = 7
    ///
    /// olduğundan calculator içerisinde de eşik 7 olmalıdır.
    ///
    /// Bunu doğrudan private field'a erişmeden davranış üzerinden
    /// test ediyoruz.
    /// </summary>
    [Fact]
    public void AddBayiPerformansBusiness_ShouldConfigureStockMetricsCalculator()
    {
        // Configuration oluşturulur.
        IConfiguration configuration =
            CreateConfiguration();

        // Service collection oluşturulur.
        ServiceCollection services =
            new();

        // Calculator'ın kendisi dışında Business servislerinin
        // dependency graph'ının da tamam olması için
        // repository stub'larını ekliyoruz.
        RegisterRepositoryStubs(services);

        // Business DI kayıtları yapılır.
        services.AddBayiPerformansBusiness(configuration);

        // Container oluşturulur.
        using ServiceProvider provider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });

        // Scoped calculator alınır.
        using IServiceScope scope =
            provider.CreateScope();

        StockMetricsCalculator calculator =
            scope.ServiceProvider
                .GetRequiredService<StockMetricsCalculator>();

        // --------------------------------------------------
        // 7 günlük stok kritik olmamalıdır.
        // --------------------------------------------------
        //
        // Eşik:
        //
        // stok_gun_esigi = 7
        //
        // Calculator:
        //
        // tahmini stok günü < 7
        //
        // ise kritik kabul ediyor.
        //
        // Tam 7 gün kritik değildir.
        // --------------------------------------------------

        Assert.False(
            calculator.IsCritical(7m));

        // --------------------------------------------------
        // 6 günlük stok kritik olmalıdır.
        // --------------------------------------------------

        Assert.True(
            calculator.IsCritical(6m));

        // --------------------------------------------------
        // 10 günlük stok kritik değildir.
        // --------------------------------------------------

        Assert.False(
            calculator.IsCritical(10m));
    }

    // ======================================================
    // TEST REPOSITORY STUB'LARI
    // ======================================================
    //
    // Bunlar gerçek DataAccess repository'leri değildir.
    //
    // Amaç sadece Business servislerinin constructor'larında
    // ihtiyaç duyulan interface'leri DI container'a sağlamaktır.
    //
    // Bu testlerde repository metotları çağrılmadığı için
    // metotların sonuçları boş/null olarak döndürülür.
    // ======================================================

    private sealed class StubBayiReadRepository
        : IBayiReadRepository
    {
        public Task<BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel?> GetByCodeAsync(
            string bayiKodu,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel?>(
                null);
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel>> ListAsync(
            string? segment,
            string? bolge,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel>());
        }
    }

    private sealed class StubStokReadRepository
        : IStokReadRepository
    {
        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.StokReadModel>> ListAsync(
            string? depoKodu,
            string? urunKodu,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.StokReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.StokReadModel>());
        }

        public Task<BayiPerformansAppWeb.Entities.ReadModels.StokReadModel?> GetAsync(
            string urunKodu,
            string depoKodu,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                BayiPerformansAppWeb.Entities.ReadModels.StokReadModel?>(
                null);
        }
    }

    private sealed class StubSatisReadRepository
        : ISatisReadRepository
    {
        public Task<DateOnly?> GetLatestSaleDateAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<DateOnly?>(null);
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>> ListByPeriodAsync(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>());
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>> ListByDealerAndPeriodAsync(
            string bayiKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>());
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>> ListByProductWarehouseAndPeriodAsync(
            string urunKodu,
            string depoKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.SatisReadModel>());
        }

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
            GetTotalSalesQuantitiesByPeriodAsync(
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(
                    new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        }

        public Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
            GetTotalSalesQuantitiesForDealerAsync(
                string bayiKodu,
                DateOnly periodStart,
                DateOnly periodEndExclusive,
                CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>(
                    new Dictionary<(string UrunKodu, string DepoKodu), decimal>());
        }

        public Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DateOnly>>(Array.Empty<DateOnly>());
        }
    }

    private sealed class StubBayiWriteRepository : IBayiWriteRepository
    {
        public Task<bool> EkleAsync(
            BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel bayi,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> GuncelleAsync(
            BayiPerformansAppWeb.Entities.ReadModels.BayiReadModel bayi,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubUrunWriteRepository : IUrunWriteRepository
    {
        public Task<bool> EkleAsync(
            BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel urun,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> GuncelleAsync(
            BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel urun,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubStokWriteRepository : IStokWriteRepository
    {
        public Task<bool> KaydetVeyaGuncelleAsync(
            BayiPerformansAppWeb.Entities.ReadModels.StokReadModel stok,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubUrunReadRepository
        : IUrunReadRepository
    {
        public Task<BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel?> GetByCodeAsync(
            string urunKodu,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel?>(
                null);
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel>> ListByCodesAsync(
            IReadOnlyCollection<string> urunKodlari,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel>());
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel>> ListAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.UrunReadModel>());
        }
    }

    private sealed class StubDepoReadRepository
        : IDepoReadRepository
    {
        public Task<BayiPerformansAppWeb.Entities.ReadModels.DepoReadModel?> GetByCodeAsync(
            string depoKodu,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                BayiPerformansAppWeb.Entities.ReadModels.DepoReadModel?>(
                null);
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.DepoReadModel>> ListAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.DepoReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.DepoReadModel>());
        }
    }

    private sealed class StubAylikHedefReadRepository
        : IAylikHedefReadRepository
    {
        public Task<BayiPerformansAppWeb.Entities.ReadModels.AylikHedefReadModel?> GetAsync(
            string bayiKodu,
            int yil,
            int ay,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                BayiPerformansAppWeb.Entities.ReadModels.AylikHedefReadModel?>(
                null);
        }

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.AylikHedefReadModel>> ListByPeriodAsync(
            int yil,
            int ay,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<
                IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.AylikHedefReadModel>>(
                Array.Empty<
                    BayiPerformansAppWeb.Entities.ReadModels.AylikHedefReadModel>());
        }
    }

    private sealed class StubBayiDonemPerformansReadRepository : IBayiDonemPerformansReadRepository
    {
        public Task<BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansPage> ListAsync(
            BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansQuery query,
            CancellationToken cancellationToken)
            => Task.FromResult(new BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansPage(query.Donem, 0, query.Sayfa, query.SayfaBoyutu, []));

        public Task<BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansReadModel?> GetAsync(
            string bayiKodu,
            DateOnly? donem,
            CancellationToken cancellationToken)
            => Task.FromResult<BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansReadModel?>(null);

        public Task<BayiPerformansAppWeb.Entities.ReadModels.PerformansDashboardStatsReadModel> GetDashboardStatsAsync(
            DateOnly? donem,
            CancellationToken cancellationToken)
            => Task.FromResult(new BayiPerformansAppWeb.Entities.ReadModels.PerformansDashboardStatsReadModel(donem, 0, 0, 0, 0, 0, null, null, null));

        public Task<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansReadModel>> GetHistoryAsync(
            string bayiKodu,
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BayiPerformansAppWeb.Entities.ReadModels.BayiDonemPerformansReadModel>>([]);
    }
}
