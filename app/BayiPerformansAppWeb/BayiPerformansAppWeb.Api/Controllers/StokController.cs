using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Validation;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

[ApiController]
[Route("stok")]
public sealed class StokController(
    IKritikStokService service,
    IStokEnvanterService envanterService,
    IUrunStokMatrisiService? matrisService = null,
    IStokYonetimService? stokYonetimService = null) : ApiControllerBase
{
    private static readonly HashSet<string> AllowedStokDurumlari = new(StringComparer.OrdinalIgnoreCase)
    {
        "TUKENDI", "KRITIK", "NORMAL"
    };

    private static readonly HashSet<string> AllowedKritikSiralaAlanlari = new(StringComparer.OrdinalIgnoreCase)
    {
        "urun_kodu", "depo_kodu", "mevcut_stok",
        "son_uc_ay_satis_adedi", "tahmini_stok_gunu"
    };

    private static readonly HashSet<string> AllowedEnvanterSiralaAlanlari = new(StringComparer.OrdinalIgnoreCase)
    {
        "urun_kodu", "depo_kodu", "mevcut_stok", "min_stok_seviyesi",
        "son_uc_ay_satis_adedi", "son_uc_ay_satis", "tahmini_stok_gunu"
    };

    private static readonly HashSet<string> AllowedSiralaYonleri = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc", "desc"
    };

    [HttpGet("kritik")]
    [ProducesResponseType<KritikStokResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<KritikStokResponseDto>> ListeleAsync(
        [FromQuery(Name = "depo_kodu")] string? depoKodu,
        [FromQuery(Name = "urun_kodu")] string? urunKodu,
        [FromQuery] string? kategori,
        [FromQuery(Name = "alt_kategori")] string? altKategori,
        [FromQuery(Name = "gerekce_kodu")] string? gerekceKodu,
        [FromQuery(Name = "max_stok_gunu")] decimal? maxStokGunu,
        [FromQuery(Name = "sirala_alan")] string? siralaAlan,
        [FromQuery(Name = "sirala_yon")] string? siralaYon,
        [FromQuery] int? sayfa,
        [FromQuery(Name = "sayfa_boyutu")] int? sayfaBoyutu,
        CancellationToken cancellationToken)
    {
        if (maxStokGunu.HasValue && maxStokGunu.Value < 0)
        {
            return InvalidInput<KritikStokResponseDto>(
                "max_stok_gunu",
                "max_stok_gunu 0 veya daha buyuk bir deger olmalidir.");
        }

        if (siralaYon is not null && !AllowedSiralaYonleri.Contains(siralaYon.Trim()))
        {
            return InvalidInput<KritikStokResponseDto>(
                "sirala_yon",
                "sirala_yon yalnizca 'asc' veya 'desc' olabilir.");
        }

        if (siralaAlan is not null && !AllowedKritikSiralaAlanlari.Contains(siralaAlan.Trim()))
        {
            return InvalidInput<KritikStokResponseDto>(
                "sirala_alan",
                "sirala_alan gecersiz alan adi iceriyor.");
        }

        if (sayfa.HasValue && sayfa.Value < 1)
        {
            return InvalidInput<KritikStokResponseDto>(
                "sayfa",
                "sayfa 1 veya daha buyuk bir deger olmalidir.");
        }

        if (sayfaBoyutu.HasValue && (sayfaBoyutu.Value < 1 || sayfaBoyutu.Value > 100))
        {
            return InvalidInput<KritikStokResponseDto>(
                "sayfa_boyutu",
                "sayfa_boyutu 1 ile 100 arasinda bir deger olmalidir.");
        }

        var query = new KritikStokQuery(
            DepoKodu: ApiInput.NormalizeOptionalText(depoKodu),
            UrunKodu: ApiInput.NormalizeOptionalText(urunKodu),
            Kategori: ApiInput.NormalizeOptionalText(kategori),
            AltKategori: ApiInput.NormalizeOptionalText(altKategori),
            GerekceKodu: ApiInput.NormalizeOptionalText(gerekceKodu),
            MaxStokGunu: maxStokGunu,
            SiralaAlan: ApiInput.NormalizeOptionalText(siralaAlan),
            SiralaYon: ApiInput.NormalizeOptionalText(siralaYon),
            Sayfa: sayfa,
            SayfaBoyutu: sayfaBoyutu);

        var result = await service.ListeleAsync(query, cancellationToken);
        return FromService(result, KritikStokResponseDto.From);
    }

    [HttpGet("envanter")]
    [ProducesResponseType<StokEnvanterResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StokEnvanterResponseDto>> ListeleEnvanterAsync(
        [FromQuery(Name = "depo_kodu")] string? depoKodu,
        [FromQuery(Name = "urun_kodu")] string? urunKodu,
        [FromQuery] string? bolge,
        [FromQuery] string? kategori,
        [FromQuery(Name = "alt_kategori")] string? altKategori,
        [FromQuery(Name = "stok_durumu")] string? stokDurumu,
        [FromQuery(Name = "kritik_mi")] bool? kritikMi,
        [FromQuery(Name = "min_mevcut_stok")] decimal? minMevcutStok,
        [FromQuery(Name = "max_mevcut_stok")] decimal? maxMevcutStok,
        [FromQuery(Name = "max_stok_gunu")] decimal? maxStokGunu,
        [FromQuery] string? arama,
        [FromQuery(Name = "sirala_alan")] string? siralaAlan,
        [FromQuery(Name = "sirala_yon")] string? siralaYon,
        [FromQuery] int? sayfa,
        [FromQuery(Name = "sayfa_boyutu")] int? sayfaBoyutu,
        CancellationToken cancellationToken)
    {
        if (stokDurumu is not null)
        {
            var parts = stokDurumu.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Any(p => !AllowedStokDurumlari.Contains(p)))
            {
                return InvalidInput<StokEnvanterResponseDto>(
                    "stok_durumu",
                    "stok_durumu yalnizca TUKENDI, KRITIK veya NORMAL olabilir.");
            }
        }

        if (minMevcutStok.HasValue && maxMevcutStok.HasValue && minMevcutStok.Value > maxMevcutStok.Value)
        {
            return InvalidInput<StokEnvanterResponseDto>(
                "min_mevcut_stok",
                "min_mevcut_stok max_mevcut_stok degerinden buyuk olamaz.");
        }

        if (maxStokGunu.HasValue && maxStokGunu.Value < 0)
        {
            return InvalidInput<StokEnvanterResponseDto>(
                "max_stok_gunu",
                "max_stok_gunu 0 veya daha buyuk bir deger olmalidir.");
        }

        if (siralaYon is not null && !AllowedSiralaYonleri.Contains(siralaYon.Trim()))
        {
            return InvalidInput<StokEnvanterResponseDto>(
                "sirala_yon",
                "sirala_yon yalnizca 'asc' veya 'desc' olabilir.");
        }

        if (siralaAlan is not null && !AllowedEnvanterSiralaAlanlari.Contains(siralaAlan.Trim()))
        {
            return InvalidInput<StokEnvanterResponseDto>(
                "sirala_alan",
                "sirala_alan gecersiz alan adi iceriyor.");
        }

        if (sayfa.HasValue && sayfa.Value < 1)
        {
            return InvalidInput<StokEnvanterResponseDto>(
                "sayfa",
                "sayfa 1 veya daha buyuk bir deger olmalidir.");
        }

        if (sayfaBoyutu.HasValue && (sayfaBoyutu.Value < 1 || sayfaBoyutu.Value > 100))
        {
            return InvalidInput<StokEnvanterResponseDto>(
                "sayfa_boyutu",
                "sayfa_boyutu 1 ile 100 arasinda bir deger olmalidir.");
        }

        string? normalizedSiralaAlan = ApiInput.NormalizeOptionalText(siralaAlan);
        if (string.Equals(normalizedSiralaAlan, "son_uc_ay_satis_adedi", StringComparison.OrdinalIgnoreCase))
        {
            normalizedSiralaAlan = "son_uc_ay_satis";
        }

        var query = new StokEnvanterQuery(
            DepoKodu: ApiInput.NormalizeOptionalText(depoKodu),
            UrunKodu: ApiInput.NormalizeOptionalText(urunKodu),
            Bolge: ApiInput.NormalizeOptionalText(bolge),
            Kategori: ApiInput.NormalizeOptionalText(kategori),
            AltKategori: ApiInput.NormalizeOptionalText(altKategori),
            StokDurumu: ApiInput.NormalizeOptionalText(stokDurumu),
            KritikMi: kritikMi,
            MinMevcutStok: minMevcutStok,
            MaxMevcutStok: maxMevcutStok,
            MaxStokGunu: maxStokGunu,
            Arama: ApiInput.NormalizeOptionalText(arama),
            SiralaAlan: normalizedSiralaAlan,
            SiralaYon: ApiInput.NormalizeOptionalText(siralaYon),
            Sayfa: sayfa,
            SayfaBoyutu: sayfaBoyutu);

        var result = await envanterService.ListeleAsync(query, cancellationToken);
        return FromService(result, StokEnvanterResponseDto.From);
    }

    [HttpGet("ozet")]
    [ProducesResponseType<StokOzetResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StokOzetResponseDto>> GetirOzetAsync(
        [FromQuery(Name = "depo_kodu")] string? depoKodu,
        CancellationToken cancellationToken)
    {
        if (depoKodu is not null && !ApiInput.IsValidCode(depoKodu))
        {
            return InvalidInput<StokOzetResponseDto>(
                "depo_kodu",
                "depo_kodu gecersiz.");
        }

        var query = new StokOzetQuery(depoKodu is null ? null : ApiInput.NormalizeCode(depoKodu));
        var result = await envanterService.GetirOzetAsync(query, cancellationToken);

        return FromService(result, StokOzetResponseDto.From);
    }

    [HttpGet("urun-dagilimi")]
    [ProducesResponseType<UrunStokMatrisiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UrunStokMatrisiResponseDto>> GetirUrunDagilimiAsync(
        [FromQuery(Name = "urun_kodlari")] string? urunKodlari,
        [FromQuery] string? kategori,
        [FromQuery(Name = "alt_kategori")] string? altKategori,
        [FromQuery(Name = "kritik_mi")] bool? kritikMi,
        [FromQuery] string? arama,
        [FromQuery] int? sayfa,
        [FromQuery(Name = "sayfa_boyutu")] int? sayfaBoyutu,
        CancellationToken cancellationToken)
    {
        if (matrisService is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "Servis hazir degil." });
        }

        if (sayfa.HasValue && sayfa.Value < 1)
        {
            return InvalidInput<UrunStokMatrisiResponseDto>("sayfa", "sayfa 1 veya daha buyuk bir deger olmalidir.");
        }

        if (sayfaBoyutu.HasValue && (sayfaBoyutu.Value < 1 || sayfaBoyutu.Value > 100))
        {
            return InvalidInput<UrunStokMatrisiResponseDto>("sayfa_boyutu", "sayfa_boyutu 1 ile 100 arasinda bir deger olmalidir.");
        }

        var query = new UrunStokMatrisiQuery(
            UrunKodlari: ApiInput.NormalizeOptionalText(urunKodlari),
            Kategori: ApiInput.NormalizeOptionalText(kategori),
            AltKategori: ApiInput.NormalizeOptionalText(altKategori),
            KritikMi: kritikMi,
            Arama: ApiInput.NormalizeOptionalText(arama),
            Sayfa: sayfa,
            SayfaBoyutu: sayfaBoyutu);

        var result = await matrisService.GetirMatrisAsync(query, cancellationToken);
        return FromService(result, UrunStokMatrisiResponseDto.From);
    }

    /// <summary>
    /// Ürün ve depo bazında yeni stok kaydı ekler veya mevcut stok miktarını günceller.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(StokKayitResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StokKayitResponseDto>> KaydetStokAsync(
        [FromBody] YeniStokRequestDto request,
        CancellationToken cancellationToken)
    {
        if (stokYonetimService is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "Servis hazir degil." });
        }

        if (request is null)
        {
            return InvalidInput<StokKayitResponseDto>("istek", "Stok gövdesi zorunludur.");
        }

        var command = new YeniStokCommand(
            UrunKodu: request.UrunKodu,
            DepoKodu: request.DepoKodu,
            MevcutStok: request.MevcutStok!.Value,
            MinStokSeviyesi: request.MinStokSeviyesi!.Value);

        var result = await stokYonetimService.KaydetVeyaGuncelleAsync(command, cancellationToken);
        return FromService(result, StokKayitResponseDto.From);
    }
}
