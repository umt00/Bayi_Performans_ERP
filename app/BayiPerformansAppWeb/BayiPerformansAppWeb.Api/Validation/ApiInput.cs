using System.Globalization;

namespace BayiPerformansAppWeb.Api.Validation;

public static class ApiInput
{
    public static bool IsValidCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length is >= 2 and <= 32
            && trimmed.All(character =>
                char.IsLetterOrDigit(character)
                || character is '_' or '-');
    }

    public static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool TryParsePeriod(string? value, out DateOnly? period)
    {
        period = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateOnly.TryParseExact(
                $"{value.Trim()}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            period = parsed;
            return true;
        }

        return false;
    }
}
