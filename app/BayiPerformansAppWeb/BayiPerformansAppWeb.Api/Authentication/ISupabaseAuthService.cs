namespace BayiPerformansAppWeb.Api.Authentication;

public interface ISupabaseAuthService
{
    Task<SupabaseAuthResult<SupabaseRegistration>> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<SupabaseAuthResult<SupabaseSession>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<SupabaseAuthResult<SupabaseSession>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>> LogoutAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<SupabaseAuthResult<SupabaseAuthAcknowledgement>> ResendSignupConfirmationAsync(
        string email,
        CancellationToken cancellationToken);
}
