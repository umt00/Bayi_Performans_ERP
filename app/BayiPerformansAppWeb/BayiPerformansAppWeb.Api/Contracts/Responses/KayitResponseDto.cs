namespace BayiPerformansAppWeb.Api.Contracts.Responses;

public sealed record KayitResponseDto(
    Guid KullaniciId,
    string Email,
    bool EmailOnayiGerekli,
    LoginResponseDto? Oturum);
