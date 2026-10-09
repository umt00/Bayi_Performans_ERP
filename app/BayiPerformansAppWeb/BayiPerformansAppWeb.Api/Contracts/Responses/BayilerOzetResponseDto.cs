using System.Globalization;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record BayilerOzetResponseDto(
    string Donem,
    int ToplamBayiSayisi,
    int AktifSatisYapanBayiSayisi,
    decimal ToplamGerceklesenCiro,
    decimal? ToplamHedefCiro,
    decimal? GenelHedefeUlasmaPct,
    decimal? GenelOrtalamaMarjPct,
    decimal? GenelOrtalamaIskontoPct,
    int YuksekRiskliBayiSayisi,
    int OrtaRiskliBayiSayisi,
    int DusukRiskliBayiSayisi,
    int KritikStokEtkilenenBayiSayisi,
    IReadOnlyDictionary<string, int> GerekceKoduDagilimi,
    decimal? OrtalamaHedefeUlasmaPct = null,
    decimal? OrtalamaMarjPct = null,
    decimal? OrtalamaIskontoPct = null,
    decimal? ToplamCiro = null)
{
    public static BayilerOzetResponseDto From(BayilerOzetOutput output)
    {
        return new BayilerOzetResponseDto(
            output.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            output.ToplamBayiSayisi,
            output.AktifSatisYapanBayiSayisi,
            output.ToplamGerceklesenCiro,
            output.ToplamHedefCiro,
            output.GenelHedefeUlasmaPct,
            output.GenelOrtalamaMarjPct,
            output.GenelOrtalamaIskontoPct,
            output.YuksekRiskliBayiSayisi,
            output.OrtaRiskliBayiSayisi,
            output.DusukRiskliBayiSayisi,
            output.KritikStokEtkilenenBayiSayisi,
            output.GerekceKoduDagilimi,
            OrtalamaHedefeUlasmaPct: output.GenelHedefeUlasmaPct,
            OrtalamaMarjPct: output.GenelOrtalamaMarjPct,
            OrtalamaIskontoPct: output.GenelOrtalamaIskontoPct,
            ToplamCiro: output.ToplamGerceklesenCiro);
    }
}
