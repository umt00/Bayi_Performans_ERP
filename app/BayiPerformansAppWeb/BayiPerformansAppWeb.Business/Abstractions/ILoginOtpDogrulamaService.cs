using BayiPerformansAppWeb.Business.Models;

namespace BayiPerformansAppWeb.Business.Abstractions;

/// <summary>
/// OTP dogrulamasindan uretilen tek kullanimlik giris kanitini tuketir.
/// Gercek Redis uygulamasi Business/DataAccess entegrasyonu tarafindan saglanir.
/// </summary>
public interface ILoginOtpDogrulamaService
{
    Task<ServiceResult<bool>> DogrulamaTokeniniTuketAsync(
        string email,
        string dogrulamaToken,
        string amac,
        CancellationToken cancellationToken = default);
}
