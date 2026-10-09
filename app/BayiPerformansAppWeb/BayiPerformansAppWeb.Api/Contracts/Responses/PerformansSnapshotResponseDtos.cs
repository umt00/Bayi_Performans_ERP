using System.Globalization;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Api.Contracts.Responses;

/// <summary>
/// bayi_donem_performans tablosunun kesinleşmiş aylık karne sözleşmesi.
/// Alanlar Data/Automation tarafından hesaplandığı biçimiyle döndürülür.
/// </summary>
public sealed record PerformansBayiResponseDto(
    string BayiKodu,
    string Donem,
    string? BayiAdi,
    string? Segment,
    string? Bolge,
    decimal? Ciro,
    decimal? Hedef,
    decimal? HedefTutmaOrani,
    decimal? OrtalamaKarMarji,
    decimal? OrtalamaIskontoOrani,
    decimal? OrtalamaIadeOrani,
    int? KritikStokKalanUrunSayisi,
    int? RiskSkoru,
    string? RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari)
{
    public static PerformansBayiResponseDto From(
        BayiDonemPerformansReadModel model)
    {
        return new PerformansBayiResponseDto(
            model.BayiKodu,
            model.Donem.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            model.BayiAdi,
            model.Segment,
            model.Bolge,
            model.Ciro,
            model.Hedef,
            model.HedefTutmaOrani,
            model.OrtalamaKarMarji,
            model.OrtalamaIskontoOrani,
            model.OrtalamaIadeOrani,
            model.KritikStokKalanUrunSayisi,
            model.RiskSkoru,
            model.RiskSeviyesi,
            model.GerekceKodlari);
    }
}

public sealed record PerformansBayiListesiResponseDto(
    string? Donem,
    int ToplamKayitSayisi,
    int Sayfa,
    int SayfaBoyutu,
    IReadOnlyList<PerformansBayiResponseDto> Bayiler)
{
    public static PerformansBayiListesiResponseDto From(
        BayiDonemPerformansPage page)
    {
        return new PerformansBayiListesiResponseDto(
            page.Donem?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            page.ToplamKayitSayisi,
            page.Sayfa,
            page.SayfaBoyutu,
            page.Bayiler.Select(PerformansBayiResponseDto.From).ToArray());
    }
}

public sealed record PerformansDashboardStatsResponseDto(
    string? Donem,
    int ToplamBayiSayisi,
    decimal ToplamCiro,
    decimal ToplamHedef,
    int YuksekRiskliBayiSayisi,
    int KritikStokluBayiSayisi,
    decimal? OrtalamaHedefTutmaOrani,
    decimal? OrtalamaKarMarji,
    decimal? OrtalamaRiskSkoru)
{
    public static PerformansDashboardStatsResponseDto From(
        PerformansDashboardStatsReadModel model)
    {
        return new PerformansDashboardStatsResponseDto(
            model.Donem?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            model.ToplamBayiSayisi,
            model.ToplamCiro,
            model.ToplamHedef,
            model.YuksekRiskliBayiSayisi,
            model.KritikStokluBayiSayisi,
            model.OrtalamaHedefTutmaOrani,
            model.OrtalamaKarMarji,
            model.OrtalamaRiskSkoru);
    }
}
