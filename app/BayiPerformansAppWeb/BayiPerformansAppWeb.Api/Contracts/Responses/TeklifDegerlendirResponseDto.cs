using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record TeklifDegerlendirResponseDto(
    string Karar,
    IReadOnlyList<string> GerekceKodlari,
    decimal? HesaplananMarjPct,
    decimal MarjEsigiPct,
    decimal? MevcutStok,
    string? AiComment = null,
    string? CommentSource = null)
{
    public static TeklifDegerlendirResponseDto From(TeklifDegerlendirmeOutput output)
    {
        return new TeklifDegerlendirResponseDto(
            output.Karar,
            output.GerekceKodlari,
            output.HesaplananMarjPct,
            output.MarjEsigiPct,
            output.MevcutStok,
            output.AiComment,
            output.CommentSource);
    }
}
