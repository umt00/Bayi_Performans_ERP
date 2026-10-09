using System.ComponentModel.DataAnnotations;

namespace BayiPerformansAppWeb.Api.Contracts.Requests;

public sealed class TokenYenileRequestDto
{
    [Required]
    [StringLength(4096, MinimumLength = 1)]
    [RegularExpression(@".*\S.*")]
    public string? RefreshToken { get; init; }
}
