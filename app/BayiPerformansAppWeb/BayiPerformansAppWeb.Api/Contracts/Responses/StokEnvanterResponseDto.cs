using System;
using System.Collections.Generic;
using System.Linq;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record StokEnvanterResponseDto(
    DateTimeOffset HesaplamaTarihi,
    int Toplam,
    IReadOnlyList<StokEnvanterItemDto> Stoklar,
    int? Sayfa = null,
    int? SayfaBoyutu = null)
{
    public static StokEnvanterResponseDto From(StokEnvanterOutput output)
    {
        var items = output.Stoklar
            .Select(StokEnvanterItemDto.From)
            .ToArray();

        return new StokEnvanterResponseDto(
            output.HesaplamaTarihi,
            output.ToplamKayitSayisi,
            items,
            output.Sayfa,
            output.SayfaBoyutu);
    }
}

public sealed record StokEnvanterItemDto(
    string UrunKodu,
    string DepoKodu,
    string? Kategori,
    string? AltKategori,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? TahminiStokGunu,
    bool KritikMi,
    IReadOnlyList<string> GerekceKodlari,
    string? UrunAdi = null)
{
    public static StokEnvanterItemDto From(StokEnvanterItemOutput output)
    {
        return new StokEnvanterItemDto(
            output.UrunKodu,
            output.DepoKodu,
            output.Kategori,
            output.AltKategori,
            output.MevcutStok,
            output.MinStokSeviyesi,
            output.SonUcAySatisAdedi,
            output.TahminiStokGunu,
            output.KritikMi,
            output.GerekceKodlari,
            output.UrunAdi);
    }
}
