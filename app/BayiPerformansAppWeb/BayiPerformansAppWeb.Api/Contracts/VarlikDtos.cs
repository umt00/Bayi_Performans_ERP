using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Api.Contracts;

// --- BAYİ DTOS ---

public sealed record YeniBayiRequestDto(
    [Required(ErrorMessage = "Bayi kodu zorunludur.")]
    [property: JsonPropertyName("bayi_kodu")]
    string BayiKodu,

    [Required(ErrorMessage = "Bayi adı zorunludur.")]
    [property: JsonPropertyName("bayi_adi")]
    string BayiAdi,

    [property: JsonPropertyName("sehir")]
    string? Sehir,

    [property: JsonPropertyName("bolge")]
    string? Bolge,

    [property: JsonPropertyName("segment")]
    string? Segment);

public sealed record GuncelleBayiRequestDto(
    [property: JsonPropertyName("bayi_adi")]
    string? BayiAdi,

    [property: JsonPropertyName("sehir")]
    string? Sehir,

    [property: JsonPropertyName("bolge")]
    string? Bolge,

    [property: JsonPropertyName("segment")]
    string? Segment);

public sealed record BayiDetayResponseDto(
    [property: JsonPropertyName("bayi_kodu")]
    string BayiKodu,

    [property: JsonPropertyName("bayi_adi")]
    string? BayiAdi,

    [property: JsonPropertyName("sehir")]
    string? Sehir,

    [property: JsonPropertyName("bolge")]
    string? Bolge,

    [property: JsonPropertyName("segment")]
    string? Segment)
{
    public static BayiDetayResponseDto From(BayiReadModel model) =>
        new(
            BayiKodu: model.BayiKodu,
            BayiAdi: model.BayiAdi,
            Sehir: model.Sehir,
            Bolge: model.Bolge,
            Segment: model.Segment);
}

// --- ÜRÜN DTOS ---

public sealed record YeniUrunRequestDto(
    [Required(ErrorMessage = "Ürün kodu zorunludur.")]
    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [Required(ErrorMessage = "Ürün adı zorunludur.")]
    [property: JsonPropertyName("urun_adi")]
    string UrunAdi,

    [property: JsonPropertyName("kategori")]
    string? Kategori,

    [property: JsonPropertyName("alt_kategori")]
    string? AltKategori,

    [property: JsonPropertyName("marka")]
    string? Marka,

    [Required(ErrorMessage = "Birim maliyet zorunludur.")]
    [Range(0, 1000000000, ErrorMessage = "Birim maliyet 0 veya pozitif olmalıdır.")]
    [property: JsonPropertyName("birim_maliyet")]
    decimal? BirimMaliyet);

public sealed record UrunDetayResponseDto(
    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [property: JsonPropertyName("urun_adi")]
    string? UrunAdi,

    [property: JsonPropertyName("kategori")]
    string? Kategori,

    [property: JsonPropertyName("alt_kategori")]
    string? AltKategori,

    [property: JsonPropertyName("marka")]
    string? Marka,

    [property: JsonPropertyName("birim_maliyet")]
    decimal BirimMaliyet)
{
    public static UrunDetayResponseDto From(UrunReadModel model) =>
        new(
            UrunKodu: model.UrunKodu,
            UrunAdi: model.UrunAdi,
            Kategori: model.Kategori,
            AltKategori: model.AltKategori,
            Marka: model.Marka,
            BirimMaliyet: model.BirimMaliyet ?? 0m);
}

// --- STOK DTOS ---

public sealed record YeniStokRequestDto(
    [Required(ErrorMessage = "Ürün kodu zorunludur.")]
    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [Required(ErrorMessage = "Depo kodu zorunludur.")]
    [property: JsonPropertyName("depo_kodu")]
    string DepoKodu,

    [Required(ErrorMessage = "Mevcut stok zorunludur.")]
    [Range(0, 1000000000, ErrorMessage = "Mevcut stok 0 veya pozitif olmalıdır.")]
    [property: JsonPropertyName("mevcut_stok")]
    decimal? MevcutStok,

    [Required(ErrorMessage = "Minimum stok seviyesi zorunludur.")]
    [Range(0, 1000000000, ErrorMessage = "Minimum stok seviyesi 0 veya pozitif olmalıdır.")]
    [property: JsonPropertyName("min_stok_seviyesi")]
    decimal? MinStokSeviyesi);

public sealed record StokKayitResponseDto(
    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [property: JsonPropertyName("depo_kodu")]
    string DepoKodu,

    [property: JsonPropertyName("mevcut_stok")]
    decimal MevcutStok,

    [property: JsonPropertyName("min_stok_seviyesi")]
    decimal MinStokSeviyesi,

    [property: JsonPropertyName("son_sayim_tarihi")]
    string SonSayimTarihi)
{
    public static StokKayitResponseDto From(StokReadModel model) =>
        new(
            UrunKodu: model.UrunKodu,
            DepoKodu: model.DepoKodu,
            MevcutStok: model.MevcutStok ?? 0m,
            MinStokSeviyesi: model.MinStokSeviyesi ?? 0m,
            SonSayimTarihi: model.SonSayimTarihi?.ToString("yyyy-MM-dd") ?? string.Empty);
}
