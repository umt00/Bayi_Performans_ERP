namespace BayiPerformansAppWeb.Api.Authentication;

internal sealed class SupabaseAuthOptions
{
    public const string SectionName = "SupabaseAuth";
    public const string HttpClientName = "SupabaseAuth";
    public const string RateLimitPolicyName = "auth";

    public string ProjectUrl { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;

    public string Audience { get; set; } = "authenticated";

    public int HttpTimeoutSeconds { get; set; } = 15;
}
