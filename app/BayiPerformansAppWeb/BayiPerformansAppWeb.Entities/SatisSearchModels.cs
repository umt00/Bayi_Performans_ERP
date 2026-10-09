namespace BayiPerformansAppWeb.Entities.Search;

/// <summary>
/// Elasticsearch'teki satislar_index üzerinde uygulanacak salt-okunur arama
/// ölçütleridir. Tarih aralığının bitişi dahildir.
/// </summary>
public sealed record SatisSearchQuery(
    string? Arama,
    DateOnly? BaslangicTarihi,
    DateOnly? BitisTarihi,
    string? BayiKodu,
    string? UrunKodu,
    string? DepoKodu,
    string? Bolge,
    string? Segment,
    int Sayfa,
    int SayfaBoyutu);

public sealed record SatisSearchItem(
    string? SatisId,
    DateTimeOffset? Tarih,
    string? BayiKodu,
    string? BayiAdi,
    string? BayiSehir,
    string? Bolge,
    string? Segment,
    string? UrunKodu,
    string? UrunAdi,
    string? Kategori,
    string? Marka,
    string? DepoKodu,
    string? DepoAdi,
    decimal? Miktar,
    decimal? BirimFiyat,
    decimal? IskontoOrani,
    string? SatisKanali,
    bool? IsIade,
    decimal? Ciro,
    decimal? BrutKar);

public sealed record SatisSearchPage(
    long ToplamKayitSayisi,
    int Sayfa,
    int SayfaBoyutu,
    IReadOnlyList<SatisSearchItem> Kayitlar);
