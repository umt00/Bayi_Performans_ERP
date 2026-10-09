namespace BayiPerformansAppWeb.Business.Configuration;

/// <summary>
/// OTP (One-Time Password) iş kuralları ve süre yapılandırması.
/// </summary>
public sealed class OtpOptions
{
    public const string SectionName = "OtpSettings";

    /// <summary>
    /// Üretilen OTP kodunun geçerlilik süresi (saniye cinsinden). Varsayılan: 180 (3 dakika).
    /// </summary>
    public int GecerlilikSuresiSaniye { get; set; } = 180;

    /// <summary>
    /// Aynı hedef ve amaç için yeniden kod talep edilmeden önce beklenmesi gereken süre (saniye). Varsayılan: 60.
    /// </summary>
    public int YenidenGonderimBeklemeSuresiSaniye { get; set; } = 60;

    /// <summary>
    /// İzin verilen maksimum hatalı deneme sayısı. Bu sayı aşılınca OTP iptal edilir. Varsayılan: 3.
    /// </summary>
    public int MaksimumDenemeSayisi { get; set; } = 3;

    /// <summary>
    /// Üretilecek OTP kodunun basamak sayısı. Varsayılan: 6.
    /// </summary>
    public int KodUzunlugu { get; set; } = 6;
}
