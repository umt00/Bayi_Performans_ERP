using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record BayiSehirDagilimiResponseDto(
    string Donem,
    int ToplamSehirSayisi,
    int ToplamBayiSayisi,
    IReadOnlyList<BayiSehirItemDto> Sehirler)
{
    public static BayiSehirDagilimiResponseDto From(BayiSehirDagilimiOutput output)
    {
        return new BayiSehirDagilimiResponseDto(
            output.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            output.ToplamSehirSayisi,
            output.ToplamBayiSayisi,
            output.Sehirler.Select(BayiSehirItemDto.From).ToArray());
    }
}

public sealed record BayiSehirItemDto(
    string Sehir,
    string Bolge,
    int ToplamBayiSayisi,
    int AktifBayiSayisi,
    decimal ToplamCiro,
    int YuksekRiskliBayiSayisi,
    int OrtaRiskliBayiSayisi,
    int DusukRiskliBayiSayisi,
    IReadOnlyList<BayiSehirDetayItemDto> Bayiler)
{
    public static BayiSehirItemDto From(BayiSehirItemOutput output)
    {
        return new BayiSehirItemDto(
            output.Sehir,
            output.Bolge,
            output.ToplamBayiSayisi,
            output.AktifBayiSayisi,
            output.ToplamCiro,
            output.YuksekRiskliBayiSayisi,
            output.OrtaRiskliBayiSayisi,
            output.DusukRiskliBayiSayisi,
            output.Bayiler.Select(BayiSehirDetayItemDto.From).ToArray());
    }
}

public sealed record BayiSehirDetayItemDto(
    string BayiKodu,
    string? BayiAdi,
    string Segment,
    decimal GerceklesenCiro,
    int RiskSkoru,
    string RiskSeviyesi)
{
    public static BayiSehirDetayItemDto From(BayiSehirDetayItemOutput output)
    {
        return new BayiSehirDetayItemDto(
            output.BayiKodu,
            output.BayiAdi,
            output.Segment,
            output.GerceklesenCiro,
            output.RiskSkoru,
            output.RiskSeviyesi);
    }
}

public sealed record BayiBolgeDagilimiResponseDto(
    string Donem,
    int ToplamBolgeSayisi,
    int ToplamBayiSayisi,
    IReadOnlyList<BayiBolgeItemDto> Bolgeler)
{
    public static BayiBolgeDagilimiResponseDto From(BayiBolgeDagilimiOutput output)
    {
        return new BayiBolgeDagilimiResponseDto(
            output.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            output.ToplamBolgeSayisi,
            output.ToplamBayiSayisi,
            output.Bolgeler.Select(BayiBolgeItemDto.From).ToArray());
    }
}

public sealed record BayiBolgeItemDto(
    string Bolge,
    IReadOnlyList<string> Sehirler,
    int ToplamBayiSayisi,
    int AktifBayiSayisi,
    decimal ToplamCiro,
    decimal? ToplamHedefCiro,
    decimal? GenelHedefeUlasmaPct,
    int YuksekRiskliBayiSayisi,
    int OrtaRiskliBayiSayisi,
    int DusukRiskliBayiSayisi)
{
    public static BayiBolgeItemDto From(BayiBolgeItemOutput output)
    {
        return new BayiBolgeItemDto(
            output.Bolge,
            output.Sehirler,
            output.ToplamBayiSayisi,
            output.AktifBayiSayisi,
            output.ToplamCiro,
            output.ToplamHedefCiro,
            output.GenelHedefeUlasmaPct,
            output.YuksekRiskliBayiSayisi,
            output.OrtaRiskliBayiSayisi,
            output.DusukRiskliBayiSayisi);
    }
}
