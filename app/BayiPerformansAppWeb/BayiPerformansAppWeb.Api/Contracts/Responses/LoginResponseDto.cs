namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record LoginResponseDto(
    Guid KullaniciId,
    string Email,
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn);
