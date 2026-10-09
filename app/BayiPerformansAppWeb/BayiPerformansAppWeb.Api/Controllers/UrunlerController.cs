using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Controllers;

/// <summary>
/// Ürün tanımlama, listeleme ve detay sorgulama işlemlerini sağlayan API controller'ı.
/// </summary>
[ApiController]
[Route("urunler")]
[Produces("application/json")]
public sealed class UrunlerController : ApiControllerBase
{
    private readonly IUrunYonetimService _urunService;

    public UrunlerController(IUrunYonetimService urunService)
    {
        _urunService = urunService;
    }

    /// <summary>
    /// Sistemde kayıtlı ürünleri listeler. Kategori ve arama filtreleri destekler.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UrunDetayResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UrunDetayResponseDto>>> ListeleAsync(
        [FromQuery] string? kategori,
        [FromQuery] string? arama,
        CancellationToken cancellationToken)
    {
        var result = await _urunService.ListeleAsync(kategori, arama, cancellationToken);
        return FromService(result, (IReadOnlyList<UrunReadModel> list) => (IReadOnlyList<UrunDetayResponseDto>)list.Select(UrunDetayResponseDto.From).ToList());
    }

    /// <summary>
    /// Belirtilen ürün koduna ait ürün detayını ve maliyet bilgisini getirir.
    /// </summary>
    [HttpGet("{urunKodu}")]
    [ProducesResponseType(typeof(UrunDetayResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UrunDetayResponseDto>> GetirByKodAsync(
        [FromRoute] string urunKodu,
        CancellationToken cancellationToken)
    {
        var result = await _urunService.GetirByKodAsync(urunKodu, cancellationToken);
        return FromService(result, UrunDetayResponseDto.From);
    }

    /// <summary>
    /// Sisteme yeni bir ürün tanımlar ve kaydeder.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(UrunDetayResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UrunDetayResponseDto>> EkleAsync(
        [FromBody] YeniUrunRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidInput<UrunDetayResponseDto>("istek", "Ürün gövdesi zorunludur.");
        }

        var command = new YeniUrunCommand(
            UrunKodu: request.UrunKodu,
            UrunAdi: request.UrunAdi,
            Kategori: request.Kategori ?? "Genel",
            AltKategori: request.AltKategori ?? "Genel",
            Marka: request.Marka ?? "Genel",
            BirimMaliyet: request.BirimMaliyet!.Value);

        var result = await _urunService.EkleAsync(command, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return FromService(result, UrunDetayResponseDto.From);
        }

        var response = UrunDetayResponseDto.From(result.Value);
        return CreatedAtAction("GetirByKod", new { urunKodu = response.UrunKodu }, response);
    }
}
