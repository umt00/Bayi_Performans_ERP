namespace BayiPerformansAppWeb.Api.Authentication;

public sealed record SupabaseSession(
    Guid UserId,
    string Email,
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn);

public sealed record SupabaseRegistration(
    Guid UserId,
    string Email,
    bool EmailConfirmationRequired,
    SupabaseSession? Session);

public sealed record SupabaseAuthAcknowledgement;

public sealed record SupabaseAuthFailure(
    int StatusCode,
    string ErrorCode,
    string Detail,
    string? RetryAfter);

public sealed record SupabaseAuthResult<T>(
    T? Value,
    SupabaseAuthFailure? Failure)
{
    public bool IsSuccess => Value is not null && Failure is null;

    public static SupabaseAuthResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SupabaseAuthResult<T>(value, null);
    }

    public static SupabaseAuthResult<T> Fail(SupabaseAuthFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new SupabaseAuthResult<T>(default, failure);
    }
}
