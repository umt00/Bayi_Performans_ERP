using System;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Repositories;
using BayiPerformansAppWeb.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class OtpServiceTests
{
    private readonly InMemoryOtpStore _store = new(TimeProvider.System);
    private readonly RecordingOtpNotificationSender _notificationSender = new();
    private readonly OtpOptions _options = new()
    {
        GecerlilikSuresiSaniye = 180,
        YenidenGonderimBeklemeSuresiSaniye = 60,
        MaksimumDenemeSayisi = 3,
        KodUzunlugu = 6
    };

    private OtpService CreateService(IOtpNotificationSender? notificationSender = null)
    {
        return new OtpService(
            _store,
            notificationSender ?? _notificationSender,
            Options.Create(_options),
            TimeProvider.System);
    }

    [Fact]
    public async Task UretAsync_GecerliHedefVeAmac_BasariliUretimSonucuDoner()
    {
        var service = CreateService();

        var result = await service.UretAsync(
            "test@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value.IslemId);
        Assert.Equal("test@sirket.com", result.Value.Hedef);
        Assert.Equal(OtpAmaclari.Login2Fa, result.Value.Amac);
        Assert.Equal(180, result.Value.GecerlilikSaniyesi);
        Assert.Equal(60, result.Value.YenidenGonderimBeklemeSaniyesi);
        Assert.NotNull(result.Value.Kod);
        Assert.Equal(6, result.Value.Kod.Length);
        Assert.Equal(result.Value.Kod, _notificationSender.LastCode);
        Assert.Equal("test@sirket.com", _notificationSender.LastRecipient);
    }

    [Fact]
    public async Task UretAsync_EpostaGonderilemezse_KaydiVeCooldownuTemizler()
    {
        var failingSender = new RecordingOtpNotificationSender
        {
            Failure = new OtpNotificationException("SMTP kullanilamiyor.")
        };
        var service = CreateService(failingSender);

        var result = await service.UretAsync(
            "teslim@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorKind.Unavailable, result.ErrorKind);
        Assert.Equal("OTP_EPOSTA_GONDERILEMEDI", result.ErrorCode);
        Assert.Null(await _store.GetRemainingCooldownAsync(
            "teslim@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("telefon")]
    [InlineData("5551234567")]
    [InlineData("eposta@")]
    public async Task UretAsync_EpostaOlmayanHedef_ValidationHatasiDoner(string hedef)
    {
        var service = CreateService();

        var result = await service.UretAsync(
            hedef,
            OtpAmaclari.Login2Fa,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GECERSIZ_EPOSTA", result.ErrorCode);
        Assert.Null(_notificationSender.LastCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UretAsync_GecersizHedef_HataDoner(string? hedef)
    {
        var service = CreateService();

        var result = await service.UretAsync(
            hedef!,
            OtpAmaclari.Login2Fa,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GECERSIZ_HEDEF", result.ErrorCode);
    }

    [Fact]
    public async Task UretAsync_GecersizAmac_HataDoner()
    {
        var service = CreateService();

        var result = await service.UretAsync(
            "test@sirket.com",
            "GECERSIZ_AMAC_123",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("GECERSIZ_OTP_AMACI", result.ErrorCode);
    }

    [Fact]
    public async Task UretAsync_CooldownSuresiIcindeTekrarIstenirse_HataDoner()
    {
        var service = CreateService();

        var firstResult = await service.UretAsync(
            "cooldown@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        Assert.True(firstResult.IsSuccess);

        var secondResult = await service.UretAsync(
            "cooldown@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);

        Assert.False(secondResult.IsSuccess);
        Assert.Equal("OTP_COOLDOWN_AKTIF", secondResult.ErrorCode);
    }

    [Fact]
    public async Task DogrulaAsync_GecerliKod_BasariliVeTokenDoner()
    {
        var service = CreateService();

        var uretimResult = await service.UretAsync(
            "dogrula@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        Assert.True(uretimResult.IsSuccess);

        string islemId = uretimResult.Value!.IslemId;
        string kod = uretimResult.Value.Kod!;

        var dogrulamaResult = await service.DogrulaAsync(
            islemId,
            kod,
            CancellationToken.None);

        Assert.True(dogrulamaResult.IsSuccess);
        Assert.NotNull(dogrulamaResult.Value);
        Assert.True(dogrulamaResult.Value.Dogrulandi);
        Assert.NotEmpty(dogrulamaResult.Value.DogrulamaToken!);

        var loginProofService = new LoginOtpDogrulamaService(
            _store,
            TimeProvider.System);
        var consumeResult = await loginProofService.DogrulamaTokeniniTuketAsync(
            "dogrula@sirket.com",
            dogrulamaResult.Value.DogrulamaToken!,
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        Assert.True(consumeResult.IsSuccess);

        var reuseResult = await loginProofService.DogrulamaTokeniniTuketAsync(
            "dogrula@sirket.com",
            dogrulamaResult.Value.DogrulamaToken!,
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        Assert.False(reuseResult.IsSuccess);
        Assert.Equal("OTP_DOGRULAMA_TOKENI_GECERSIZ", reuseResult.ErrorCode);

        // Tek kullanımlık olmalı, ikinci doğrulama başarısız olmalı
        var secondVerify = await service.DogrulaAsync(islemId, kod, CancellationToken.None);
        Assert.False(secondVerify.Value!.Dogrulandi);
    }

    [Fact]
    public async Task DogrulaAsync_HataliKod_KalanDenemeSayisiniAzaltir()
    {
        var service = CreateService();

        var uretimResult = await service.UretAsync(
            "hatali@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        string islemId = uretimResult.Value!.IslemId;

        var dogrulamaResult = await service.DogrulaAsync(
            islemId,
            "000000",
            CancellationToken.None);

        Assert.True(dogrulamaResult.IsSuccess);
        Assert.False(dogrulamaResult.Value!.Dogrulandi);
        Assert.Equal(2, dogrulamaResult.Value.KalanDenemeHakki);
        Assert.Equal("OTP_HATALI_KOD", dogrulamaResult.Value.HataKodu);
    }

    [Fact]
    public async Task DogrulaAsync_MaksimumDenemeAsildiginda_KilitlendiHatasiDonerVeSilinir()
    {
        var service = CreateService();

        var uretimResult = await service.UretAsync(
            "kilit@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        string islemId = uretimResult.Value!.IslemId;

        // 1. deneme
        await service.DogrulaAsync(islemId, "111111", CancellationToken.None);
        // 2. deneme
        await service.DogrulaAsync(islemId, "222222", CancellationToken.None);
        // 3. deneme (Maksimum deneme aşımı)
        var thirdResult = await service.DogrulaAsync(islemId, "333333", CancellationToken.None);

        Assert.False(thirdResult.Value!.Dogrulandi);
        Assert.Equal("OTP_KILITLENDI", thirdResult.Value.HataKodu);
        Assert.Equal(0, thirdResult.Value.KalanDenemeHakki);

        // Artık silinmiş olmalı
        var durumResult = await service.GetirDurumAsync(islemId, CancellationToken.None);
        Assert.False(durumResult.IsSuccess);
    }

    [Fact]
    public async Task DogrulaAsync_OlmayanIslemId_BulunamadiHatasiDoner()
    {
        var service = CreateService();

        var result = await service.DogrulaAsync(
            "olmayan-islem-id",
            "123456",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Dogrulandi);
        Assert.Equal("OTP_BULUNAMADI_VEYA_SURESI_DOLDU", result.Value.HataKodu);
    }

    [Fact]
    public async Task GetirDurumAsync_GecerliIslemId_DogruDurumBilgisiDoner()
    {
        var service = CreateService();

        var uretimResult = await service.UretAsync(
            "durum@sirket.com",
            OtpAmaclari.Login2Fa,
            CancellationToken.None);
        string islemId = uretimResult.Value!.IslemId;

        var durumResult = await service.GetirDurumAsync(islemId, CancellationToken.None);

        Assert.True(durumResult.IsSuccess);
        Assert.NotNull(durumResult.Value);
        Assert.Equal(islemId, durumResult.Value.IslemId);
        Assert.Equal(OtpAmaclari.Login2Fa, durumResult.Value.Amac);
        Assert.Equal(3, durumResult.Value.KalanDenemeHakki);
        Assert.False(durumResult.Value.SuresiDolduMu);
        Assert.False(durumResult.Value.KilitlendiMi);
    }

    private sealed class RecordingOtpNotificationSender : IOtpNotificationSender
    {
        public string? LastRecipient { get; private set; }

        public string? LastCode { get; private set; }

        public Exception? Failure { get; init; }

        public Task SendAsync(
            string recipientEmail,
            string code,
            string purpose,
            TimeSpan validity,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            LastRecipient = recipientEmail;
            LastCode = code;
            return Task.CompletedTask;
        }
    }
}
