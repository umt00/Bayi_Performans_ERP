namespace BayiPerformansAppWeb.DataAccess.Persistence;

internal sealed class BayiRow
{
    public string? BayiKodu { get; init; }
    public string? BayiAdi { get; init; }
    public string? Sehir { get; init; }
    public string? Bolge { get; init; }
    public string? Segment { get; init; }
}

internal sealed class DepoRow
{
    public string? DepoKodu { get; init; }
    public string? DepoAdi { get; init; }
    public string? Sehir { get; init; }
    public string? Bolge { get; init; }
}

internal sealed class HedefRow
{
    public string? BayiKodu { get; init; }
    public int? Yil { get; init; }
    public int? Ay { get; init; }
    public decimal? Hedef { get; init; }
}

internal sealed class SatisRow
{
    public string? SatisId { get; init; }
    public DateOnly? Tarih { get; init; }
    public string? BayiKodu { get; init; }
    public string? UrunKodu { get; init; }
    public string? DepoKodu { get; init; }
    public decimal? Miktar { get; init; }
    public decimal? BirimFiyat { get; init; }
    public decimal? IskontoOrani { get; init; }
    public string? SatisKanali { get; init; }
    public bool? IsIade { get; init; }
}

internal sealed class StokRow
{
    public string? UrunKodu { get; init; }
    public string? DepoKodu { get; init; }
    public decimal? MevcutStok { get; init; }
    public decimal? MinStokSeviyesi { get; init; }
    public DateOnly? SonSayimTarihi { get; init; }
}

internal sealed class UrunRow
{
    public string? UrunKodu { get; init; }
    public string? UrunAdi { get; init; }
    public string? Kategori { get; init; }
    public string? AltKategori { get; init; }
    public string? Marka { get; init; }
    public decimal? BirimMaliyet { get; init; }
}

internal sealed class TeklifRow
{
    public string TeklifId { get; set; } = string.Empty;
    public string BayiKodu { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public string Durum { get; set; } = string.Empty;
    public decimal ToplamNetTutar { get; set; }
    public DateTimeOffset OlusturulmaTarihi { get; set; }
    public ICollection<TeklifKalemRow> Kalemler { get; } = new List<TeklifKalemRow>();
    public TeklifDegerlendirmeRow? Degerlendirme { get; set; }
    public TeklifAiYorumRow? AiYorum { get; set; }
}

internal sealed class TeklifKalemRow
{
    public string TeklifId { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public string UrunKodu { get; set; } = string.Empty;
    public string DepoKodu { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal IskontoOrani { get; set; }
    public decimal NetTutar { get; set; }
    public TeklifRow Teklif { get; set; } = null!;
}

internal sealed class TeklifDegerlendirmeRow
{
    public string TeklifId { get; set; } = string.Empty;
    public string BayiKodu { get; set; } = string.Empty;
    public string Karar { get; set; } = string.Empty;
    public int? RiskSkoru { get; set; }
    public string? RiskSeviyesi { get; set; }
    public string[] GerekceKodlari { get; set; } = [];
    public decimal? BayiMarji { get; set; }
    public decimal? HedefTutturmaYuzdesi { get; set; }
    public decimal? OrtalamaIskontoOrani { get; set; }
    public decimal? IadeOrani { get; set; }
    public DateTimeOffset DegerlendirmeTarihi { get; set; }
    public TeklifRow Teklif { get; set; } = null!;
}

internal sealed class TeklifAiYorumRow
{
    public string TeklifId { get; set; } = string.Empty;
    public string? AiComment { get; set; }
    public string? AlertMessage { get; set; }
    public bool IsFallback { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public TeklifRow Teklif { get; set; } = null!;
}

internal sealed class BayiDonemPerformansRow
{
    public string BayiKodu { get; init; } = string.Empty;
    public DateOnly Donem { get; init; }
    public decimal? Ciro { get; init; }
    public decimal? Hedef { get; init; }
    public decimal? HedefTutmaOrani { get; init; }
    public decimal? OrtalamaKarMarji { get; init; }
    public decimal? OrtalamaIskontoOrani { get; init; }
    public decimal? OrtalamaIadeOrani { get; init; }
    public int? KritikStokKalanUrunSayisi { get; init; }
    public int? RiskSkoru { get; init; }
    public string? RiskSeviyesi { get; init; }
    public string[] GerekceKodlari { get; init; } = [];
}
