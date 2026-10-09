namespace BayiPerformansAppWeb.Api.Contracts.Responses;

/// <summary>
/// E-posta ve sifre dogrulandiktan sonra baslatilan ikinci faktor islemini temsil eder.
/// </summary>
public sealed record LoginOtpChallengeResponseDto(
    bool OtpGerekli,
    string IslemId,
    string MaskelenmisEmail,
    int GecerlilikSaniyesi,
    int YenidenGonderimSaniyesi);
