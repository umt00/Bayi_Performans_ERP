using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record RiskListesiResponseDto(
    string Donem,
    int Toplam,
    IReadOnlyList<RiskListesiItemDto> Bayiler,
    int? Sayfa = null,
    int? SayfaBoyutu = null)
{
    public static RiskListesiResponseDto From(RiskListesiOutput output)
    {
        var items = output.Bayiler
            .Select(RiskListesiItemDto.From)
            .ToArray();

        return new RiskListesiResponseDto(
            output.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            output.ToplamKayitSayisi ?? items.Length,
            items,
            output.Sayfa,
            output.SayfaBoyutu);
    }
}

public sealed record RiskListesiItemDto(
    string BayiKodu,
    string Segment,
    string Bolge,
    decimal? HedefeUlasmaPct,
    decimal? OrtalamaMarjPct,
    decimal? OrtalamaIskontoPct,
    int RiskSkoru,
    string RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari,
    string? Sehir = null,
    string? BayiAdi = null,
    decimal? GerceklesenCiro = null,
    decimal? HedefCiro = null,
    bool? KritikStokVarMi = null)
{
    public static RiskListesiItemDto From(RiskListesiItemOutput output)
    {
        return new RiskListesiItemDto(
            output.BayiKodu,
            output.Segment,
            output.Bolge,
            output.HedefeUlasmaPct,
            output.OrtalamaMarjPct,
            output.OrtalamaIskontoPct,
            output.RiskSkoru,
            output.RiskSeviyesi,
            output.GerekceKodlari,
            output.Sehir,
            output.BayiAdi,
            output.GerceklesenCiro,
            output.HedefCiro,
            output.KritikStokVarMi);
    }
}
