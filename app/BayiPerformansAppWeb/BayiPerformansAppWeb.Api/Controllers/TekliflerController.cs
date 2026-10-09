using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Validation;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

/// <summary>
/// Çoklu ürün içeren tekliflerin oluşturulması, saklanması ve karar motoruyla değerlendirilmesini yöneten API controller'ı.
/// </summary>
[ApiController]
[Route("teklifler")]
[Route("api/v1/teklifler")]
[Produces("application/json")]
public sealed class TekliflerController : ApiControllerBase
{
    private readonly ITeklifYonetimService _teklifService;

    public TekliflerController(ITeklifYonetimService teklifService)
    {
        _teklifService = teklifService;
    }

    /// <summary>
    /// Çoklu ürün içeren yeni bir teklif oluşturur ve kaydeder.
    /// DİKKAT: Bu endpoint henüz hesaplama/değerlendirme yapmaz, teklifi TASLAK olarak kaydedip tekil TeklifId üretir.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TeklifResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeklifResponseDto>> OlusturAsync(
        [FromBody] YeniTeklifRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidInput<TeklifResponseDto>("istek", "Teklif gövdesi zorunludur.");
        }

        var command = ToCommand(request);

        var result = await _teklifService.TeklifOlusturAsync(command, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return FromService(result, TeklifResponseDto.From);
        }

        var responseDto = TeklifResponseDto.From(result.Value);
        return CreatedAtAction("GetirById", new { id = responseDto.TeklifId }, responseDto);
    }

    /// <summary>
    /// UI'daki tek buton akışı için teklifi önce kaydeder, ardından aynı teklif
    /// kimliğiyle değerlendirir. İki işlem ayrı servis adımı olarak sıralı yürür.
    /// </summary>
    [HttpPost("olustur-ve-degerlendir")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TeklifResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TeklifResponseDto>> OlusturVeDegerlendirAsync(
        [FromBody] YeniTeklifRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidInput<TeklifResponseDto>(
                "istek",
                "Teklif gövdesi zorunludur.");
        }

        var createResult = await _teklifService.TeklifOlusturAsync(
            ToCommand(request),
            cancellationToken);
        if (!createResult.IsSuccess || createResult.Value is null)
        {
            return FromService(createResult, TeklifResponseDto.From);
        }

        var evaluationResult = await _teklifService.TeklifDegerlendirByIdAsync(
            createResult.Value.TeklifId,
            cancellationToken);
        if (!evaluationResult.IsSuccess)
        {
            return FromService(
                evaluationResult,
                _ => TeklifResponseDto.From(createResult.Value));
        }

        var persistedResult = await _teklifService.TeklifGetirAsync(
            createResult.Value.TeklifId,
            cancellationToken);
        return FromService(persistedResult, TeklifResponseDto.From);
    }

    /// <summary>
    /// Teklifleri bayi, şehir, bölge, durum ve oluşturulma tarihi filtreleriyle listeler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TeklifResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TeklifResponseDto>>> ListeleAsync(
        [FromQuery(Name = "bayi_kodu")] string? bayiKodu,
        [FromQuery] string? sehir,
        [FromQuery] string? bolge,
        [FromQuery] string? durum,
        [FromQuery(Name = "baslangic_tarihi")] DateTimeOffset? baslangicTarihi,
        [FromQuery(Name = "bitis_tarihi")] DateTimeOffset? bitisTarihi,
        CancellationToken cancellationToken)
    {
        string? normalizedBayiKodu = string.IsNullOrWhiteSpace(bayiKodu)
            ? null
            : ApiInput.NormalizeCode(bayiKodu);
        string? normalizedSehir = ApiInput.NormalizeOptionalText(sehir);
        string? normalizedBolge = ApiInput.NormalizeOptionalText(bolge);
        string? normalizedDurum = string.IsNullOrWhiteSpace(durum)
            ? null
            : durum.Trim().ToUpperInvariant();

        if (normalizedBayiKodu?.Length > 100)
        {
            return InvalidInput<IReadOnlyList<TeklifResponseDto>>(
                "bayi_kodu",
                "bayi_kodu en fazla 100 karakter olabilir.");
        }
        if (normalizedSehir?.Length > 100)
        {
            return InvalidInput<IReadOnlyList<TeklifResponseDto>>(
                "sehir",
                "sehir en fazla 100 karakter olabilir.");
        }
        if (normalizedBolge?.Length > 100)
        {
            return InvalidInput<IReadOnlyList<TeklifResponseDto>>(
                "bolge",
                "bolge en fazla 100 karakter olabilir.");
        }
        if (normalizedDurum is not null && !TeklifDurumlari.GecerliMi(normalizedDurum))
        {
            return InvalidInput<IReadOnlyList<TeklifResponseDto>>(
                "durum",
                "durum TASLAK, BEKLIYOR, DEGERLENDIRILDI, ONAYLANDI veya REDDEDILDI olmalıdır.");
        }
        if (baslangicTarihi.HasValue
            && bitisTarihi.HasValue
            && baslangicTarihi.Value > bitisTarihi.Value)
        {
            return InvalidInput<IReadOnlyList<TeklifResponseDto>>(
                "tarih_araligi",
                "baslangic_tarihi bitis_tarihi değerinden sonra olamaz.");
        }

        var filtre = new TeklifListeFiltre(
            BayiKodu: normalizedBayiKodu,
            Sehir: normalizedSehir,
            Bolge: normalizedBolge,
            Durum: normalizedDurum,
            BaslangicTarihiUtc: baslangicTarihi?.ToUniversalTime(),
            BitisTarihiUtc: bitisTarihi?.ToUniversalTime());

        var result = await _teklifService.TeklifleriListeleAsync(filtre, cancellationToken);
        return FromService(result, (IReadOnlyList<TeklifModel> list) => (IReadOnlyList<TeklifResponseDto>)list.Select(TeklifResponseDto.From).ToList());
    }

    /// <summary>
    /// TeklifId üzerinden teklif detayını ve varsa karar motoru değerlendirme sonucunu getirir.
    /// </summary>
    [HttpGet("{id}")]
    [HttpGet("{id}/detay")]
    [ProducesResponseType(typeof(TeklifResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeklifResponseDto>> GetirByIdAsync(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var result = await _teklifService.TeklifGetirAsync(id, cancellationToken);
        return FromService(result, TeklifResponseDto.From);
    }

    /// <summary>
    /// Daha önceden oluşturulmuş bir teklifi TeklifId üzerinden deterministik karar motorunda değerlendirir.
    /// Her kalem için marj, iskonto ve stok kontrolü yapılarak APPROVE / REJECT / NEED_APPROVAL kararı ve gerekçe kodları üretilir.
    /// </summary>
    [HttpPost("{id}/degerlendir")]
    [HttpPost("{id}/yeniden-degerlendir")]
    [ProducesResponseType(typeof(TeklifCokluDegerlendirmeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TeklifCokluDegerlendirmeResponseDto>> DegerlendirByIdAsync(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var result = await _teklifService.TeklifDegerlendirByIdAsync(id, cancellationToken);
        return FromService(result, TeklifCokluDegerlendirmeResponseDto.From);
    }

    private static YeniTeklifCommand ToCommand(YeniTeklifRequestDto request) =>
        new(
            BayiKodu: request.BayiKodu,
            Aciklama: request.Aciklama,
            Kalemler: request.Kalemler.Select(k => new TeklifKalemCommand(
                UrunKodu: k.UrunKodu,
                DepoKodu: k.DepoKodu,
                Miktar: k.Miktar!.Value,
                BirimFiyat: k.BirimFiyat!.Value,
                IskontoOrani: k.IskontoOrani!.Value)).ToList());
}
