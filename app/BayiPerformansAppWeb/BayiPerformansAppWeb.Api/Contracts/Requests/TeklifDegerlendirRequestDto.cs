using System.ComponentModel.DataAnnotations;
using BayiPerformansAppWeb.Api.Validation;

namespace BayiPerformansAppWeb.Api.Contracts.Requests;

public sealed class TeklifDegerlendirRequestDto : IValidatableObject
{
    [Required]
    public string BayiKodu { get; init; } = string.Empty;

    [Required]
    public string UrunKodu { get; init; } = string.Empty;

    [Required]
    public string DepoKodu { get; init; } = string.Empty;

    [Required]
    public decimal? Miktar { get; init; }

    [Required]
    public decimal? BirimFiyat { get; init; }

    [Required]
    public decimal? IskontoOrani { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ApiInput.IsValidCode(BayiKodu))
        {
            yield return new ValidationResult(
                "bayi_kodu gecersiz.",
                [nameof(BayiKodu)]);
        }

        if (!ApiInput.IsValidCode(UrunKodu))
        {
            yield return new ValidationResult(
                "urun_kodu gecersiz.",
                [nameof(UrunKodu)]);
        }

        if (!ApiInput.IsValidCode(DepoKodu))
        {
            yield return new ValidationResult(
                "depo_kodu gecersiz.",
                [nameof(DepoKodu)]);
        }

        if (Miktar is <= 0)
        {
            yield return new ValidationResult(
                "miktar sifirdan buyuk olmalidir.",
                [nameof(Miktar)]);
        }

        if (BirimFiyat is <= 0)
        {
            yield return new ValidationResult(
                "birim_fiyat sifirdan buyuk olmalidir.",
                [nameof(BirimFiyat)]);
        }

        if (IskontoOrani is < 0 or > 1)
        {
            yield return new ValidationResult(
                "iskonto_orani 0 ile 1 arasinda olmalidir.",
                [nameof(IskontoOrani)]);
        }
    }
}
