using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class OtpControllerTests
{
    private sealed class FakeOtpService : IOtpService
    {
        public bool ShouldFailCooldown { get; set; }
        public bool ShouldFailValidation { get; set; }
        public bool ShouldFailNotFound { get; set; }

        public Task<ServiceResult<OtpUretimSonucu>> UretAsync(
            string hedef,
            string amac,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFailValidation || string.IsNullOrWhiteSpace(hedef))
            {
                return Task.FromResult(ServiceResult<OtpUretimSonucu>.Fail("GECERSIZ_HEDEF", "Gecersiz hedef"));
            }

            if (ShouldFailCooldown)
            {
                return Task.FromResult(ServiceResult<OtpUretimSonucu>.Fail("OTP_COOLDOWN_AKTIF", "Lutfen bekleyin"));
            }

            var sonuc = new OtpUretimSonucu(
                IslemId: "islem-123",
                Hedef: hedef,
                Amac: amac,
                GecerlilikSaniyesi: 180,
                YenidenGonderimBeklemeSaniyesi: 60,
                MaskelenmisHedef: "t***t@domain.com",
                Kod: "123456");

            return Task.FromResult(ServiceResult<OtpUretimSonucu>.Success(sonuc));
        }

        public Task<ServiceResult<OtpDogrulamaSonucu>> DogrulaAsync(
            string islemId,
            string kod,
            CancellationToken cancellationToken = default)
        {
            if (kod == "123456")
            {
                return Task.FromResult(ServiceResult<OtpDogrulamaSonucu>.Success(
                    new OtpDogrulamaSonucu(
                        Dogrulandi: true,
                        DogrulamaToken: "test-token-xyz")));
            }

            return Task.FromResult(ServiceResult<OtpDogrulamaSonucu>.Success(
                new OtpDogrulamaSonucu(
                    Dogrulandi: false,
                    KalanDenemeHakki: 2,
                    HataKodu: "OTP_HATALI_KOD",
                    HataMesaji: "Hatali kod")));
        }

        public Task<ServiceResult<OtpDurumBilgisi>> GetirDurumAsync(
            string islemId,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFailNotFound)
            {
                return Task.FromResult(ServiceResult<OtpDurumBilgisi>.Fail("OTP_BULUNAMADI", "Bulunamadi"));
            }

            var durum = new OtpDurumBilgisi(
                IslemId: islemId,
                MaskelenmisHedef: "t***t@domain.com",
                Amac: "LOGIN_2FA",
                KalanGecerlilikSaniyesi: 150,
                KalanDenemeHakki: 3,
                SuresiDolduMu: false,
                KilitlendiMi: false);

            return Task.FromResult(ServiceResult<OtpDurumBilgisi>.Success(durum));
        }
    }

    [Fact]
    public async Task GonderAsync_Login2FaAmaci_SifreDogrulamasiGerekli400Doner()
    {
        var fakeService = new FakeOtpService();
        var controller = new OtpController(fakeService);

        var request = new OtpGonderRequestDto("user@example.com", OtpAmaclari.Login2Fa);
        var actionResult = await controller.GonderAsync(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(
            "LOGIN_OTP_SIFRE_DOGRULAMASI_GEREKLI",
            problem.Extensions["hata_kodu"]);
    }

    [Fact]
    public async Task GonderAsync_SifreSifirlamaAmaci_200OkVeDtoDoner()
    {
        var fakeService = new FakeOtpService();
        var controller = new OtpController(fakeService);

        var request = new OtpGonderRequestDto(
            "user@example.com",
            OtpAmaclari.SifreSifirlama);
        var actionResult = await controller.GonderAsync(
            request,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<OtpGonderResponseDto>(okResult.Value);
        Assert.Equal("islem-123", response.IslemId);
        Assert.Equal("user@example.com", response.Hedef);
        Assert.Equal(180, response.GecerlilikSaniyesi);
    }

    [Fact]
    public async Task GonderAsync_GecersizHedef_400BadRequestDoner()
    {
        var fakeService = new FakeOtpService();
        var controller = new OtpController(fakeService);

        var request = new OtpGonderRequestDto("", OtpAmaclari.Login2Fa);
        var actionResult = await controller.GonderAsync(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task GonderAsync_CooldownAktifse_429TooManyRequestsDoner()
    {
        var fakeService = new FakeOtpService { ShouldFailCooldown = true };
        var controller = new OtpController(fakeService);

        var request = new OtpGonderRequestDto("user@example.com", OtpAmaclari.SifreSifirlama);
        var actionResult = await controller.GonderAsync(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, objectResult.StatusCode);
    }

    [Fact]
    public async Task DogrulaAsync_GecerliKod_200OkVeDogrulandiDoner()
    {
        var fakeService = new FakeOtpService();
        var controller = new OtpController(fakeService);

        var request = new OtpDogrulaRequestDto("islem-123", "123456");
        var actionResult = await controller.DogrulaAsync(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<OtpDogrulaResponseDto>(okResult.Value);
        Assert.True(response.Dogrulandi);
        Assert.Equal("test-token-xyz", response.DogrulamaToken);
    }

    [Fact]
    public async Task DogrulaAsync_GecersizKodUzunlugu_400BadRequestDoner()
    {
        var fakeService = new FakeOtpService();
        var controller = new OtpController(fakeService);

        var request = new OtpDogrulaRequestDto("islem-123", "123");
        var actionResult = await controller.DogrulaAsync(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task GetirDurumAsync_GecerliIslem_200OkDoner()
    {
        var fakeService = new FakeOtpService();
        var controller = new OtpController(fakeService);

        var actionResult = await controller.GetirDurumAsync("islem-123", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<OtpDurumResponseDto>(okResult.Value);
        Assert.Equal("islem-123", response.IslemId);
        Assert.Equal(3, response.KalanDenemeHakki);
    }
}
