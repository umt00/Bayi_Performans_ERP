using System.Globalization;
using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Validation;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.Search;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

/// <summary>
/// Data ekibinin satislar_index indexine aktardığı satış kayıtlarını tam metin
/// arama ve kesin alan filtreleriyle sorgular. Bu endpoint karar veya performans
/// metriği hesaplamaz.
/// </summary>
[ApiController]
[Route("api/v1/satis/arama")]
public sealed class SatisAramaController(
    IElasticsearchSalesSearch search) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SatisAramaResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SatisAramaResponseDto>> AraAsync(
        [FromQuery] string? q,
        [FromQuery(Name = "baslangic_tarihi")] string? baslangicTarihi,
        [FromQuery(Name = "bitis_tarihi")] string? bitisTarihi,
        [FromQuery(Name = "bayi_kodu")] string? bayiKodu,
        [FromQuery(Name = "urun_kodu")] string? urunKodu,
        [FromQuery(Name = "depo_kodu")] string? depoKodu,
        [FromQuery] string? bolge,
        [FromQuery] string? segment,
        [FromQuery] int sayfa = 1,
        [FromQuery(Name = "sayfa_boyutu")] int sayfaBoyutu = 50,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(q)
            && q.Trim().Length is < 3 or > 100)
        {
            return InvalidInput<SatisAramaResponseDto>(
                "q",
                "q 3 ile 100 karakter arasinda olmalidir.");
        }

        if (!TryParseDate(baslangicTarihi, out DateOnly? parsedStart))
        {
            return InvalidInput<SatisAramaResponseDto>(
                "baslangic_tarihi",
                "baslangic_tarihi YYYY-MM-DD biciminde olmalidir.");
        }

        if (!TryParseDate(bitisTarihi, out DateOnly? parsedEnd))
        {
            return InvalidInput<SatisAramaResponseDto>(
                "bitis_tarihi",
                "bitis_tarihi YYYY-MM-DD biciminde olmalidir.");
        }

        if (parsedStart.HasValue
            && parsedEnd.HasValue
            && parsedEnd.Value < parsedStart.Value)
        {
            return InvalidInput<SatisAramaResponseDto>(
                "bitis_tarihi",
                "bitis_tarihi baslangic_tarihinden once olamaz.");
        }

        if (sayfa < 1)
        {
            return InvalidInput<SatisAramaResponseDto>(
                "sayfa",
                "sayfa en az 1 olmalidir.");
        }

        if (sayfaBoyutu is < 1 or > 100)
        {
            return InvalidInput<SatisAramaResponseDto>(
                "sayfa_boyutu",
                "sayfa_boyutu 1 ile 100 arasinda olmalidir.");
        }

        foreach (var (field, value) in new[]
        {
            ("bayi_kodu", bayiKodu),
            ("urun_kodu", urunKodu),
            ("depo_kodu", depoKodu)
        })
        {
            if (value is not null && !ApiInput.IsValidCode(value))
            {
                return InvalidInput<SatisAramaResponseDto>(
                    field,
                    $"{field} 2-32 karakterlik harf, rakam, '_' veya '-' icermelidir.");
            }
        }

        var query = new SatisSearchQuery(
            ApiInput.NormalizeOptionalText(q),
            parsedStart,
            parsedEnd,
            NormalizeOptionalCode(bayiKodu),
            NormalizeOptionalCode(urunKodu),
            NormalizeOptionalCode(depoKodu),
            ApiInput.NormalizeOptionalText(bolge),
            ApiInput.NormalizeOptionalText(segment),
            sayfa,
            sayfaBoyutu);

        try
        {
            SatisSearchPage page = await search.SearchAsync(
                query,
                cancellationToken);
            return Ok(SatisAramaResponseDto.From(page));
        }
        catch (ElasticsearchSearchException exception)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Arama servisi kullanilamiyor.",
                Detail = exception.Message,
                Instance = Request.Path
            };
            problem.Extensions["hata_kodu"] = exception.ErrorCode;
            problem.Extensions["trace_id"] = HttpContext.TraceIdentifier;
            return StatusCode(StatusCodes.Status503ServiceUnavailable, problem);
        }
    }

    private static bool TryParseDate(string? value, out DateOnly? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateOnly.TryParseExact(
                value.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : ApiInput.NormalizeCode(value);
}
