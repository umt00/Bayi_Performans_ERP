using System.ComponentModel.DataAnnotations;

namespace BayiPerformansAppWeb.Api.Contracts.Requests;

public sealed class OnayMailiYenidenGonderRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; init; }
}
