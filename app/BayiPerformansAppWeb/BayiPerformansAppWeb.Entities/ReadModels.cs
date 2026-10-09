namespace BayiPerformansAppWeb.Entities.ReadModels;

public sealed record BayiReadModel(
    string BayiKodu,
    string? Segment,
    string? Bolge,
    string? Sehir = null,
    string? BayiAdi = null);

public sealed record DepoReadModel(
    string DepoKodu,
    string? Bolge,
    string? DepoAdi = null,
    string? Sehir = null);

public sealed record SatisReadModel(
    string? SatisId,
    DateOnly Tarih,
    string BayiKodu,
    string UrunKodu,
    string DepoKodu,
    decimal? Miktar,
    decimal? BirimFiyat,
    decimal? IskontoOrani,
    string? SatisKanali,
    bool? IsIade)
{
    public SatisReadModel(
        long? SatisId,
        DateOnly Tarih,
        string BayiKodu,
        string UrunKodu,
        string DepoKodu,
        decimal? Miktar,
        decimal? BirimFiyat,
        decimal? IskontoOrani,
        string? SatisKanali,
        bool? IsIade)
        : this(
            SatisId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Tarih,
            BayiKodu,
            UrunKodu,
            DepoKodu,
            Miktar,
            BirimFiyat,
            IskontoOrani,
            SatisKanali,
            IsIade)
    {
    }
}

public sealed record UrunReadModel(
    string UrunKodu,
    string? Kategori,
    string? AltKategori,
    decimal? BirimMaliyet,
    string? UrunAdi = null,
    string? Marka = null);

public sealed record StokReadModel(
    string UrunKodu,
    string DepoKodu,
    decimal? MevcutStok,
    decimal? MinStokSeviyesi,
    DateOnly? SonSayimTarihi);

public sealed record AylikHedefReadModel(
    string BayiKodu,
    int Yil,
    int Ay,
    decimal? Hedef);

/// <summary>
/// Data/Automation ekibinin aylık olarak hazırladığı bayi performans
/// karnesinin salt-okunur uygulama modelidir. Yüzde alanları 0-100
/// sözleşmesini kullanır; API bu değerleri yeniden hesaplamaz.
/// </summary>
public sealed record BayiDonemPerformansReadModel(
    string BayiKodu,
    DateOnly Donem,
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
    IReadOnlyList<string> GerekceKodlari,
    string? Sehir = null);

public sealed record BayiDonemPerformansQuery(
    DateOnly? Donem,
    string? RiskSeviyesi,
    string? Bolge,
    string? Segment,
    int Sayfa,
    int SayfaBoyutu,
    string? Sehir = null);

public sealed record BayiDonemPerformansPage(
    DateOnly? Donem,
    int ToplamKayitSayisi,
    int Sayfa,
    int SayfaBoyutu,
    IReadOnlyList<BayiDonemPerformansReadModel> Bayiler);

public sealed record PerformansDashboardStatsReadModel(
    DateOnly? Donem,
    int ToplamBayiSayisi,
    decimal ToplamCiro,
    decimal ToplamHedef,
    int YuksekRiskliBayiSayisi,
    int KritikStokluBayiSayisi,
    decimal? OrtalamaHedefTutmaOrani,
    decimal? OrtalamaKarMarji,
    decimal? OrtalamaRiskSkoru);
