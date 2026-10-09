using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Teklif değerlendirme işlemlerini gerçekleştiren Business servisidir.
///
/// Bu servis:
/// - Bayinin varlığını kontrol eder.
/// - Ürünün varlığını kontrol eder.
/// - Deponun varlığını kontrol eder.
/// - İlgili ürün/depo stok bilgisini getirir.
/// - Teklifin marj, iskonto ve stok durumunu
///   OfferDecisionEngine üzerinden değerlendirir.
/// - Deterministik olarak APPROVE, REJECT veya
///   NEED_APPROVAL kararı üretir.
///
/// ÖNEMLİ:
/// - Veritabanına doğrudan erişmez.
/// - PentaDbContext oluşturmaz.
/// - Raw SQL kullanmaz.
/// - Supabase SDK kullanmaz.
/// - Başka bir endpoint veya HTTP servisi çağırmaz.
/// - Teklif veya stok verisi yazmaz.
/// - AI / LLM çağrısı yapmaz.
///
/// Bu endpoint yalnızca değerlendirme yapar.
/// </summary>
public sealed class TeklifDegerlendirmeService : ITeklifDegerlendirmeService
{
    private readonly IBayiReadRepository _bayiReadRepository;
    private readonly IUrunReadRepository _urunReadRepository;
    private readonly IDepoReadRepository _depoReadRepository;
    private readonly IStokReadRepository _stokReadRepository;

    private readonly OfferDecisionEngine _offerDecisionEngine;
    private readonly BusinessRulesOptions _options;

    /// <summary>
    /// Teklif değerlendirme servisinin bağımlılıklarını oluşturur.
    ///
    /// Repository'ler DataAccess katmanından,
    /// OfferDecisionEngine ve BusinessRulesOptions ise
    /// Business katmanından sağlanır.
    /// </summary>
    public TeklifDegerlendirmeService(
        IBayiReadRepository bayiReadRepository,
        IUrunReadRepository urunReadRepository,
        IDepoReadRepository depoReadRepository,
        IStokReadRepository stokReadRepository,
        OfferDecisionEngine offerDecisionEngine,
        BusinessRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(bayiReadRepository);
        ArgumentNullException.ThrowIfNull(urunReadRepository);
        ArgumentNullException.ThrowIfNull(depoReadRepository);
        ArgumentNullException.ThrowIfNull(stokReadRepository);
        ArgumentNullException.ThrowIfNull(offerDecisionEngine);
        ArgumentNullException.ThrowIfNull(options);

        _bayiReadRepository = bayiReadRepository;
        _urunReadRepository = urunReadRepository;
        _depoReadRepository = depoReadRepository;
        _stokReadRepository = stokReadRepository;

        _offerDecisionEngine = offerDecisionEngine;
        _options = options;
    }

    /// <summary>
    /// Verilen teklif bilgisini deterministik olarak değerlendirir.
    ///
    /// İşlem sırası:
    ///
    /// 1. Komut doğrulanır.
    /// 2. Bayinin varlığı kontrol edilir.
    /// 3. Ürünün varlığı kontrol edilir.
    /// 4. Deponun varlığı kontrol edilir.
    /// 5. Ürün/depo stok bilgisi okunur.
    /// 6. Ürün maliyeti alınır.
    /// 7. OfferDecisionEngine ile teklif değerlendirilir.
    /// 8. TeklifDegerlendirmeOutput oluşturulur.
    ///
    /// Hiçbir veri yazılmaz.
    /// </summary>
    public async Task<ServiceResult<TeklifDegerlendirmeOutput>> DegerlendirAsync(
        TeklifDegerlendirmeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // --------------------------------------------------
        // 1. TEMEL KOMUT DOĞRULAMALARI
        // --------------------------------------------------
        //
        // Bayi, ürün ve depo kodları Business katmanı açısından
        // zorunlu alanlardır.
        //
        // Miktar, fiyat ve iskonto değerlerinin detaylı
        // doğrulaması OfferDecisionEngine tarafından yapılır.
        //

        if (string.IsNullOrWhiteSpace(command.BayiKodu))
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.Validation,
                "BAYI_KODU_ZORUNLU",
                "Bayi kodu boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(command.UrunKodu))
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.Validation,
                "URUN_KODU_ZORUNLU",
                "Ürün kodu boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(command.DepoKodu))
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.Validation,
                "DEPO_KODU_ZORUNLU",
                "Depo kodu boş olamaz.");
        }

        // --------------------------------------------------
        // 2. BAYİ KONTROLÜ
        // --------------------------------------------------
        //
        // Teklifin hangi bayiye ait olduğunu doğruluyoruz.
        //
        // Bayi bulunamazsa teklif değerlendirmesi yapılamaz.
        //

        var bayi =
            await _bayiReadRepository.GetByCodeAsync(
                command.BayiKodu,
                cancellationToken);

        if (bayi is null)
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.NotFound,
                "BAYI_BULUNAMADI",
                $"'{command.BayiKodu}' kodlu bayi bulunamadı.");
        }

        // --------------------------------------------------
        // 3. ÜRÜN KONTROLÜ
        // --------------------------------------------------
        //
        // Ürünün sistemde gerçekten bulunup bulunmadığını
        // kontrol ediyoruz.
        //
        // Ürün bulunamazsa maliyet bilgisi de alınamayacağı
        // için teklif değerlendirilemez.
        //

        var urun =
            await _urunReadRepository.GetByCodeAsync(
                command.UrunKodu,
                cancellationToken);

        if (urun is null)
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.NotFound,
                "URUN_BULUNAMADI",
                $"'{command.UrunKodu}' kodlu ürün bulunamadı.");
        }

        // --------------------------------------------------
        // 4. DEPO KONTROLÜ
        // --------------------------------------------------
        //
        // Teklifte belirtilen deponun sistemde bulunması gerekir.
        //

        var depo =
            await _depoReadRepository.GetByCodeAsync(
                command.DepoKodu,
                cancellationToken);

        if (depo is null)
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.NotFound,
                "DEPO_BULUNAMADI",
                $"'{command.DepoKodu}' kodlu depo bulunamadı.");
        }

        // --------------------------------------------------
        // 5. STOK BİLGİSİNİ GETİR
        // --------------------------------------------------
        //
        // Teklif değerlendirmesinde stok miktarı kritik bir
        // girdidir.
        //
        // Stok kaydı bulunamadığında bunu sessizce 0 kabul
        // etmiyoruz.
        //
        // Çünkü şartnameye göre nullable / eksik veriyi
        // sessizce 0'a çevirmek doğru değildir.
        //

        var stok =
            await _stokReadRepository.GetAsync(
                command.UrunKodu,
                command.DepoKodu,
                cancellationToken);

        if (stok is null)
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.NotFound,
                "STOK_BILGISI_BULUNAMADI",
                $"'{command.UrunKodu}' ürünü için '{command.DepoKodu}' deposunda stok bilgisi bulunamadı.");
        }

        // --------------------------------------------------
        // 6. MEVCUT STOK KONTROLÜ
        // --------------------------------------------------
        //
        // Stok kaydı var ancak mevcut stok alanı nullable ise
        // bunu 0'a çevirmiyoruz.
        //
        // Eksik stok miktarıyla güvenilir teklif kararı
        // üretilemeyeceğinden kontrollü hata döndürüyoruz.
        //

        if (!stok.MevcutStok.HasValue)
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.Unavailable,
                "STOK_MIKTARI_YOK",
                $"'{command.UrunKodu}' ürünü için '{command.DepoKodu}' deposundaki mevcut stok miktarı bulunamadı.");
        }

        decimal mevcutStok =
            stok.MevcutStok.Value;

        // --------------------------------------------------
        // 7. ÜRÜN MALİYETİ KONTROLÜ
        // --------------------------------------------------
        //
        // OfferDecisionEngine marj hesabı için birim maliyete
        // ihtiyaç duyar.
        //
        // Maliyet bilgisi nullable ise bunu 0 kabul etmek
        // yanlış olur.
        //
        // Bu nedenle kontrollü bir ServiceResult döndürüyoruz.
        //

        if (!urun.BirimMaliyet.HasValue)
        {
            return ServiceResult<TeklifDegerlendirmeOutput>.Fail(
                ServiceErrorKind.Unavailable,
                "MALIYET_BILGISI_YOK",
                $"'{command.UrunKodu}' kodlu ürün için birim maliyet bilgisi bulunamadı.");
        }

        decimal birimMaliyet =
            urun.BirimMaliyet.Value;

        // --------------------------------------------------
        // 8. TEKLİF KARAR MOTORU
        // --------------------------------------------------
        //
        // Burada asıl Business kararı OfferDecisionEngine
        // tarafından verilir.
        //
        // Motor:
        // - Miktarı
        // - Birim fiyatı
        // - İskonto oranını
        // - Birim maliyeti
        // - Mevcut stoku
        //
        // kullanarak:
        //
        // APPROVE
        // REJECT
        // NEED_APPROVAL
        //
        // kararlarından birini üretir.
        //
        // Ayrıca:
        // - MARJ_ESIK_ALTI
        // - YUKSEK_ISKONTO
        // - STOK_YETERSIZ
        //
        // gibi gerekçe kodlarını üretir.
        //

        OfferDecisionResult decision =
            _offerDecisionEngine.Evaluate(
                miktar: command.Miktar,
                birimFiyat: command.BirimFiyat,
                iskontoOrani: command.IskontoOrani,
                birimMaliyet: birimMaliyet,
                mevcutStok: mevcutStok);

        // --------------------------------------------------
        // 9. SERVICE OUTPUT OLUŞTUR
        // --------------------------------------------------
        //
        // Oranlar Business katmanında 0-1 formatında tutulur.
        //
        // Örneğin:
        //
        // 0.098 = %9,8
        // 0.15  = %15
        //
        // API katmanında gösterim yapılırken yüzde formatına
        // çevrilebilir.
        //
        // AI/LLM henüz uygulanmadığı için:
        //
        // AiComment = null
        // CommentSource = null
        //
        // bırakılır.
        //

        TeklifDegerlendirmeOutput output =
            new(
                Karar: decision.Karar,
                GerekceKodlari: decision.GerekceKodlari,
                HesaplananMarjPct: decision.HesaplananMarjPct,
                MarjEsigiPct: _options.MarjEsigiPct,
                MevcutStok: decision.MevcutStok,
                AiComment: null,
                CommentSource: null);

        // --------------------------------------------------
        // 10. BAŞARILI SONUÇ
        // --------------------------------------------------
        //
        // ServiceResult<T> yapısını değiştirmiyoruz.
        //
        // Doğru kullanım:
        //
        // ServiceResult<TeklifDegerlendirmeOutput>.Success(...)
        //
        // --------------------------------------------------

        return ServiceResult<TeklifDegerlendirmeOutput>.Success(
            output);
    }
}