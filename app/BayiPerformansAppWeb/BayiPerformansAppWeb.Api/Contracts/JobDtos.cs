using System;
using System.Text.Json.Serialization;
using BayiPerformansAppWeb.Entities;

namespace BayiPerformansAppWeb.Api.Contracts;

public sealed record JobDurumResponseDto(
    [property: JsonPropertyName("islem_adi")]
    string IslemAdi,

    [property: JsonPropertyName("durum")]
    string Durum,

    [property: JsonPropertyName("son_calisma_zamani")]
    DateTimeOffset? SonCalismaZamaniUtc,

    [property: JsonPropertyName("sonraki_calisma_zamani")]
    DateTimeOffset? SonrakiCalismaZamaniUtc,

    [property: JsonPropertyName("islenen_bayi_sayisi")]
    int IslenenBayiSayisi,

    [property: JsonPropertyName("son_hata_mesaji")]
    string? SonHataMesaji)
{
    public static JobDurumResponseDto From(JobDurumBilgisi model) =>
        new(
            IslemAdi: model.IslemAdi,
            Durum: model.Durum,
            SonCalismaZamaniUtc: model.SonCalismaZamaniUtc,
            SonrakiCalismaZamaniUtc: model.SonrakiCalismaZamaniUtc,
            IslenenBayiSayisi: model.IslenenBayiSayisi,
            SonHataMesaji: model.SonHataMesaji);
}
