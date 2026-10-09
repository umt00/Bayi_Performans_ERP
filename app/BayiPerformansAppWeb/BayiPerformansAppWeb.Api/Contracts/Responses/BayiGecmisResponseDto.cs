using System.Globalization;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record BayiGecmisResponseDto(
    string BayiKodu,
    string Segment,
    string Bolge,
    IReadOnlyList<BayiAylikPerformansItemDto> AylikPerformanslar)
{
    public static BayiGecmisResponseDto From(BayiGecmisPerformansOutput output)
    {
        return new BayiGecmisResponseDto(
            output.BayiKodu,
            output.Segment,
            output.Bolge,
            output.AylikPerformanslar.Select(BayiAylikPerformansItemDto.From).ToArray());
    }
}

public sealed record BayiAylikPerformansItemDto(
    string Donem,
    decimal GerceklesenCiro,
    decimal? HedefCiro,
    decimal? HedefeUlasmaPct,
    decimal? OrtalamaMarjPct,
    decimal? OrtalamaIskontoPct,
    decimal? IadeOraniPct,
    int RiskSkoru,
    string RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari)
{
    public static BayiAylikPerformansItemDto From(BayiAylikPerformansItemOutput output)
    {
        return new BayiAylikPerformansItemDto(
            output.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            output.GerceklesenCiro,
            output.HedefCiro,
            output.HedefeUlasmaPct,
            output.OrtalamaMarjPct,
            output.OrtalamaIskontoPct,
            output.IadeOraniPct,
            output.RiskSkoru,
            output.RiskSeviyesi,
            output.GerekceKodlari);
    }
}
