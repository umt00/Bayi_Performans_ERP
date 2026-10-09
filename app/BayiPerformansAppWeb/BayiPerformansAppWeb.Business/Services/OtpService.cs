using System;
using System.Security.Cryptography;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// OTP (One-Time Password) üretim, yönetim ve doğrulama motoru.
/// Kriptografik güvenli kod üretimi, SHA-256 tuzlama, TTL ve Brute-force koruması sağlar.
/// </summary>
public sealed class OtpService : IOtpService
{
    private readonly IOtpStore _otpStore;
    private readonly IOtpNotificationSender _notificationSender;
    private readonly OtpOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OtpService>? _logger;

    public OtpService(
        IOtpStore otpStore,
        IOtpNotificationSender notificationSender,
        IOptions<OtpOptions>? options = null,
        TimeProvider? timeProvider = null,
        ILogger<OtpService>? logger = null)
    {
        _otpStore = otpStore ?? throw new ArgumentNullException(nameof(otpStore));
        _notificationSender = notificationSender ?? throw new ArgumentNullException(nameof(notificationSender));
        _options = options?.Value ?? new OtpOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public async Task<ServiceResult<OtpUretimSonucu>> UretAsync(
        string hedef,
        string amac,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hedef))
        {
            return ServiceResult<OtpUretimSonucu>.Fail(
                "GECERSIZ_HEDEF",
                "OTP gönderilecek hedef (e-posta veya telefon) boş olamaz.");
        }

        string normalizedHedef = hedef.Trim();

        if (!MailAddress.TryCreate(normalizedHedef, out _))
        {
            return ServiceResult<OtpUretimSonucu>.Fail(
                "GECERSIZ_EPOSTA",
                "OTP gonderilecek gecerli bir e-posta adresi belirtilmelidir.");
        }

        string normalizedAmac = amac?.Trim().ToUpperInvariant() ?? OtpAmaclari.Login2Fa;

        if (!OtpAmaclari.GecerliMi(normalizedAmac))
        {
            return ServiceResult<OtpUretimSonucu>.Fail(
                "GECERSIZ_OTP_AMACI",
                $"Belirtilen OTP amacı geçersizdir: '{amac}'. Geçerli amaçlar: {string.Join(", ", OtpAmaclari.Login2Fa, OtpAmaclari.SifreSifirlama, OtpAmaclari.EpostaDogrulama, OtpAmaclari.KritikIslemOnay)}");
        }

        // 1. Yeniden Gönderim Sınırlaması (Cooldown Throttling)
        TimeSpan? remainingCooldown = await _otpStore.GetRemainingCooldownAsync(
            normalizedHedef,
            normalizedAmac,
            cancellationToken);

        if (remainingCooldown.HasValue && remainingCooldown.Value > TimeSpan.Zero)
        {
            int remainingSeconds = (int)Math.Ceiling(remainingCooldown.Value.TotalSeconds);
            return ServiceResult<OtpUretimSonucu>.Fail(
                "OTP_COOLDOWN_AKTIF",
                $"Lütfen yeni kod talep etmeden önce {remainingSeconds} saniye bekleyin.");
        }

        // 2. Kriptografik Güvenli OTP Kodu Üretimi
        string rawOtp = UretRastgeleKod(_options.KodUzunlugu);
        string salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string hash = HashKod(rawOtp, salt);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        TimeSpan ttl = TimeSpan.FromSeconds(_options.GecerlilikSuresiSaniye);
        DateTimeOffset bitisZamani = now.Add(ttl);
        string islemId = Guid.NewGuid().ToString("N");

        var record = new OtpRecord(
            IslemId: islemId,
            Hedef: normalizedHedef,
            Amac: normalizedAmac,
            KodHash: hash,
            Salt: salt,
            MaksimumDenemeSayisi: _options.MaksimumDenemeSayisi,
            MevcutDenemeSayisi: 0,
            OlusturulmaZamaniUtc: now,
            BitisZamaniUtc: bitisZamani);

        // 3. Store'a Kaydetme
        await _otpStore.SetOtpAsync(islemId, record, ttl, cancellationToken);

        // 4. Cooldown Kilidi Koyma
        TimeSpan cooldownDuration = TimeSpan.FromSeconds(_options.YenidenGonderimBeklemeSuresiSaniye);
        bool cooldownAcquired = await _otpStore.TrySetCooldownAsync(
            normalizedHedef,
            normalizedAmac,
            cooldownDuration,
            cancellationToken);

        if (!cooldownAcquired)
        {
            await _otpStore.DeleteOtpAsync(islemId, cancellationToken);
            return ServiceResult<OtpUretimSonucu>.Fail(
                "OTP_COOLDOWN_AKTIF",
                "Lutfen yeni kod talep etmeden once bekleyin.");
        }

        try
        {
            await _notificationSender.SendAsync(
                normalizedHedef,
                rawOtp,
                normalizedAmac,
                ttl,
                cancellationToken);
        }
        catch (OtpNotificationException ex)
        {
            await _otpStore.DeleteOtpAsync(islemId, cancellationToken);
            await _otpStore.DeleteCooldownAsync(
                normalizedHedef,
                normalizedAmac,
                cancellationToken);

            _logger?.LogWarning(
                ex,
                "OTP e-posta teslimi basarisiz. Hedef: {MaskedHedef}, Amac: {Amac}",
                MaskeleHedef(normalizedHedef),
                normalizedAmac);

            return ServiceResult<OtpUretimSonucu>.Fail(
                ServiceErrorKind.Unavailable,
                "OTP_EPOSTA_GONDERILEMEDI",
                "OTP e-postasi gecici olarak gonderilemiyor. Lutfen daha sonra tekrar deneyin.");
        }

        _logger?.LogInformation(
            "Yeni OTP üretildi. IslemId: {IslemId}, Hedef: {MaskedHedef}, Amac: {Amac}, TTL: {Ttl}s",
            islemId,
            MaskeleHedef(normalizedHedef),
            normalizedAmac,
            _options.GecerlilikSuresiSaniye);

        var sonuc = new OtpUretimSonucu(
            IslemId: islemId,
            Hedef: normalizedHedef,
            Amac: normalizedAmac,
            GecerlilikSaniyesi: _options.GecerlilikSuresiSaniye,
            YenidenGonderimBeklemeSaniyesi: _options.YenidenGonderimBeklemeSuresiSaniye,
            MaskelenmisHedef: MaskeleHedef(normalizedHedef),
            Kod: rawOtp); // Bildirim servisine / log'a / geliştirici çıktısına iletilmek üzere

        return ServiceResult<OtpUretimSonucu>.Success(sonuc);
    }

    public async Task<ServiceResult<OtpDogrulamaSonucu>> DogrulaAsync(
        string islemId,
        string kod,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(islemId))
        {
            return ServiceResult<OtpDogrulamaSonucu>.Fail(
                "GECERSIZ_ISLEM_ID",
                "Doğrulama için işlem ID belirtilmelidir.");
        }

        if (string.IsNullOrWhiteSpace(kod))
        {
            return ServiceResult<OtpDogrulamaSonucu>.Fail(
                "GECERSIZ_KOD",
                "Doğrulama için OTP kodu belirtilmelidir.");
        }

        string normalizedKod = kod.Trim();

        // 1. Kaydı Getir
        var record = await _otpStore.GetOtpAsync(islemId, cancellationToken);
        if (record is null)
        {
            return ServiceResult<OtpDogrulamaSonucu>.Success(
                new OtpDogrulamaSonucu(
                    Dogrulandi: false,
                    KalanDenemeHakki: 0,
                    HataKodu: "OTP_BULUNAMADI_VEYA_SURESI_DOLDU",
                    HataMesaji: "OTP kaydı bulunamadı veya süresi doldu. Lütfen yeni bir kod talep edin."));
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // 2. Süre Kontrolü
        if (now > record.BitisZamaniUtc)
        {
            await _otpStore.DeleteOtpAsync(islemId, cancellationToken);
            return ServiceResult<OtpDogrulamaSonucu>.Success(
                new OtpDogrulamaSonucu(
                    Dogrulandi: false,
                    KalanDenemeHakki: 0,
                    HataKodu: "OTP_SURESI_DOLDU",
                    HataMesaji: "OTP kodunun geçerlilik süresi dolmuştur."));
        }

        // 3. Brute-Force / Deneme Sayısı Kontrolü
        if (record.MevcutDenemeSayisi >= record.MaksimumDenemeSayisi)
        {
            await _otpStore.DeleteOtpAsync(islemId, cancellationToken);
            return ServiceResult<OtpDogrulamaSonucu>.Success(
                new OtpDogrulamaSonucu(
                    Dogrulandi: false,
                    KalanDenemeHakki: 0,
                    HataKodu: "OTP_KILITLENDI",
                    HataMesaji: "Maksimum hatalı deneme sayısına ulaşıldı. Güvenlik nedeniyle kod iptal edildi."));
        }

        // 4. Hash Karşılaştırması (Sabit Zamanlı Güvenli Karşılaştırma)
        string computedHash = HashKod(normalizedKod, record.Salt);
        bool isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(record.KodHash));

        if (isValid)
        {
            // Başarılı doğrulama: OTP'yi sil ve güvenli tek kullanımlık token üret
            await _otpStore.DeleteOtpAsync(islemId, cancellationToken);

            string verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            string verificationTokenHash = HashVerificationToken(verificationToken);
            TimeSpan verificationTokenTtl = TimeSpan.FromSeconds(
                _options.GecerlilikSuresiSaniye);

            await _otpStore.SetVerificationTokenAsync(
                verificationTokenHash,
                new OtpVerificationTokenRecord(
                    verificationTokenHash,
                    record.Hedef.Trim().ToLowerInvariant(),
                    record.Amac.Trim().ToUpperInvariant(),
                    now,
                    now.Add(verificationTokenTtl)),
                verificationTokenTtl,
                cancellationToken);

            _logger?.LogInformation(
                "OTP başarıyla doğrulandı. IslemId: {IslemId}, Hedef: {Hedef}, Amac: {Amac}",
                islemId,
                MaskeleHedef(record.Hedef),
                record.Amac);

            return ServiceResult<OtpDogrulamaSonucu>.Success(
                new OtpDogrulamaSonucu(
                    Dogrulandi: true,
                    DogrulamaToken: verificationToken));
        }

        // 5. Hatalı Kod Durumu: Deneme sayısını artır
        int currentAttempts = await _otpStore.IncrementAttemptCountAsync(islemId, cancellationToken);
        int remainingAttempts = Math.Max(0, record.MaksimumDenemeSayisi - currentAttempts);

        if (remainingAttempts <= 0)
        {
            await _otpStore.DeleteOtpAsync(islemId, cancellationToken);
            _logger?.LogWarning("OTP maksimum deneme aşıldı ve kilitlendi. IslemId: {IslemId}", islemId);

            return ServiceResult<OtpDogrulamaSonucu>.Success(
                new OtpDogrulamaSonucu(
                    Dogrulandi: false,
                    KalanDenemeHakki: 0,
                    HataKodu: "OTP_KILITLENDI",
                    HataMesaji: "Maksimum hatalı deneme sayısına ulaşıldı. Güvenlik nedeniyle bu OTP kodu iptal edilmiştir."));
        }

        _logger?.LogWarning("Hatalı OTP denemesi. IslemId: {IslemId}, Kalan Deneme: {Remaining}", islemId, remainingAttempts);

        return ServiceResult<OtpDogrulamaSonucu>.Success(
            new OtpDogrulamaSonucu(
                Dogrulandi: false,
                KalanDenemeHakki: remainingAttempts,
                HataKodu: "OTP_HATALI_KOD",
                HataMesaji: $"Girilen OTP kodu hatalıdır. Kalan deneme hakkınız: {remainingAttempts}"));
    }

    internal static string HashVerificationToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public async Task<ServiceResult<OtpDurumBilgisi>> GetirDurumAsync(
        string islemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(islemId))
        {
            return ServiceResult<OtpDurumBilgisi>.Fail(
                "GECERSIZ_ISLEM_ID",
                "İşlem ID belirtilmelidir.");
        }

        var record = await _otpStore.GetOtpAsync(islemId, cancellationToken);
        if (record is null)
        {
            return ServiceResult<OtpDurumBilgisi>.Fail(
                "OTP_BULUNAMADI",
                "OTP kaydı bulunamadı veya süresi doldu.");
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        int remainingSeconds = Math.Max(0, (int)Math.Ceiling((record.BitisZamaniUtc - now).TotalSeconds));
        int remainingAttempts = Math.Max(0, record.MaksimumDenemeSayisi - record.MevcutDenemeSayisi);
        bool isExpired = now > record.BitisZamaniUtc;
        bool isLocked = record.MevcutDenemeSayisi >= record.MaksimumDenemeSayisi;

        var durum = new OtpDurumBilgisi(
            IslemId: record.IslemId,
            MaskelenmisHedef: MaskeleHedef(record.Hedef),
            Amac: record.Amac,
            KalanGecerlilikSaniyesi: remainingSeconds,
            KalanDenemeHakki: remainingAttempts,
            SuresiDolduMu: isExpired,
            KilitlendiMi: isLocked);

        return ServiceResult<OtpDurumBilgisi>.Success(durum);
    }

    private static string UretRastgeleKod(int uzunluk)
    {
        int min = (int)Math.Pow(10, uzunluk - 1);
        int max = (int)Math.Pow(10, uzunluk);
        int code = RandomNumberGenerator.GetInt32(min, max);
        return code.ToString();
    }

    private static string HashKod(string kod, string salt)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes($"{kod}:{salt}");
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    private static string MaskeleHedef(string hedef)
    {
        if (string.IsNullOrWhiteSpace(hedef))
            return "***";

        if (hedef.Contains('@'))
        {
            string[] parts = hedef.Split('@', 2);
            string user = parts[0];
            string domain = parts[1];

            string maskedUser = user.Length switch
            {
                <= 2 => $"{user[0]}*",
                _ => $"{user[0]}***{user[^1]}"
            };

            return $"{maskedUser}@{domain}";
        }

        // Telefon veya diğer
        if (hedef.Length > 4)
        {
            return $"{hedef[..2]}***{hedef[^2..]}";
        }

        return "***";
    }
}
