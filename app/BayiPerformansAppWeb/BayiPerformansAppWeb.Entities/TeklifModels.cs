using System;
using System.Collections.Generic;

namespace BayiPerformansAppWeb.Entities;

/// <summary>
/// Teklif durumları.
/// </summary>
public static class TeklifDurumlari
{
    public const string Taslak = "TASLAK";
    public const string Bekliyor = "BEKLIYOR";
    public const string Degerlendirildi = "DEGERLENDIRILDI";
    public const string Onaylandi = "ONAYLANDI";
    public const string Reddedildi = "REDDEDILDI";

    public static bool GecerliMi(string? durum) =>
        string.Equals(durum, Taslak, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, Bekliyor, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, Degerlendirildi, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, Onaylandi, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, Reddedildi, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Teklif listeleme sorgusunun desteklediği filtreler.
/// </summary>
public sealed record TeklifListeFiltre(
    string? BayiKodu = null,
    string? Sehir = null,
    string? Bolge = null,
    string? Durum = null,
    DateTimeOffset? BaslangicTarihiUtc = null,
    DateTimeOffset? BitisTarihiUtc = null);

/// <summary>
/// Teklif oluşturma komutunda yer alan tekil bir ürün kalemi.
/// </summary>
public sealed record TeklifKalemCommand(
    string UrunKodu,
    string DepoKodu,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani);

/// <summary>
/// Çoklu ürün içeren yeni teklif oluşturma komutu.
/// </summary>
public sealed record YeniTeklifCommand(
    string BayiKodu,
    string? Aciklama,
    IReadOnlyList<TeklifKalemCommand> Kalemler);

/// <summary>
/// Teklif kaydındaki bir ürün kalemi.
/// </summary>
public sealed record TeklifKalemModel(
    int SiraNo,
    string UrunKodu,
    string? UrunAdi,
    string DepoKodu,
    string? DepoAdi,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani,
    decimal NetTutar);

/// <summary>
/// Veritabanında veya depoda saklanan teklif ana nesnesi.
/// </summary>
public sealed record TeklifModel(
    string TeklifId,
    string BayiKodu,
    string? BayiAdi,
    string? Aciklama,
    string Durum,
    decimal ToplamNetTutar,
    DateTimeOffset OlusturulmaTarihiUtc,
    IReadOnlyList<TeklifKalemModel> Kalemler,
    TeklifCokluDegerlendirmeOutput? DegerlendirmeSonucu = null,
    string? BayiSehir = null,
    string? BayiBolge = null,
    TeklifDegerlendirmeKaydi? KayitliDegerlendirme = null,
    TeklifAiYorum? AiYorum = null);

/// <summary>
/// Data ekibinin teklif_degerlendirme tablosunda tuttuğu tekil audit kaydı.
/// Yüzde alanları veritabanında 0-100 formatındadır.
/// </summary>
public sealed record TeklifDegerlendirmeKaydi(
    string Karar,
    int? RiskSkoru,
    string? RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari,
    decimal? BayiMarji,
    decimal? HedefTutturmaYuzdesi,
    decimal? OrtalamaIskontoOrani,
    decimal? IadeOrani,
    DateTimeOffset DegerlendirmeTarihi);

/// <summary>
/// Otomasyon ekibinin teklif_ai_yorumlari tablosuna yazdığı LLM çıktısı.
/// </summary>
public sealed record TeklifAiYorum(
    string? AiComment,
    string? AlertMessage,
    bool IsFallback,
    DateTimeOffset CreatedTime);

/// <summary>
/// Teklifteki bir kalemin karar motoru değerlendirme sonucu.
/// </summary>
public sealed record TeklifKalemDegerlendirmeOutput(
    int SiraNo,
    string UrunKodu,
    string? UrunAdi,
    string DepoKodu,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani,
    decimal BirimMaliyet,
    decimal NetTutar,
    decimal MarjPct,
    decimal MevcutStok,
    bool StokYeterliMi,
    string Karar,
    IReadOnlyList<string> GerekceKodlari);

/// <summary>
/// Çoklu kalem içeren teklifin genel karar motoru değerlendirme çıktısı.
/// </summary>
public sealed record TeklifCokluDegerlendirmeOutput(
    string TeklifId,
    string BayiKodu,
    string GenelKarar,
    IReadOnlyList<string> GenelGerekceKodlari,
    decimal ToplamNetTutar,
    decimal ToplamMaliyet,
    decimal GenelMarjPct,
    DateTimeOffset DegerlendirilmeTarihiUtc,
    IReadOnlyList<TeklifKalemDegerlendirmeOutput> KalemSonuclari,
    int? RiskSkoru = null,
    string? RiskSeviyesi = null,
    decimal? HedefTutturmaYuzdesi = null,
    decimal? OrtalamaIskontoOrani = null,
    decimal? IadeOrani = null,
    string? AiComment = null,
    string? AlertMessage = null,
    bool? IsFallback = null);
