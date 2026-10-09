using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Api.Integration;

internal sealed class PendingBusinessServices :
    IBayiPerformansService,
    IRiskListesiService,
    IKritikStokService,
    ITeklifDegerlendirmeService
{
    private const string ErrorCode = "BUSINESS_KATMANI_HAZIR_DEGIL";
    private const string ErrorDetail =
        "Deterministik metrik ve karar servisleri henuz API katmanina baglanmadi.";

    public Task<ServiceResult<BayiPerformansOutput>> GetirAsync(
        string bayiKodu,
        DateOnly? donem,
        CancellationToken cancellationToken)
    {
        return NotReady<BayiPerformansOutput>();
    }

    public Task<ServiceResult<RiskListesiOutput>> ListeleAsync(
        RiskListesiQuery query,
        CancellationToken cancellationToken)
    {
        return NotReady<RiskListesiOutput>();
    }

    public Task<ServiceResult<KritikStokOutput>> ListeleAsync(
        KritikStokQuery query,
        CancellationToken cancellationToken)
    {
        return NotReady<KritikStokOutput>();
    }

    public Task<ServiceResult<TeklifDegerlendirmeOutput>> DegerlendirAsync(
        TeklifDegerlendirmeCommand command,
        CancellationToken cancellationToken)
    {
        return NotReady<TeklifDegerlendirmeOutput>();
    }

    private static Task<ServiceResult<T>> NotReady<T>()
    {
        return Task.FromResult(
            ServiceResult<T>.Fail(
                ServiceErrorKind.Unavailable,
                ErrorCode,
                ErrorDetail));
    }
}

internal sealed class PendingLoginOtpDogrulamaService : ILoginOtpDogrulamaService
{
    public Task<ServiceResult<bool>> DogrulamaTokeniniTuketAsync(
        string email,
        string dogrulamaToken,
        string amac,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            ServiceResult<bool>.Fail(
                ServiceErrorKind.Unavailable,
                "OTP_DOGRULAMA_ENTEGRASYONU_HAZIR_DEGIL",
                "Tek kullanimlik OTP dogrulama tokeni henuz Redis ile giris akisina baglanmadi."));
    }
}
