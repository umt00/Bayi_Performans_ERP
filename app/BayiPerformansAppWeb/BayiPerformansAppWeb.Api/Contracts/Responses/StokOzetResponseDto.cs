using System;
using System.Collections.Generic;
using System.Linq;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record StokOzetResponseDto(
    DateTimeOffset HesaplamaTarihi,
    int ToplamStokKalemSayisi,
    decimal ToplamFizikselStokAdedi,
    int KritikSeviyedekiKalemSayisi,
    int TukenenKalemSayisi,
    IReadOnlyList<DepoStokOzetDto> DepoBazliOzetler)
{
    public static StokOzetResponseDto From(StokOzetOutput output)
    {
        return new StokOzetResponseDto(
            output.HesaplamaTarihi,
            output.ToplamStokKalemSayisi,
            output.ToplamFizikselStokAdedi,
            output.KritikSeviyedekiKalemSayisi,
            output.TukenenKalemSayisi,
            output.DepoBazliOzetler.Select(DepoStokOzetDto.From).ToArray());
    }
}

public sealed record DepoStokOzetDto(
    string DepoKodu,
    string? Bolge,
    int ToplamKalemSayisi,
    int KritikKalemSayisi,
    decimal ToplamMevcutStok)
{
    public static DepoStokOzetDto From(DepoStokOzetOutput output)
    {
        return new DepoStokOzetDto(
            output.DepoKodu,
            output.Bolge,
            output.ToplamKalemSayisi,
            output.KritikKalemSayisi,
            output.ToplamMevcutStok);
    }
}
