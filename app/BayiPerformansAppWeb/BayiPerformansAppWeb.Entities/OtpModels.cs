using System;

namespace BayiPerformansAppWeb.Entities;

/// <summary>
/// OTP (One-Time Password) kullanım amaçları.
/// </summary>
public static class OtpAmaclari
{
    public const string Login2Fa = "LOGIN_2FA";
    public const string SifreSifirlama = "SIFRE_SIFIRLAMA";
    public const string EpostaDogrulama = "EPOSTA_DOGRULAMA";
    public const string KritikIslemOnay = "KRITIK_ISLEM_ONAY";

    private static readonly HashSet<string> GecerliAmaclar = new(StringComparer.OrdinalIgnoreCase)
    {
        Login2Fa,
        SifreSifirlama,
        EpostaDogrulama,
        KritikIslemOnay
    };

    public static bool GecerliMi(string? amac) =>
        !string.IsNullOrWhiteSpace(amac) && GecerliAmaclar.Contains(amac);
}

/// <summary>
/// Veri saklama katmanında (Redis / Memory) tutulan OTP kayıt nesnesi.
/// </summary>
public sealed record OtpRecord(
    string IslemId,
    string Hedef,
    string Amac,
    string KodHash,
    string Salt,
    int MaksimumDenemeSayisi,
    int MevcutDenemeSayisi,
    DateTimeOffset OlusturulmaZamaniUtc,
    DateTimeOffset BitisZamaniUtc);

/// <summary>
/// OTP üretim işleminin sonucunu temsil eder.
/// </summary>
public sealed record OtpUretimSonucu(
    string IslemId,
    string Hedef,
    string Amac,
    int GecerlilikSaniyesi,
    int YenidenGonderimBeklemeSaniyesi,
    string MaskelenmisHedef,
    string? Kod = null);

/// <summary>
/// OTP doğrulama işleminin sonucunu temsil eder.
/// </summary>
public sealed record OtpDogrulamaSonucu(
    bool Dogrulandi,
    string? DogrulamaToken = null,
    int KalanDenemeHakki = 0,
    string? HataKodu = null,
    string? HataMesaji = null);

/// <summary>
/// Devam eden bir OTP işleminin anlık durum bilgisini temsil eder.
/// </summary>
public sealed record OtpDurumBilgisi(
    string IslemId,
    string MaskelenmisHedef,
    string Amac,
    int KalanGecerlilikSaniyesi,
    int KalanDenemeHakki,
    bool SuresiDolduMu,
    bool KilitlendiMi);

/// <summary>
/// Basarili OTP dogrulamasindan sonra uretilen tek kullanimlik giris kanitidir.
/// Ham token saklanmaz; yalnizca SHA-256 ozeti depolanir.
/// </summary>
public sealed record OtpVerificationTokenRecord(
    string TokenHash,
    string Hedef,
    string Amac,
    DateTimeOffset OlusturulmaZamaniUtc,
    DateTimeOffset BitisZamaniUtc);
