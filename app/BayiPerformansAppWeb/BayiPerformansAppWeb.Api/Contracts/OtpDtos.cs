using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BayiPerformansAppWeb.Api.Contracts;

public sealed record OtpGonderRequestDto(
    [property: JsonPropertyName("hedef")]
    string? Hedef = null,

    [property: JsonPropertyName("amac")]
    string? Amac = "LOGIN_2FA",

    [property: JsonPropertyName("email")]
    string? Email = null,

    [property: JsonPropertyName("islemTipi")]
    string? IslemTipi = null)
{
    public string EffectiveHedef => !string.IsNullOrWhiteSpace(Hedef) ? Hedef : (Email ?? string.Empty);
    public string EffectiveAmac => !string.IsNullOrWhiteSpace(Amac) ? Amac : (IslemTipi ?? "LOGIN_2FA");
}

/// <summary>
/// OTP kodu üretim ve gönderim yanıt DTO'su.
/// </summary>
public sealed record OtpGonderResponseDto(
    [property: JsonPropertyName("islem_id")]
    string IslemId,

    [property: JsonPropertyName("hedef")]
    string Hedef,

    [property: JsonPropertyName("maskelenmis_hedef")]
    string MaskelenmisHedef,

    [property: JsonPropertyName("amac")]
    string Amac,

    [property: JsonPropertyName("gecerlilik_saniyesi")]
    int GecerlilikSaniyesi,

    [property: JsonPropertyName("yeniden_gonderim_saniyesi")]
    int YenidenGonderimSaniyesi);

/// <summary>
/// OTP doğrulama isteği DTO'su.
/// </summary>
public sealed record OtpDogrulaRequestDto(
    [property: JsonPropertyName("islem_id")]
    string? IslemId = null,

    [property: JsonPropertyName("kod")]
    string? Kod = null,

    [property: JsonPropertyName("islemId")]
    string? IslemIdCamel = null,

    [property: JsonPropertyName("code")]
    string? Code = null)
{
    public string EffectiveIslemId => !string.IsNullOrWhiteSpace(IslemId) ? IslemId : (IslemIdCamel ?? string.Empty);
    public string EffectiveKod => !string.IsNullOrWhiteSpace(Kod) ? Kod : (Code ?? string.Empty);
}

/// <summary>
/// OTP doğrulama sonucu DTO'su.
/// </summary>
public sealed record OtpDogrulaResponseDto(
    [property: JsonPropertyName("dogrulandi")]
    bool Dogrulandi,

    [property: JsonPropertyName("dogrulama_token")]
    string? DogrulamaToken = null,

    [property: JsonPropertyName("kalan_deneme_hakki")]
    int KalanDenemeHakki = 0,

    [property: JsonPropertyName("hata_kodu")]
    string? HataKodu = null,

    [property: JsonPropertyName("hata_mesaji")]
    string? HataMesaji = null);

/// <summary>
/// OTP işlem anlık durum yanıt DTO'su.
/// </summary>
public sealed record OtpDurumResponseDto(
    [property: JsonPropertyName("islem_id")]
    string IslemId,

    [property: JsonPropertyName("maskelenmis_hedef")]
    string MaskelenmisHedef,

    [property: JsonPropertyName("amac")]
    string Amac,

    [property: JsonPropertyName("kalan_gecerlilik_saniyesi")]
    int KalanGecerlilikSaniyesi,

    [property: JsonPropertyName("kalan_deneme_hakki")]
    int KalanDenemeHakki,

    [property: JsonPropertyName("suresi_doldu_mu")]
    bool SuresiDolduMu,

    [property: JsonPropertyName("kilitlendi_mi")]
    bool KilitlendiMi);
