using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record KritikStokResponseDto(
    DateTimeOffset HesaplamaTarihi,
    int Toplam,
    IReadOnlyList<KritikStokItemDto> Urunler,
    int? Sayfa = null,
    int? SayfaBoyutu = null)
{
    public static KritikStokResponseDto From(KritikStokOutput output)
    {
        var items = output.Urunler
            .Select(KritikStokItemDto.From)
            .ToArray();

        return new KritikStokResponseDto(
            output.HesaplamaTarihi,
            output.ToplamKayitSayisi ?? items.Length,
            items,
            output.Sayfa,
            output.SayfaBoyutu);
    }
}

public sealed record KritikStokItemDto(
    string UrunKodu,
    string DepoKodu,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? OrtalamaGunlukSatis,
    decimal? TahminiStokGunu,
    IReadOnlyList<string> GerekceKodlari,
    string? Kategori = null,
    string? AltKategori = null)
{
    public static KritikStokItemDto From(KritikStokItemOutput output)
    {
        return new KritikStokItemDto(
            output.UrunKodu,
            output.DepoKodu,
            output.MevcutStok,
            output.MinStokSeviyesi,
            output.SonUcAySatisAdedi,
            output.OrtalamaGunlukSatis,
            output.TahminiStokGunu,
            output.GerekceKodlari,
            output.Kategori,
            output.AltKategori);
    }
}
