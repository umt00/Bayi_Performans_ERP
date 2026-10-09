using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BayiPerformansAppWeb.Api.Contracts.Requests;

public sealed class LoginRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string? Sifre { get; init; }

    // Ilk login adiminda alan bos veya null olabilir. Dolu bir degerin
    // gecerliligi tek kullanimlik OTP kaniti tuketilirken kontrol edilir.
    [StringLength(256)]
    [JsonPropertyName("dogrulama_token")]
    public string? DogrulamaToken { get; init; }
}
