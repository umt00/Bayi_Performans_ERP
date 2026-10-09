using System;

namespace BayiPerformansAppWeb.Entities;

/// <summary>
/// Yeni bayi oluşturma komutu.
/// </summary>
public sealed record YeniBayiCommand(
    string BayiKodu,
    string BayiAdi,
    string Sehir,
    string Bolge,
    string Segment);

/// <summary>
/// Bayi güncelleme komutu.
/// </summary>
public sealed record GuncelleBayiCommand(
    string BayiAdi,
    string Sehir,
    string Bolge,
    string Segment);

/// <summary>
/// Yeni ürün oluşturma komutu.
/// </summary>
public sealed record YeniUrunCommand(
    string UrunKodu,
    string UrunAdi,
    string Kategori,
    string AltKategori,
    string Marka,
    decimal BirimMaliyet);

/// <summary>
/// Stok kaydı oluşturma veya güncelleme komutu.
/// </summary>
public sealed record YeniStokCommand(
    string UrunKodu,
    string DepoKodu,
    decimal MevcutStok,
    decimal MinStokSeviyesi);

/// <summary>
/// Arka plan zamanlanmış hesaplama işinin anlık durumu.
/// </summary>
public sealed record JobDurumBilgisi(
    string IslemAdi,
    string Durum,
    DateTimeOffset? SonCalismaZamaniUtc,
    DateTimeOffset? SonrakiCalismaZamaniUtc,
    int IslenenBayiSayisi,
    string? SonHataMesaji);
