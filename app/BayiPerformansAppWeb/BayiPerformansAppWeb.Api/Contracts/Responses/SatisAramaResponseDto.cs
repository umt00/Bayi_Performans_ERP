using BayiPerformansAppWeb.Entities.Search;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record SatisAramaItemResponseDto(
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
    decimal? BrutKar)
{
    public static SatisAramaItemResponseDto From(SatisSearchItem item) =>
        new(
            item.SatisId,
            item.Tarih,
            item.BayiKodu,
            item.BayiAdi,
            item.BayiSehir,
            item.Bolge,
            item.Segment,
            item.UrunKodu,
            item.UrunAdi,
            item.Kategori,
            item.Marka,
            item.DepoKodu,
            item.DepoAdi,
            item.Miktar,
            item.BirimFiyat,
            item.IskontoOrani,
            item.SatisKanali,
            item.IsIade,
            item.Ciro,
            item.BrutKar);
}

public sealed record SatisAramaResponseDto(
    long ToplamKayitSayisi,
    int Sayfa,
    int SayfaBoyutu,
    IReadOnlyList<SatisAramaItemResponseDto> Kayitlar)
{
    public static SatisAramaResponseDto From(SatisSearchPage page) =>
        new(
            page.ToplamKayitSayisi,
            page.Sayfa,
            page.SayfaBoyutu,
            page.Kayitlar.Select(SatisAramaItemResponseDto.From).ToArray());
}
