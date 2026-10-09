using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using BayiPerformansAppWeb.Entities;

namespace BayiPerformansAppWeb.Api.Contracts;

public sealed record YeniTeklifKalemRequestDto(
    [Required(ErrorMessage = "Ürün kodu zorunludur.")]
    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [Required(ErrorMessage = "Depo kodu zorunludur.")]
    [property: JsonPropertyName("depo_kodu")]
    string DepoKodu,

    [Required(ErrorMessage = "Miktar zorunludur.")]
    [Range(0.0001, 1000000000, ErrorMessage = "Miktar 0'dan büyük olmalıdır.")]
    [property: JsonPropertyName("miktar")]
    decimal? Miktar,

    [Required(ErrorMessage = "Birim fiyat zorunludur.")]
    [Range(0.0001, 1000000000, ErrorMessage = "Birim fiyat 0'dan büyük olmalıdır.")]
    [property: JsonPropertyName("birim_fiyat")]
    decimal? BirimFiyat,

    [Required(ErrorMessage = "İskonto oranı zorunludur.")]
    [Range(0, 1, ErrorMessage = "İskonto oranı 0 ile 1 arasında olmalıdır.")]
    [property: JsonPropertyName("iskonto_orani")]
    decimal? IskontoOrani);

public sealed record YeniTeklifRequestDto(
    [Required(ErrorMessage = "Bayi kodu zorunludur.")]
    [property: JsonPropertyName("bayi_kodu")]
    string BayiKodu,

    [property: JsonPropertyName("aciklama")]
    string? Aciklama,

    [Required(ErrorMessage = "Teklif en az bir ürün kalemi içermelidir.")]
    [MinLength(1, ErrorMessage = "Teklif en az bir ürün kalemi içermelidir.")]
    [property: JsonPropertyName("kalemler")]
    IReadOnlyList<YeniTeklifKalemRequestDto> Kalemler);

public sealed record TeklifKalemResponseDto(
    [property: JsonPropertyName("sira_no")]
    int SiraNo,

    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [property: JsonPropertyName("urun_adi")]
    string? UrunAdi,

    [property: JsonPropertyName("depo_kodu")]
    string DepoKodu,

    [property: JsonPropertyName("depo_adi")]
    string? DepoAdi,

    [property: JsonPropertyName("miktar")]
    decimal Miktar,

    [property: JsonPropertyName("birim_fiyat")]
    decimal BirimFiyat,

    [property: JsonPropertyName("iskonto_orani")]
    decimal IskontoOrani,

    [property: JsonPropertyName("net_tutar")]
    decimal NetTutar);

public sealed record TeklifResponseDto(
    [property: JsonPropertyName("teklif_id")]
    string TeklifId,

    [property: JsonPropertyName("bayi_kodu")]
    string BayiKodu,

    [property: JsonPropertyName("bayi_adi")]
    string? BayiAdi,

    [property: JsonPropertyName("aciklama")]
    string? Aciklama,

    [property: JsonPropertyName("durum")]
    string Durum,

    [property: JsonPropertyName("toplam_net_tutar")]
    decimal ToplamNetTutar,

    [property: JsonPropertyName("olusturulma_tarihi")]
    DateTimeOffset OlusturulmaTarihiUtc,

    [property: JsonPropertyName("kalemler")]
    IReadOnlyList<TeklifKalemResponseDto> Kalemler,

    [property: JsonPropertyName("degerlendirme_sonucu")]
    TeklifCokluDegerlendirmeResponseDto? DegerlendirmeSonucu = null,

    [property: JsonPropertyName("bayi_sehir")]
    string? BayiSehir = null,

    [property: JsonPropertyName("bayi_bolge")]
    string? BayiBolge = null,

    [property: JsonPropertyName("karar")]
    string? Karar = null,

    [property: JsonPropertyName("risk_skoru")]
    int? RiskSkoru = null,

    [property: JsonPropertyName("risk_seviyesi")]
    string? RiskSeviyesi = null,

    [property: JsonPropertyName("gerekce_kodlari")]
    IReadOnlyList<string>? GerekceKodlari = null,

    [property: JsonPropertyName("bayi_marji")]
    decimal? BayiMarji = null,

    [property: JsonPropertyName("hedef_tutturma_yuzdesi")]
    decimal? HedefTutturmaYuzdesi = null,

    [property: JsonPropertyName("ortalama_iskonto_orani")]
    decimal? OrtalamaIskontoOrani = null,

    [property: JsonPropertyName("iade_orani")]
    decimal? IadeOrani = null,

    [property: JsonPropertyName("degerlendirme_tarihi")]
    DateTimeOffset? DegerlendirmeTarihi = null,

    [property: JsonPropertyName("ai_comment")]
    string? AiComment = null,

    [property: JsonPropertyName("alert_message")]
    string? AlertMessage = null,

    [property: JsonPropertyName("is_fallback")]
    bool? IsFallback = null,

    [property: JsonPropertyName("ai_created_time")]
    DateTimeOffset? AiCreatedTime = null,

    [property: JsonPropertyName("kalem_sayisi")]
    int KalemSayisi = 0,

    [property: JsonPropertyName("sevk_depolari")]
    string? SevkDepolari = null)
{
    public static TeklifResponseDto From(TeklifModel model)
    {
        var audit = model.KayitliDegerlendirme;
        return new(
            TeklifId: model.TeklifId,
            BayiKodu: model.BayiKodu,
            BayiAdi: model.BayiAdi,
            Aciklama: model.Aciklama,
            Durum: model.Durum,
            ToplamNetTutar: model.ToplamNetTutar,
            OlusturulmaTarihiUtc: model.OlusturulmaTarihiUtc,
            Kalemler: model.Kalemler.Select(k => new TeklifKalemResponseDto(
                SiraNo: k.SiraNo,
                UrunKodu: k.UrunKodu,
                UrunAdi: k.UrunAdi,
                DepoKodu: k.DepoKodu,
                DepoAdi: k.DepoAdi,
                Miktar: k.Miktar,
                BirimFiyat: k.BirimFiyat,
                IskontoOrani: k.IskontoOrani,
                NetTutar: k.NetTutar)).ToList(),
            DegerlendirmeSonucu: model.DegerlendirmeSonucu is null
                ? null
                : TeklifCokluDegerlendirmeResponseDto.From(model.DegerlendirmeSonucu),
            BayiSehir: model.BayiSehir,
            BayiBolge: model.BayiBolge,
            Karar: audit?.Karar ?? model.DegerlendirmeSonucu?.GenelKarar,
            RiskSkoru: audit?.RiskSkoru,
            RiskSeviyesi: audit?.RiskSeviyesi,
            GerekceKodlari: audit?.GerekceKodlari
                ?? model.DegerlendirmeSonucu?.GenelGerekceKodlari,
            BayiMarji: audit?.BayiMarji,
            HedefTutturmaYuzdesi: audit?.HedefTutturmaYuzdesi,
            OrtalamaIskontoOrani: audit?.OrtalamaIskontoOrani,
            IadeOrani: audit?.IadeOrani,
            DegerlendirmeTarihi: audit?.DegerlendirmeTarihi,
            AiComment: model.AiYorum?.AiComment,
            AlertMessage: model.AiYorum?.AlertMessage,
            IsFallback: model.AiYorum?.IsFallback,
            AiCreatedTime: model.AiYorum?.CreatedTime,
            KalemSayisi: model.Kalemler.Count,
            SevkDepolari: string.Join(", ", model.Kalemler
                .Select(item => item.DepoKodu)
                .Distinct(StringComparer.OrdinalIgnoreCase)));
    }
}

public sealed record TeklifKalemDegerlendirmeResponseDto(
    [property: JsonPropertyName("sira_no")]
    int SiraNo,

    [property: JsonPropertyName("urun_kodu")]
    string UrunKodu,

    [property: JsonPropertyName("urun_adi")]
    string? UrunAdi,

    [property: JsonPropertyName("depo_kodu")]
    string DepoKodu,

    [property: JsonPropertyName("miktar")]
    decimal Miktar,

    [property: JsonPropertyName("birim_fiyat")]
    decimal BirimFiyat,

    [property: JsonPropertyName("iskonto_orani")]
    decimal IskontoOrani,

    [property: JsonPropertyName("birim_maliyet")]
    decimal BirimMaliyet,

    [property: JsonPropertyName("net_tutar")]
    decimal NetTutar,

    [property: JsonPropertyName("marj_orani")]
    decimal MarjPct,

    [property: JsonPropertyName("mevcut_stok")]
    decimal MevcutStok,

    [property: JsonPropertyName("stok_yeterli_mi")]
    bool StokYeterliMi,

    [property: JsonPropertyName("karar")]
    string Karar,

    [property: JsonPropertyName("gerekce_kodlari")]
    IReadOnlyList<string> GerekceKodlari);

public sealed record TeklifCokluDegerlendirmeResponseDto(
    [property: JsonPropertyName("teklif_id")]
    string TeklifId,

    [property: JsonPropertyName("bayi_kodu")]
    string BayiKodu,

    [property: JsonPropertyName("genel_karar")]
    string GenelKarar,

    [property: JsonPropertyName("genel_gerekce_kodlari")]
    IReadOnlyList<string> GenelGerekceKodlari,

    [property: JsonPropertyName("toplam_net_tutar")]
    decimal ToplamNetTutar,

    [property: JsonPropertyName("toplam_maliyet")]
    decimal ToplamMaliyet,

    [property: JsonPropertyName("genel_marj_orani")]
    decimal GenelMarjPct,

    [property: JsonPropertyName("degerlendirilme_tarihi")]
    DateTimeOffset DegerlendirilmeTarihiUtc,

    [property: JsonPropertyName("kalem_sonuclari")]
    IReadOnlyList<TeklifKalemDegerlendirmeResponseDto> KalemSonuclari,

    [property: JsonPropertyName("risk_skoru")]
    int? RiskSkoru = null,

    [property: JsonPropertyName("risk_seviyesi")]
    string? RiskSeviyesi = null,

    [property: JsonPropertyName("hedef_tutturma_yuzdesi")]
    decimal? HedefTutturmaYuzdesi = null,

    [property: JsonPropertyName("ortalama_iskonto_orani")]
    decimal? OrtalamaIskontoOrani = null,

    [property: JsonPropertyName("iade_orani")]
    decimal? IadeOrani = null,

    [property: JsonPropertyName("ai_comment")]
    string? AiComment = null,

    [property: JsonPropertyName("alert_message")]
    string? AlertMessage = null,

    [property: JsonPropertyName("is_fallback")]
    bool? IsFallback = null)
{
    public static TeklifCokluDegerlendirmeResponseDto From(TeklifCokluDegerlendirmeOutput output) =>
        new(
            TeklifId: output.TeklifId,
            BayiKodu: output.BayiKodu,
            GenelKarar: output.GenelKarar,
            GenelGerekceKodlari: output.GenelGerekceKodlari,
            ToplamNetTutar: output.ToplamNetTutar,
            ToplamMaliyet: output.ToplamMaliyet,
            GenelMarjPct: output.GenelMarjPct,
            DegerlendirilmeTarihiUtc: output.DegerlendirilmeTarihiUtc,
            KalemSonuclari: output.KalemSonuclari.Select(k => new TeklifKalemDegerlendirmeResponseDto(
                SiraNo: k.SiraNo,
                UrunKodu: k.UrunKodu,
                UrunAdi: k.UrunAdi,
                DepoKodu: k.DepoKodu,
                Miktar: k.Miktar,
                BirimFiyat: k.BirimFiyat,
                IskontoOrani: k.IskontoOrani,
                BirimMaliyet: k.BirimMaliyet,
                NetTutar: k.NetTutar,
                MarjPct: k.MarjPct,
                MevcutStok: k.MevcutStok,
                StokYeterliMi: k.StokYeterliMi,
                Karar: k.Karar,
                GerekceKodlari: k.GerekceKodlari)).ToList(),
            RiskSkoru: output.RiskSkoru,
            RiskSeviyesi: output.RiskSeviyesi,
            HedefTutturmaYuzdesi: output.HedefTutturmaYuzdesi,
            OrtalamaIskontoOrani: output.OrtalamaIskontoOrani,
            IadeOrani: output.IadeOrani,
            AiComment: output.AiComment,
            AlertMessage: output.AlertMessage,
            IsFallback: output.IsFallback);
}
