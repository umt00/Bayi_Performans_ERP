using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;

namespace BayiPerformansAppWeb.Business;

/// <summary>
/// Business katmanının Dependency Injection kayıtlarını sağlar.
///
/// API tarafında tek bir Business kayıt metodu kullanılır:
///
/// services.AddBayiPerformansBusiness(configuration);
///
/// Repository kayıtları DataAccess katmanında yapılır.
/// Business katmanı ise bu repository'leri kullanan servisleri
/// ve hesaplama/karar sınıflarını kaydeder.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Bayi Performans Business servislerini DI container'a kaydeder.
    ///
    /// Kayıt sırası:
    ///
    /// 1. BusinessRulesOptions configuration'dan okunur.
    /// 2. Configuration değerleri validate edilir.
    /// 3. BusinessRulesOptions DI container'a kaydedilir.
    /// 4. Calculator ve engine sınıfları kaydedilir.
    /// 5. TimeProvider kaydedilir.
    /// 6. Dört Business servisi Scoped olarak kaydedilir.
    ///
    /// Repository kullanan Business servisleri Singleton yapılmaz.
    /// Çünkü repository'lerin yaşam döngüsü Scoped'dur.
    /// </summary>
    public static IServiceCollection AddBayiPerformansBusiness(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // services veya configuration null gelirse
        // uygulamanın eksik dependency ile başlamasını engelleriz.
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // --------------------------------------------------
        // 1. BUSINESS RULES CONFIGURATION
        // --------------------------------------------------
        //
        // thresholds configuration bölümündeki değerleri
        // BusinessRulesOptions nesnesine dönüştürüyoruz.
        //
        // Örneğin configuration içerisinde:
        //
        // "thresholds": {
        //     "marj_esigi_pct": 0.15,
        //     "max_iskonto_pct": 0.15,
        //     "hedef_risk_esigi_pct": 0.80,
        //     "stok_gun_esigi": 7
        // }
        //
        // şeklinde değerler bulunması beklenir.
        //
        // Snake_case kullanan mevcut JSON sözleşmesini
        // değiştirmiyoruz.
        //
        // Bunun yerine değerleri tek tek okuyarak
        // BusinessRulesOptions içerisine aktarıyoruz.
        // --------------------------------------------------

        BusinessRulesOptions options =
            BindBusinessRulesOptions(configuration);

        // Configuration değerlerinin Business kurallarına
        // uygun olup olmadığını kontrol ediyoruz.
        //
        // Örneğin:
        // - Oranlar 0-1 arasında mı?
        // - Stok günü negatif mi?
        // - Risk ağırlıkları mevcut mu?
        // - Risk seviyeleri mantıklı mı?
        ValidateBusinessRulesOptions(options);

        // --------------------------------------------------
        // 2. BUSINESS RULES OPTIONS KAYDI
        // --------------------------------------------------
        //
        // Yukarıda okuyup doğruladığımız options nesnesini
        // DI container'a kaydediyoruz.
        //
        // Böylece Business servisleri ve calculator'lar
        // aynı doğrulanmış configuration değerlerini
        // kullanabilir.
        //
        // Bu singleton bir repository veya Business servisi
        // değildir.
        //
        // Sadece uygulama başlangıcında okunan configuration
        // değerlerini taşır.
        // --------------------------------------------------

        services.AddSingleton(options);

        // --------------------------------------------------
        // 3. CALCULATORS / DECISION ENGINE
        // --------------------------------------------------
        //
        // Bu sınıflar doğrudan database'e erişmez.
        //
        // Business servislerinden gelen veriler üzerinde
        // hesaplama ve karar üretimi yaparlar.
        // --------------------------------------------------

        // Satış metriklerini hesaplayan calculator.
        services.AddScoped<SalesMetricsCalculator>();

        // --------------------------------------------------
        // StockMetricsCalculator özel DI kaydı
        // --------------------------------------------------
        //
        // StockMetricsCalculator constructor'ı:
        //
        //     StockMetricsCalculator(decimal stokGunEsigi)
        //
        // şeklindedir.
        //
        // DI container'ın decimal değerini otomatik olarak
        // çözmesini beklemek doğru değildir.
        //
        // Bu nedenle factory kullanıyoruz.
        //
        // Önceden DI container'a kaydettiğimiz
        // BusinessRulesOptions içerisindeki StokGunEsigi
        // calculator constructor'ına aktarılır.
        // --------------------------------------------------

        services.AddScoped<StockMetricsCalculator>(
            provider =>
            {
                // DI container içerisindeki doğrulanmış
                // BusinessRulesOptions nesnesini alıyoruz.
                BusinessRulesOptions options =
                    provider.GetRequiredService<BusinessRulesOptions>();

                // Configuration'dan gelen stok gün eşiğini
                // calculator'a aktarıyoruz.
                return new StockMetricsCalculator(
                    options.StokGunEsigi);
            });

        // Risk skorunu ve risk seviyesini hesaplayan engine.
        services.AddScoped<RiskEngine>();

        // Teklif kararını veren decision engine.
        services.AddScoped<OfferDecisionEngine>();

        // --------------------------------------------------
        // 4. TIME PROVIDER
        // --------------------------------------------------
        //
        // KritikStokService hesaplama tarihini doğrudan
        // DateTime.Now veya DateTime.UtcNow üzerinden almak
        // yerine TimeProvider kullanır.
        //
        // TimeProvider.System gerçek sistem saatini sağlar.
        //
        // DI üzerinden kaydetmemizin avantajı:
        //
        // Production'da:
        //     TimeProvider.System
        //
        // Testlerde ise:
        //     Test amaçlı özel bir TimeProvider
        //
        // kullanılabilmesidir.
        // --------------------------------------------------

        services.AddSingleton(TimeProvider.System);

        // --------------------------------------------------
        // 5. BUSINESS SERVICES
        // --------------------------------------------------
        //
        // Dört ana Business servisini Scoped olarak kaydediyoruz.
        //
        // Bu servisler DataAccess katmanındaki Scoped
        // repository'leri kullandığı için Singleton
        // yapılmamalıdır.
        // --------------------------------------------------

        // Bayi performans servisinin interface -> implementation
        // eşleştirmesi.
        services.AddScoped<
            IBayiPerformansService,
            BayiPerformansService>();

        // Risk listesi servisinin interface -> implementation
        // eşleştirmesi.
        services.AddScoped<
            IRiskListesiService,
            RiskListesiService>();

        // Bayi özet KPI servisinin interface -> implementation
        // eşleştirmesi.
        services.AddScoped<
            IBayiOzetService,
            BayiOzetService>();

        // Bayi geçmiş trend servisinin interface -> implementation
        // eşleştirmesi.
        services.AddScoped<
            IBayiGecmisService,
            BayiGecmisService>();

        // Kritik stok servisinin interface -> implementation
        // eşleştirmesi.
        services.AddScoped<
            IKritikStokService,
            KritikStokService>();

        // Stok envanter ve özet servisinin interface -> implementation
        // eşleştirmesi.
        services.AddScoped<
            IStokEnvanterService,
            StokEnvanterService>();

        // Teklif değerlendirme servisinin interface ->
        // implementation eşleştirmesi.
        services.AddScoped<
            ITeklifDegerlendirmeService,
            TeklifDegerlendirmeService>();

        // Şehir ve bölge dağılım analizi servisinin interface ->
        // implementation eşleştirmesi.
        services.AddScoped<
            IBayiSehirDagilimService,
            BayiSehirDagilimService>();

        // Ürün bazlı çoklu depo stok matrisi servisinin interface ->
        // implementation eşleştirmesi.
        services.AddScoped<
            IUrunStokMatrisiService,
            UrunStokMatrisiService>();

        // Tanımlar ve filtre seçenekleri servisinin interface ->
        // implementation eşleştirmesi.
        services.AddScoped<
            ITanimlarService,
            TanimlarService>();

        // OTP (One-Time Password) ve 2FA servis kaydı.
        services.TryAddSingleton<IOtpStore, InMemoryOtpStore>();
        services.TryAddSingleton<IOtpNotificationSender, DisabledOtpNotificationSender>();
        services.Configure<OtpOptions>(
            configuration.GetSection(OtpOptions.SectionName));
        services.AddScoped<
            IOtpService,
            OtpService>();
        services.AddScoped<
            ILoginOtpDogrulamaService,
            LoginOtpDogrulamaService>();

        // Yazma ve Teklif Yönetim Repository Fallback'leri
        services.TryAddScoped<IBayiWriteRepository, BayiWriteRepository>();
        services.TryAddScoped<IUrunWriteRepository, UrunWriteRepository>();
        services.TryAddScoped<IStokWriteRepository, StokWriteRepository>();
        services.TryAddSingleton<ITeklifWriteRepository, TeklifWriteRepository>();

        // Teklif ve Varlık Yönetim Servisleri
        services.AddScoped<ITeklifYonetimService, TeklifYonetimService>();
        services.AddScoped<IBayiYonetimService, BayiYonetimService>();
        services.AddScoped<IUrunYonetimService, UrunYonetimService>();
        services.AddScoped<IStokYonetimService, StokYonetimService>();

        // Scheduled Skor Hesaplama Servisi
        services.AddScoped<IScheduledScoreJobService, ScheduledScoreJobService>();

        // Tüm Business kayıtları tamamlandı.
        return services;
    }

    /// <summary>
    /// Configuration içerisindeki "thresholds" bölümünü
    /// BusinessRulesOptions nesnesine dönüştürür.
    ///
    /// Mevcut thresholds.json snake_case sözleşmesini korur.
    ///
    /// Önemli:
    /// Bu metot thresholds bölümünün configuration'a daha önce
    /// eklenmiş olduğunu varsayar.
    /// </summary>
    private static BusinessRulesOptions BindBusinessRulesOptions(
        IConfiguration configuration)
    {
        // Configuration içerisindeki "thresholds" bölümünü alıyoruz.
        IConfigurationSection section =
            configuration.GetSection("thresholds");

        // --------------------------------------------------
        // Decimal configuration değerleri
        // --------------------------------------------------

        // Marj eşiğini okuyoruz.
        decimal marjEsigiPct =
            GetRequiredDecimal(
                section,
                "marj_esigi_pct");

        // Maksimum iskonto oranını okuyoruz.
        decimal maxIskontoPct =
            GetRequiredDecimal(
                section,
                "max_iskonto_pct");

        // Hedef risk eşiğini okuyoruz.
        decimal hedefRiskEsigiPct =
            GetRequiredDecimal(
                section,
                "hedef_risk_esigi_pct");

        // Kritik stok gün eşiğini okuyoruz.
        decimal stokGunEsigi =
            GetRequiredDecimal(
                section,
                "stok_gun_esigi");

        // Teklif değerlendirmesinde kullanılacak
        // minimum marj değerini okuyoruz.
        decimal minMarjRedPct =
            GetRequiredDecimal(
                section,
                "min_marj_red_pct");

        // İade risk eşiğini okuyoruz.
        decimal iadeRiskEsigiPct =
            GetRequiredDecimal(
                section,
                "iade_risk_esigi_pct");

        // --------------------------------------------------
        // Risk ağırlıkları
        // --------------------------------------------------
        //
        // Örneğin:
        //
        // "risk_weights": {
        //     "MARJ_ESIK_ALTI": 40,
        //     "HEDEF_ALTINDA": 30
        // }
        //
        // gibi değerler okunur.
        // --------------------------------------------------

        Dictionary<string, int> riskWeights =
            GetRequiredIntDictionary(
                section,
                "risk_weights");

        // --------------------------------------------------
        // Risk seviyeleri
        // --------------------------------------------------
        //
        // Örneğin:
        //
        // "risk_levels": {
        //     "DUSUK_max": 30,
        //     "ORTA_max": 60,
        //     "YUKSEK_max": 900
        // }
        //
        // gibi değerler okunur.
        // --------------------------------------------------

        Dictionary<string, int> riskLevels =
            GetRequiredIntDictionary(
                section,
                "risk_levels");

        // Okuduğumuz bütün configuration değerlerini
        // BusinessRulesOptions nesnesine aktarıyoruz.
        return new BusinessRulesOptions
        {
            MarjEsigiPct = marjEsigiPct,
            MaxIskontoPct = maxIskontoPct,
            HedefRiskEsigiPct = hedefRiskEsigiPct,
            StokGunEsigi = stokGunEsigi,
            MinMarjRedPct = minMarjRedPct,
            IadeRiskEsigiPct = iadeRiskEsigiPct,
            RiskWeights = riskWeights,
            RiskLevels = riskLevels
        };
    }

    /// <summary>
    /// Configuration içerisindeki zorunlu decimal değeri okur.
    ///
    /// Değer bulunamazsa veya decimal olarak parse edilemezse
    /// uygulamanın eksik/bozuk configuration ile başlamasını
    /// engeller.
    /// </summary>
    private static decimal GetRequiredDecimal(
        IConfiguration section,
        string key)
    {
        // İlgili configuration değerini alıyoruz.
        string? value = section[key];

        // Değer hiç yoksa veya boşsa hata veriyoruz.
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Business configuration değeri bulunamadı: thresholds:{key}");
        }

        // Configuration değerini invariant culture kullanarak
        // decimal'a dönüştürüyoruz.
        //
        // Böylece sistem kültüründen kaynaklanabilecek
        // virgül/nokta problemlerini azaltıyoruz.
        if (!decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal result))
        {
            throw new InvalidOperationException(
                $"Business configuration değeri geçersiz: " +
                $"thresholds:{key}={value}");
        }

        return result;
    }

    /// <summary>
    /// Risk ağırlıkları veya risk seviyeleri gibi
    /// dictionary configuration değerlerini okur.
    ///
    /// Örneğin:
    ///
    /// "risk_weights": {
    ///     "MARJ_ESIK_ALTI": 40,
    ///     "HEDEF_ALTINDA": 30
    /// }
    /// </summary>
    private static Dictionary<string, int> GetRequiredIntDictionary(
        IConfiguration section,
        string key)
    {
        // İlgili dictionary configuration bölümünü alıyoruz.
        IConfigurationSection dictionarySection =
            section.GetSection(key);

        // Bölüm hiç yoksa uygulamanın eksik configuration ile
        // başlamasını engelliyoruz.
        if (!dictionarySection.Exists())
        {
            throw new InvalidOperationException(
                $"Business configuration bölümü bulunamadı: thresholds:{key}");
        }

        // Configuration anahtarlarının sırasından bağımsız
        // deterministik erişim için Ordinal comparer kullanıyoruz.
        Dictionary<string, int> result =
            new(StringComparer.Ordinal);

        // Dictionary içerisindeki bütün child değerleri
        // tek tek okuyoruz.
        foreach (IConfigurationSection child
                 in dictionarySection.GetChildren())
        {
            // Boş key varsa dikkate almıyoruz.
            if (string.IsNullOrWhiteSpace(child.Key))
            {
                continue;
            }

            // Child değerini alıyoruz.
            string? value = child.Value;

            // Integer olarak parse etmeye çalışıyoruz.
            if (!int.TryParse(
                    value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int parsedValue))
            {
                throw new InvalidOperationException(
                    $"Business configuration değeri geçersiz: " +
                    $"thresholds:{key}:{child.Key}={value}");
            }

            // Parse edilen değeri dictionary içerisine ekliyoruz.
            result[child.Key] = parsedValue;
        }

        // Dictionary bölümü var ama boşsa yine configuration hatası
        // kabul ediyoruz.
        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                $"Business configuration bölümü boş: thresholds:{key}");
        }

        return result;
    }

    /// <summary>
    /// Bind edilmiş BusinessRulesOptions değerlerinin
    /// Business kurallarına uygun olup olmadığını kontrol eder.
    ///
    /// Böylece uygulama yanlış threshold değerleriyle
    /// sessizce çalışmaya başlamaz.
    /// </summary>
    private static void ValidateBusinessRulesOptions(
        BusinessRulesOptions options)
    {
        // --------------------------------------------------
        // Oranların 0-1 arasında olması gerekir.
        // --------------------------------------------------
        //
        // Örneğin:
        //
        // %15 = 0.15
        // %80 = 0.80
        // --------------------------------------------------

        ValidateRatio(
            options.MarjEsigiPct,
            nameof(options.MarjEsigiPct));

        ValidateRatio(
            options.MaxIskontoPct,
            nameof(options.MaxIskontoPct));

        ValidateRatio(
            options.HedefRiskEsigiPct,
            nameof(options.HedefRiskEsigiPct));

        ValidateRatio(
            options.MinMarjRedPct,
            nameof(options.MinMarjRedPct));

        ValidateRatio(
            options.IadeRiskEsigiPct,
            nameof(options.IadeRiskEsigiPct));

        // --------------------------------------------------
        // Stok günü negatif olamaz.
        // --------------------------------------------------

        if (options.StokGunEsigi < 0)
        {
            throw new InvalidOperationException(
                "StokGunEsigi negatif olamaz.");
        }

        // --------------------------------------------------
        // Risk configuration'ları zorunludur.
        // --------------------------------------------------

        if (options.RiskWeights is null ||
            options.RiskWeights.Count == 0)
        {
            throw new InvalidOperationException(
                "RiskWeights configuration boş olamaz.");
        }

        if (options.RiskLevels is null ||
            options.RiskLevels.Count == 0)
        {
            throw new InvalidOperationException(
                "RiskLevels configuration boş olamaz.");
        }

        // --------------------------------------------------
        // Risk seviyelerinin bulunması gerekir.
        // --------------------------------------------------
        //
        // RiskEngine'in kullanacağı üç temel seviye sınırının
        // configuration içerisinde bulunmasını zorunlu tutuyoruz.
        // --------------------------------------------------

        if (!options.RiskLevels.ContainsKey("DUSUK_max") ||
            !options.RiskLevels.ContainsKey("ORTA_max") ||
            !options.RiskLevels.ContainsKey("YUKSEK_max"))
        {
            throw new InvalidOperationException(
                "DUSUK_max, ORTA_max ve YUKSEK_max " +
                "risk level configuration değerleri zorunludur.");
        }

        // --------------------------------------------------
        // Risk sınırlarının mantıklı sırada olması gerekir.
        // --------------------------------------------------

        int dusukMax =
            options.RiskLevels["DUSUK_max"];

        int ortaMax =
            options.RiskLevels["ORTA_max"];

        int yuksekMax =
            options.RiskLevels["YUKSEK_max"];

        // Hiçbir risk seviyesi negatif olamaz.
        if (dusukMax < 0 ||
            ortaMax < 0 ||
            yuksekMax < 0)
        {
            throw new InvalidOperationException(
                "Risk level değerleri negatif olamaz.");
        }

        // Risk seviyeleri küçükten büyüğe doğru ilerlemelidir.
        //
        // Beklenen:
        //
        // DUSUK <= ORTA <= YUKSEK
        //
        // Örneğin:
        // 30 <= 60 <= 900
        if (dusukMax > ortaMax ||
            ortaMax > yuksekMax)
        {
            throw new InvalidOperationException(
                "Risk level sınırları DUSUK <= ORTA <= YUKSEK " +
                "şeklinde olmalıdır.");
        }
    }

    /// <summary>
    /// Bir oranın 0 ile 1 arasında olup olmadığını kontrol eder.
    ///
    /// Business katmanında oranlar decimal olarak tutulur.
    ///
    /// Örneğin:
    ///
    /// %15 = 0.15
    /// %80 = 0.80
    /// </summary>
    private static void ValidateRatio(
        decimal value,
        string propertyName)
    {
        // Oranın 0'dan küçük veya 1'den büyük olması
        // Business configuration hatasıdır.
        if (value < 0 || value > 1)
        {
            throw new InvalidOperationException(
                $"{propertyName} 0 ile 1 arasında olmalıdır. " +
                $"Mevcut değer: {value}");
        }
    }
}
