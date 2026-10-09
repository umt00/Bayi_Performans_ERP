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
[Route("bayiler")]
public sealed class BayilerController(
    IRiskListesiService service,
    IBayiOzetService ozetService,
    IBayiSehirDagilimService? sehirDagilimService = null,
    IBayiYonetimService? bayiYonetimService = null,
    ITanimlarService? tanimlarService = null) : ApiControllerBase
{
    private static readonly HashSet<string> AllowedRiskSeviyeleri = new(StringComparer.OrdinalIgnoreCase)
    {
        "DUSUK", "ORTA", "YUKSEK"
    };

    private static readonly HashSet<string> AllowedSiralaAlanlari = new(StringComparer.OrdinalIgnoreCase)
    {
        "bayi_kodu", "sehir", "gerceklesen_ciro", "ciro",
        "hedefe_ulasma_pct", "ortalama_marj_pct", "ortalama_iskonto_pct",
        "risk_skoru"
    };

    private static readonly HashSet<string> AllowedSiralaYonleri = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc", "desc"
    };

    private static readonly HashSet<string> AllowedAktiviteDurumlari = new(StringComparer.OrdinalIgnoreCase)
    {
        "AKTIF", "PASIF", "HEPSI"
    };

    /// <summary>
    /// UI seçim alanlarında kullanılmak üzere bayileri listeler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BayiDetayResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<BayiDetayResponseDto>>> ListeleBayilerAsync(
        [FromQuery] string? segment,
        [FromQuery] string? bolge,
        CancellationToken cancellationToken)
    {
        if (tanimlarService is null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "Servis hazir degil." });
        }

        string? normalizedSegment = ApiInput.NormalizeOptionalText(segment);
        string? normalizedBolge = ApiInput.NormalizeOptionalText(bolge);

        if (normalizedSegment?.Length > 100)
        {
            return InvalidInput<IReadOnlyList<BayiDetayResponseDto>>(
                "segment",
                "segment en fazla 100 karakter olabilir.");
        }

        if (normalizedBolge?.Length > 100)
        {
            return InvalidInput<IReadOnlyList<BayiDetayResponseDto>>(
                "bolge",
                "bolge en fazla 100 karakter olabilir.");
        }

        var result = await tanimlarService.ListeleBayilerAsync(
            normalizedSegment,
            normalizedBolge,
            cancellationToken);

        return FromService(
            result,
            bayiler => (IReadOnlyList<BayiDetayResponseDto>)bayiler
                .Select(BayiDetayResponseDto.From)
                .ToArray());
    }

    [HttpGet("risk-listesi")]
    [ProducesResponseType<RiskListesiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RiskListesiResponseDto>> ListeleAsync(
        [FromQuery] string? donem,
        [FromQuery] string? segment,
        [FromQuery] string? bolge,
        [FromQuery] string? sehir,
        [FromQuery(Name = "risk_seviyesi")] string? riskSeviyesi,
        [FromQuery(Name = "min_risk_skoru")] int? minRiskSkoru,
        [FromQuery(Name = "max_risk_skoru")] int? maxRiskSkoru,
        [FromQuery(Name = "gerekce_kodu")] string? gerekceKodu,
        [FromQuery(Name = "min_hedefe_ulasma_pct")] decimal? minHedefeUlasmaPct,
        [FromQuery(Name = "max_hedefe_ulasma_pct")] decimal? maxHedefeUlasmaPct,
        [FromQuery(Name = "min_marj_pct")] decimal? minMarjPct,
        [FromQuery(Name = "max_marj_pct")] decimal? maxMarjPct,
        [FromQuery(Name = "min_iskonto_pct")] decimal? minIskontoPct,
        [FromQuery(Name = "max_iskonto_pct")] decimal? maxIskontoPct,
        [FromQuery(Name = "min_ciro")] decimal? minCiro,
        [FromQuery(Name = "max_ciro")] decimal? maxCiro,
        [FromQuery(Name = "aktivite_durumu")] string? aktiviteDurumu,
        [FromQuery(Name = "kritik_stok_var_mi")] bool? kritikStokVarMi,
        [FromQuery] string? arama,
        [FromQuery(Name = "sirala_alan")] string? siralaAlan,
        [FromQuery(Name = "sirala_yon")] string? siralaYon,
        [FromQuery] int? sayfa,
        [FromQuery(Name = "sayfa_boyutu")] int? sayfaBoyutu,
        CancellationToken cancellationToken)
    {
        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<RiskListesiResponseDto>(
                "donem",
                "donem YYYY-MM biciminde olmalidir.");
        }

        var normalizedSegment = ApiInput.NormalizeOptionalText(segment);
        var normalizedBolge = ApiInput.NormalizeOptionalText(bolge);
        var normalizedSehir = ApiInput.NormalizeOptionalText(sehir);

        if (riskSeviyesi is not null)
        {
            var parts = riskSeviyesi.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Any(p => !AllowedRiskSeviyeleri.Contains(p)))
            {
                return InvalidInput<RiskListesiResponseDto>(
                    "risk_seviyesi",
                    "risk_seviyesi yalnizca DUSUK, ORTA veya YUKSEK olabilir.");
            }
        }

        if (aktiviteDurumu is not null && !AllowedAktiviteDurumlari.Contains(aktiviteDurumu.Trim()))
        {
            return InvalidInput<RiskListesiResponseDto>(
                "aktivite_durumu",
                "aktivite_durumu yalnizca 'AKTIF', 'PASIF' veya 'HEPSI' olabilir.");
        }

        if (minRiskSkoru.HasValue && maxRiskSkoru.HasValue && minRiskSkoru.Value > maxRiskSkoru.Value)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "min_risk_skoru",
                "min_risk_skoru max_risk_skoru degerinden buyuk olamaz.");
        }

        if (minRiskSkoru is < 0 or > 100)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "min_risk_skoru",
                "min_risk_skoru 0 ile 100 arasinda olmalidir.");
        }

        if (maxRiskSkoru is < 0 or > 100)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "max_risk_skoru",
                "max_risk_skoru 0 ile 100 arasinda olmalidir.");
        }

        if (minHedefeUlasmaPct.HasValue && maxHedefeUlasmaPct.HasValue && minHedefeUlasmaPct.Value > maxHedefeUlasmaPct.Value)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "min_hedefe_ulasma_pct",
                "min_hedefe_ulasma_pct max_hedefe_ulasma_pct degerinden buyuk olamaz.");
        }

        if (minMarjPct.HasValue && maxMarjPct.HasValue && minMarjPct.Value > maxMarjPct.Value)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "min_marj_pct",
                "min_marj_pct max_marj_pct degerinden buyuk olamaz.");
        }

        if (minIskontoPct.HasValue && maxIskontoPct.HasValue && minIskontoPct.Value > maxIskontoPct.Value)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "min_iskonto_pct",
                "min_iskonto_pct max_iskonto_pct degerinden buyuk olamaz.");
        }

        if (minCiro.HasValue && maxCiro.HasValue && minCiro.Value > maxCiro.Value)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "min_ciro",
                "min_ciro max_ciro degerinden buyuk olamaz.");
        }

        if (siralaYon is not null && !AllowedSiralaYonleri.Contains(siralaYon.Trim()))
        {
            return InvalidInput<RiskListesiResponseDto>(
                "sirala_yon",
                "sirala_yon yalnizca 'asc' veya 'desc' olabilir.");
        }

        if (siralaAlan is not null && !AllowedSiralaAlanlari.Contains(siralaAlan.Trim()))
        {
            return InvalidInput<RiskListesiResponseDto>(
                "sirala_alan",
                "sirala_alan gecersiz alan adi iceriyor.");
        }

        if (sayfa.HasValue && sayfa.Value < 1)
        {
            return InvalidInput<RiskListesiResponseDto>(
                "sayfa",
                "sayfa 1 veya daha buyuk bir deger olmalidir.");
        }

        if (sayfaBoyutu.HasValue && (sayfaBoyutu.Value < 1 || sayfaBoyutu.Value > 100))
        {
            return InvalidInput<RiskListesiResponseDto>(
                "sayfa_boyutu",
                "sayfa_boyutu 1 ile 100 arasinda bir deger olmalidir.");
        }

        var query = new RiskListesiQuery(
            Donem: parsedPeriod,
            Segment: normalizedSegment,
            Bolge: normalizedBolge,
            Sehir: normalizedSehir,
            RiskSeviyesi: ApiInput.NormalizeOptionalText(riskSeviyesi),
            MinRiskSkoru: minRiskSkoru,
            MaxRiskSkoru: maxRiskSkoru,
            GerekceKodu: ApiInput.NormalizeOptionalText(gerekceKodu),
            MinHedefeUlasmaPct: minHedefeUlasmaPct,
            MaxHedefeUlasmaPct: maxHedefeUlasmaPct,
            MinMarjPct: minMarjPct,
            MaxMarjPct: maxMarjPct,
            MinIskontoPct: minIskontoPct,
            MaxIskontoPct: maxIskontoPct,
            MinCiro: minCiro,
            MaxCiro: maxCiro,
            AktiviteDurumu: ApiInput.NormalizeOptionalText(aktiviteDurumu),
            KritikStokVarMi: kritikStokVarMi,
            Arama: ApiInput.NormalizeOptionalText(arama),
            SiralaAlan: ApiInput.NormalizeOptionalText(siralaAlan),
            SiralaYon: ApiInput.NormalizeOptionalText(siralaYon),
            Sayfa: sayfa,
            SayfaBoyutu: sayfaBoyutu);

        var result = await service.ListeleAsync(query, cancellationToken);
        return FromService(result, RiskListesiResponseDto.From);
    }

    [HttpGet("ozet")]
    [ProducesResponseType<BayilerOzetResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BayilerOzetResponseDto>> GetirOzetAsync(
        [FromQuery] string? donem,
        [FromQuery] string? segment,
        [FromQuery] string? bolge,
        CancellationToken cancellationToken)
    {
        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<BayilerOzetResponseDto>(
                "donem",
                "donem YYYY-MM biciminde olmalidir.");
        }

        var normalizedSegment = ApiInput.NormalizeOptionalText(segment);
        var normalizedBolge = ApiInput.NormalizeOptionalText(bolge);

        if (normalizedSegment?.Length > 100)
        {
            return InvalidInput<BayilerOzetResponseDto>(
                "segment",
                "segment en fazla 100 karakter olabilir.");
        }

        if (normalizedBolge?.Length > 100)
        {
            return InvalidInput<BayilerOzetResponseDto>(
                "bolge",
                "bolge en fazla 100 karakter olabilir.");
        }

        var query = new BayilerOzetQuery(parsedPeriod, normalizedSegment, normalizedBolge);
        var result = await ozetService.GetirOzetAsync(query, cancellationToken);

        return FromService(result, BayilerOzetResponseDto.From);
    }

    [HttpGet("sehir-dagilimi")]
    [ProducesResponseType<BayiSehirDagilimiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BayiSehirDagilimiResponseDto>> GetirSehirDagilimiAsync(
        [FromQuery] string? donem,
        [FromQuery] string? bolge,
        [FromQuery] string? segment,
        CancellationToken cancellationToken)
    {
        if (sehirDagilimService is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "Servis hazir degil." });
        }

        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<BayiSehirDagilimiResponseDto>("donem", "donem YYYY-MM biciminde olmalidir.");
        }

        var query = new BayiSehirDagilimiQuery(parsedPeriod, ApiInput.NormalizeOptionalText(bolge), ApiInput.NormalizeOptionalText(segment));
        var result = await sehirDagilimService.GetirSehirDagilimiAsync(query, cancellationToken);

        return FromService(result, BayiSehirDagilimiResponseDto.From);
    }

    [HttpGet("bolge-dagilimi")]
    [ProducesResponseType<BayiBolgeDagilimiResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BayiBolgeDagilimiResponseDto>> GetirBolgeDagilimiAsync(
        [FromQuery] string? donem,
        CancellationToken cancellationToken)
    {
        if (sehirDagilimService is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "Servis hazir degil." });
        }

        if (!ApiInput.TryParsePeriod(donem, out var parsedPeriod))
        {
            return InvalidInput<BayiBolgeDagilimiResponseDto>("donem", "donem YYYY-MM biciminde olmalidir.");
        }

        var query = new BayiBolgeDagilimiQuery(parsedPeriod);
        var result = await sehirDagilimService.GetirBolgeDagilimiAsync(query, cancellationToken);

        return FromService(result, BayiBolgeDagilimiResponseDto.From);
    }

    /// <summary>
    /// Yeni bir bayi tanımlar ve sisteme kaydeder.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BayiDetayResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BayiDetayResponseDto>> EkleBayiAsync(
        [FromBody] YeniBayiRequestDto request,
        CancellationToken cancellationToken)
    {
        if (bayiYonetimService is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "Servis hazir degil." });
        }

        if (request is null)
        {
            return InvalidInput<BayiDetayResponseDto>("istek", "Bayi gövdesi zorunludur.");
        }

        var command = new YeniBayiCommand(
            BayiKodu: request.BayiKodu,
            BayiAdi: request.BayiAdi,
            Sehir: request.Sehir ?? string.Empty,
            Bolge: request.Bolge ?? string.Empty,
            Segment: request.Segment ?? "Standart");

        var result = await bayiYonetimService.EkleAsync(command, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return FromService(result, BayiDetayResponseDto.From);
        }

        var response = BayiDetayResponseDto.From(result.Value);
        return Created($"/bayi/{response.BayiKodu}/performans", response);
    }

    /// <summary>
    /// Mevcut bir bayinin bilgilerini günceller.
    /// </summary>
    [HttpPut("{bayiKodu}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BayiDetayResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BayiDetayResponseDto>> GuncelleBayiAsync(
        [FromRoute] string bayiKodu,
        [FromBody] GuncelleBayiRequestDto request,
        CancellationToken cancellationToken)
    {
        if (bayiYonetimService is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails { Title = "Servis hazir degil." });
        }

        if (string.IsNullOrWhiteSpace(bayiKodu))
        {
            return InvalidInput<BayiDetayResponseDto>("bayiKodu", "Bayi kodu zorunludur.");
        }

        if (request is null)
        {
            return InvalidInput<BayiDetayResponseDto>("istek", "Güncelleme gövdesi zorunludur.");
        }

        var command = new GuncelleBayiCommand(
            BayiAdi: request.BayiAdi ?? string.Empty,
            Sehir: request.Sehir ?? string.Empty,
            Bolge: request.Bolge ?? string.Empty,
            Segment: request.Segment ?? string.Empty);

        var result = await bayiYonetimService.GuncelleAsync(bayiKodu, command, cancellationToken);
        return FromService(result, BayiDetayResponseDto.From);
    }
}
