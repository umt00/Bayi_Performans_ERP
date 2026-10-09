using System;
using System.Collections.Generic;

namespace BayiPerformansAppWeb.Entities.ServiceOutputs;

public sealed record BayiPerformansOutput(
    string BayiKodu,
    string Segment,
    string Bolge,
    DateOnly Donem,
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
    bool? LlmFallbackUsed = null);

public sealed record RiskListesiOutput(
    DateOnly Donem,
    IReadOnlyList<RiskListesiItemOutput> Bayiler,
    int? ToplamKayitSayisi = null,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record RiskListesiItemOutput(
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
    bool? KritikStokVarMi = null);

public sealed record KritikStokOutput(
    DateTimeOffset HesaplamaTarihi,
    IReadOnlyList<KritikStokItemOutput> Urunler,
    int? ToplamKayitSayisi = null,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record KritikStokItemOutput(
    string UrunKodu,
    string DepoKodu,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? OrtalamaGunlukSatis,
    decimal? TahminiStokGunu,
    IReadOnlyList<string> GerekceKodlari,
    string? Kategori = null,
    string? AltKategori = null);

public sealed record TeklifDegerlendirmeOutput(
    string Karar,
    IReadOnlyList<string> GerekceKodlari,
    decimal? HesaplananMarjPct,
    decimal MarjEsigiPct,
    decimal? MevcutStok,
    string? AiComment = null,
    string? CommentSource = null);

// --- Admin Paneli & Yönetici Analiz Modelleri ---

public sealed record BayilerOzetOutput(
    DateOnly Donem,
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
    IReadOnlyDictionary<string, int> GerekceKoduDagilimi);

public sealed record BayiGecmisPerformansOutput(
    string BayiKodu,
    string Segment,
    string Bolge,
    IReadOnlyList<BayiAylikPerformansItemOutput> AylikPerformanslar);

public sealed record BayiAylikPerformansItemOutput(
    DateOnly Donem,
    decimal GerceklesenCiro,
    decimal? HedefCiro,
    decimal? HedefeUlasmaPct,
    decimal? OrtalamaMarjPct,
    decimal? OrtalamaIskontoPct,
    decimal? IadeOraniPct,
    int RiskSkoru,
    string RiskSeviyesi,
    IReadOnlyList<string> GerekceKodlari);

public sealed record StokEnvanterOutput(
    DateTimeOffset HesaplamaTarihi,
    IReadOnlyList<StokEnvanterItemOutput> Stoklar,
    int ToplamKayitSayisi,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record StokEnvanterItemOutput(
    string UrunKodu,
    string DepoKodu,
    string? Kategori,
    string? AltKategori,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? TahminiStokGunu,
    bool KritikMi,
    IReadOnlyList<string> GerekceKodlari,
    string? UrunAdi = null);

public sealed record StokOzetOutput(
    DateTimeOffset HesaplamaTarihi,
    int ToplamStokKalemSayisi,
    decimal ToplamFizikselStokAdedi,
    int KritikSeviyedekiKalemSayisi,
    int TukenenKalemSayisi,
    IReadOnlyList<DepoStokOzetOutput> DepoBazliOzetler);

public sealed record DepoStokOzetOutput(
    string DepoKodu,
    string? Bolge,
    int ToplamKalemSayisi,
    int KritikKalemSayisi,
    decimal ToplamMevcutStok);

public sealed record FiltreSecenekleriOutput(
    IReadOnlyList<string> Bolgeler,
    IReadOnlyList<string> Segmentler,
    IReadOnlyList<DepoTanimOutput> Depolar,
    IReadOnlyList<KategoriTanimOutput> Kategoriler,
    IReadOnlyList<string> Donemler,
    IReadOnlyList<string> RiskSeviyeleri,
    IReadOnlyList<string> BayiRiskGerekceKodlari,
    IReadOnlyList<string> StokRiskGerekceKodlari,
    IReadOnlyList<SehirTanimOutput>? Sehirler = null);

public sealed record DepoTanimOutput(
    string DepoKodu,
    string? Bolge,
    string? DepoAdi = null,
    string? Sehir = null);

public sealed record KategoriTanimOutput(
    string Kategori,
    IReadOnlyList<string> AltKategoriler);

public sealed record SehirTanimOutput(
    string Sehir,
    string Bolge,
    int BayiSayisi);

// --- Şehir & Bölge Dağılım Çıktıları ---

public sealed record BayiSehirDagilimiOutput(
    DateOnly Donem,
    int ToplamSehirSayisi,
    int ToplamBayiSayisi,
    IReadOnlyList<BayiSehirItemOutput> Sehirler);

public sealed record BayiSehirItemOutput(
    string Sehir,
    string Bolge,
    int ToplamBayiSayisi,
    int AktifBayiSayisi,
    decimal ToplamCiro,
    int YuksekRiskliBayiSayisi,
    int OrtaRiskliBayiSayisi,
    int DusukRiskliBayiSayisi,
    IReadOnlyList<BayiSehirDetayItemOutput> Bayiler);

public sealed record BayiSehirDetayItemOutput(
    string BayiKodu,
    string? BayiAdi,
    string Segment,
    decimal GerceklesenCiro,
    int RiskSkoru,
    string RiskSeviyesi);

public sealed record BayiBolgeDagilimiOutput(
    DateOnly Donem,
    int ToplamBolgeSayisi,
    int ToplamBayiSayisi,
    IReadOnlyList<BayiBolgeItemOutput> Bolgeler);

public sealed record BayiBolgeItemOutput(
    string Bolge,
    IReadOnlyList<string> Sehirler,
    int ToplamBayiSayisi,
    int AktifBayiSayisi,
    decimal ToplamCiro,
    decimal? ToplamHedefCiro,
    decimal? GenelHedefeUlasmaPct,
    int YuksekRiskliBayiSayisi,
    int OrtaRiskliBayiSayisi,
    int DusukRiskliBayiSayisi);

// --- Ürün Depo Dağılım Matrisi Çıktıları ---

public sealed record UrunDepoStokMatrisiOutput(
    DateTimeOffset HesaplamaTarihi,
    int ToplamUrunSayisi,
    IReadOnlyList<UrunDepoMatrisItemOutput> Urunler,
    IReadOnlyList<DepoTanimOutput> Depolar,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record UrunDepoMatrisItemOutput(
    string UrunKodu,
    string? UrunAdi,
    string? Kategori,
    string? AltKategori,
    decimal ToplamMevcutStok,
    decimal ToplamSonUcAySatisAdedi,
    int KritikDepoSayisi,
    IReadOnlyList<UrunDepoDetayItemOutput> DepoStoklari);

public sealed record UrunDepoDetayItemOutput(
    string DepoKodu,
    string? DepoAdi,
    string? Sehir,
    string? Bolge,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? TahminiStokGunu,
    bool KritikMi);
