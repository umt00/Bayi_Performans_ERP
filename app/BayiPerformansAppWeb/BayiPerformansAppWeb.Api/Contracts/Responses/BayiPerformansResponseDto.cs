using System.Globalization;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record BayiPerformansResponseDto(
    string BayiKodu,
    string Segment,
    string Bolge,
    string Donem,
    decimal GerceklesenCiro,
    decimal? HedefCiro,
    decimal? HedefeUlasmaPct,
    decimal? OrtalamaMarjPct,
    decimal MarjEsigiPct,
    decimal? OrtalamaIskontoPct,
    decimal? IadeOraniPct,
    int KritikStokUrunSayisi,
    int RiskSkoru,
    string RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari,
    string? AiComment = null,
    string? AlertMessageDraft = null,
    string? CommentSource = null,
    bool? LlmFallbackUsed = null)
{
    public static BayiPerformansResponseDto From(BayiPerformansOutput output)
    {
        return new BayiPerformansResponseDto(
            output.BayiKodu,
            output.Segment,
            output.Bolge,
            output.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            output.GerceklesenCiro,
            output.HedefCiro,
            output.HedefeUlasmaPct,
            output.OrtalamaMarjPct,
            output.MarjEsigiPct,
            output.OrtalamaIskontoPct,
            output.IadeOraniPct,
            output.KritikStokUrunSayisi,
            output.RiskSkoru,
            output.RiskSeviyesi,
            output.GerekceKodlari,
            output.AiComment,
            output.AlertMessageDraft,
            output.CommentSource,
            output.LlmFallbackUsed);
    }
}
