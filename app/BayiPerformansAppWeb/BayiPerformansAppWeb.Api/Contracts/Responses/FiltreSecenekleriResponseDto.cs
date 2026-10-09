using System.Collections.Generic;
using System.Linq;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record FiltreSecenekleriResponseDto(
    IReadOnlyList<string> Bolgeler,
    IReadOnlyList<string> Segmentler,
    IReadOnlyList<DepoTanimDto> Depolar,
    IReadOnlyList<KategoriTanimDto> Kategoriler,
    IReadOnlyList<string> Donemler,
    IReadOnlyList<string> RiskSeviyeleri,
    IReadOnlyList<string> BayiRiskGerekceKodlari,
    IReadOnlyList<string> StokRiskGerekceKodlari,
    IReadOnlyList<SehirTanimDto>? Sehirler = null)
{
    public static FiltreSecenekleriResponseDto From(FiltreSecenekleriOutput output)
    {
        return new FiltreSecenekleriResponseDto(
            output.Bolgeler,
            output.Segmentler,
            output.Depolar.Select(DepoTanimDto.From).ToArray(),
            output.Kategoriler.Select(KategoriTanimDto.From).ToArray(),
            output.Donemler,
            output.RiskSeviyeleri,
            output.BayiRiskGerekceKodlari,
            output.StokRiskGerekceKodlari,
            output.Sehirler?.Select(SehirTanimDto.From).ToArray());
    }
}

public sealed record DepoTanimDto(
    string DepoKodu,
    string? Bolge,
    string? DepoAdi = null,
    string? Sehir = null)
{
    public static DepoTanimDto From(DepoTanimOutput output)
    {
        return new DepoTanimDto(output.DepoKodu, output.Bolge, output.DepoAdi, output.Sehir);
    }
}

public sealed record KategoriTanimDto(
    string Kategori,
    IReadOnlyList<string> AltKategoriler)
{
    public static KategoriTanimDto From(KategoriTanimOutput output)
    {
        return new KategoriTanimDto(output.Kategori, output.AltKategoriler);
    }
}

public sealed record SehirTanimDto(
    string Sehir,
    string Bolge,
    int BayiSayisi)
{
    public static SehirTanimDto From(SehirTanimOutput output)
    {
        return new SehirTanimDto(output.Sehir, output.Bolge, output.BayiSayisi);
    }
}
