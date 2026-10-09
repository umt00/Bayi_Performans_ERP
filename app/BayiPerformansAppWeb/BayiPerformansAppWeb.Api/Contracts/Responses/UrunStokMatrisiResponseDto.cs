using System;
using System.Collections.Generic;
using System.Linq;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record UrunStokMatrisiResponseDto(
    DateTimeOffset HesaplamaTarihi,
    int Toplam,
    IReadOnlyList<UrunDepoMatrisItemDto> Urunler,
    IReadOnlyList<DepoTanimDto> Depolar,
    int? Sayfa = null,
    int? SayfaBoyutu = null)
{
    public static UrunStokMatrisiResponseDto From(UrunDepoStokMatrisiOutput output)
    {
        return new UrunStokMatrisiResponseDto(
            output.HesaplamaTarihi,
            output.ToplamUrunSayisi,
            output.Urunler.Select(UrunDepoMatrisItemDto.From).ToArray(),
            output.Depolar.Select(DepoTanimDto.From).ToArray(),
            output.Sayfa,
            output.SayfaBoyutu);
    }
}

public sealed record UrunDepoMatrisItemDto(
    string UrunKodu,
    string? UrunAdi,
    string? Kategori,
    string? AltKategori,
    decimal ToplamMevcutStok,
    decimal ToplamSonUcAySatisAdedi,
    int KritikDepoSayisi,
    IReadOnlyList<UrunDepoDetayItemDto> DepoStoklari)
{
    public static UrunDepoMatrisItemDto From(UrunDepoMatrisItemOutput output)
    {
        return new UrunDepoMatrisItemDto(
            output.UrunKodu,
            output.UrunAdi,
            output.Kategori,
            output.AltKategori,
            output.ToplamMevcutStok,
            output.ToplamSonUcAySatisAdedi,
            output.KritikDepoSayisi,
            output.DepoStoklari.Select(UrunDepoDetayItemDto.From).ToArray());
    }
}

public sealed record UrunDepoDetayItemDto(
    string DepoKodu,
    string? DepoAdi,
    string? Sehir,
    string? Bolge,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? TahminiStokGunu,
    bool KritikMi)
{
    public static UrunDepoDetayItemDto From(UrunDepoDetayItemOutput output)
    {
        return new UrunDepoDetayItemDto(
            output.DepoKodu,
            output.DepoAdi,
            output.Sehir,
            output.Bolge,
            output.MevcutStok,
            output.MinStokSeviyesi,
            output.SonUcAySatisAdedi,
            output.TahminiStokGunu,
            output.KritikMi);
    }
}
