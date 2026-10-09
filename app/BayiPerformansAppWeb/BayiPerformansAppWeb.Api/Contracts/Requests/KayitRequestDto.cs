using System.ComponentModel.DataAnnotations;

namespace BayiPerformansAppWeb.Api.Contracts.Requests;

public sealed class KayitRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string? Sifre { get; init; }
}
