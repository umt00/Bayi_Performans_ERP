using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class TekliflerControllerTests
{
    private sealed class FakeTeklifYonetimService : ITeklifYonetimService
    {
        public bool ShouldFailValidation { get; set; }
        public bool ShouldFailNotFound { get; set; }
        public TeklifListeFiltre? ReceivedFilter { get; private set; }
        public List<string> Calls { get; } = [];

        public Task<ServiceResult<TeklifModel>> TeklifOlusturAsync(YeniTeklifCommand command, CancellationToken ct = default)
        {
            Calls.Add("olustur");

            if (ShouldFailValidation || string.IsNullOrWhiteSpace(command.BayiKodu))
            {
                return Task.FromResult(ServiceResult<TeklifModel>.Fail(ServiceErrorKind.Validation, "GECERSIZ_BAYI", "Gecersiz"));
            }

            var model = new TeklifModel(
                TeklifId: "TKLF-2026-ABCD1234",
                BayiKodu: command.BayiKodu,
                BayiAdi: "Test Bayi",
                Aciklama: command.Aciklama,
                Durum: TeklifDurumlari.Taslak,
                ToplamNetTutar: 50000m,
                OlusturulmaTarihiUtc: DateTimeOffset.UtcNow,
                Kalemler: new List<TeklifKalemModel>
                {
                    new(1, "U001", "Laptop", "DP01", "Depo 1", 5, 10000, 0, 50000)
                });

            return Task.FromResult(ServiceResult<TeklifModel>.Success(model));
        }

        public Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> TeklifDegerlendirByIdAsync(string teklifId, CancellationToken ct = default)
        {
            Calls.Add("degerlendir");

            if (ShouldFailNotFound)
            {
                return Task.FromResult(ServiceResult<TeklifCokluDegerlendirmeOutput>.Fail(ServiceErrorKind.NotFound, "TEKLIF_BULUNAMADI", "Bulunamadi"));
            }

            var output = new TeklifCokluDegerlendirmeOutput(
                TeklifId: teklifId,
                BayiKodu: "B001",
                GenelKarar: "APPROVE",
                GenelGerekceKodlari: Array.Empty<string>(),
                ToplamNetTutar: 50000m,
                ToplamMaliyet: 40000m,
                GenelMarjPct: 0.20m,
                DegerlendirilmeTarihiUtc: DateTimeOffset.UtcNow,
                KalemSonuclari: new List<TeklifKalemDegerlendirmeOutput>
                {
                    new(1, "U001", "Laptop", "DP01", 5, 10000, 0, 8000, 50000, 0.20m, 20, true, "APPROVE", Array.Empty<string>())
                },
                RiskSkoru: 25,
                RiskSeviyesi: "DUSUK",
                HedefTutturmaYuzdesi: 92.5m,
                OrtalamaIskontoOrani: 7.5m,
                IadeOrani: 1.25m,
                AiComment: "Teklif uygundur.",
                AlertMessage: "Düşük risk.",
                IsFallback: false);

            return Task.FromResult(ServiceResult<TeklifCokluDegerlendirmeOutput>.Success(output));
        }

        public Task<ServiceResult<TeklifCokluDegerlendirmeOutput>> TeklifAnlikDegerlendirAsync(YeniTeklifCommand command, CancellationToken ct = default) =>
            TeklifDegerlendirByIdAsync("ANLIK-1", ct);

        public Task<ServiceResult<TeklifModel>> TeklifGetirAsync(string teklifId, CancellationToken ct = default)
        {
            Calls.Add("getir");

            if (ShouldFailNotFound)
            {
                return Task.FromResult(ServiceResult<TeklifModel>.Fail(ServiceErrorKind.NotFound, "TEKLIF_BULUNAMADI", "Bulunamadi"));
            }

            var model = new TeklifModel(
                TeklifId: teklifId,
                BayiKodu: "B001",
                BayiAdi: "Test Bayi",
                Aciklama: "Test",
                Durum: TeklifDurumlari.Taslak,
                ToplamNetTutar: 50000m,
                OlusturulmaTarihiUtc: DateTimeOffset.UtcNow,
                Kalemler: Array.Empty<TeklifKalemModel>(),
                KayitliDegerlendirme: Calls.Contains("degerlendir")
                    ? new TeklifDegerlendirmeKaydi(
                        "APPROVE",
                        10,
                        "DUSUK",
                        Array.Empty<string>(),
                        20m,
                        100m,
                        5m,
                        0m,
                        DateTimeOffset.UtcNow)
                    : null);

            return Task.FromResult(ServiceResult<TeklifModel>.Success(model));
        }

        public Task<ServiceResult<IReadOnlyList<TeklifModel>>> TeklifleriListeleAsync(
            TeklifListeFiltre filtre,
            CancellationToken ct = default)
        {
            ReceivedFilter = filtre;
            return Task.FromResult(ServiceResult<IReadOnlyList<TeklifModel>>.Success(new List<TeklifModel>()));
        }
    }

    [Fact]
    public async Task OlusturAsync_GecerliIstek_201CreatedVeDtoDoner()
    {
        var service = new FakeTeklifYonetimService();
        var controller = new TekliflerController(service);

        var request = new YeniTeklifRequestDto(
            BayiKodu: "B001",
            Aciklama: "Yeni Sipariş",
            Kalemler: new List<YeniTeklifKalemRequestDto>
            {
                new("U001", "DP01", 5, 10000, 0)
            });

        var actionResult = await controller.OlusturAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<TeklifResponseDto>(createdResult.Value);
        Assert.Equal("TKLF-2026-ABCD1234", response.TeklifId);
        Assert.Equal("B001", response.BayiKodu);
        Assert.Equal(TeklifDurumlari.Taslak, response.Durum);
    }

    [Fact]
    public async Task DegerlendirByIdAsync_GecerliTeklif_200OkVeDegerlendirmeSonucuDoner()
    {
        var service = new FakeTeklifYonetimService();
        var controller = new TekliflerController(service);

        var actionResult = await controller.DegerlendirByIdAsync("TKLF-2026-ABCD1234", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<TeklifCokluDegerlendirmeResponseDto>(okResult.Value);
        Assert.Equal("APPROVE", response.GenelKarar);
        Assert.Equal(50000m, response.ToplamNetTutar);
        Assert.Equal(25, response.RiskSkoru);
        Assert.Equal("DUSUK", response.RiskSeviyesi);
        Assert.Equal(92.5m, response.HedefTutturmaYuzdesi);
        Assert.Equal(7.5m, response.OrtalamaIskontoOrani);
        Assert.Equal(1.25m, response.IadeOrani);
        Assert.Equal("Teklif uygundur.", response.AiComment);
        Assert.Equal("Düşük risk.", response.AlertMessage);
        Assert.False(response.IsFallback);
    }

    [Fact]
    public async Task OlusturVeDegerlendirAsync_IslemleriSiraliYurutupKayitliSonucuDoner()
    {
        var service = new FakeTeklifYonetimService();
        var controller = new TekliflerController(service);
        var request = new YeniTeklifRequestDto(
            BayiKodu: "B001",
            Aciklama: "Tek adım akışı",
            Kalemler: new List<YeniTeklifKalemRequestDto>
            {
                new("U001", "DP01", 5, 10000, 0)
            });

        var actionResult = await controller.OlusturVeDegerlendirAsync(
            request,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<TeklifResponseDto>(okResult.Value);
        Assert.Equal(new[] { "olustur", "degerlendir", "getir" }, service.Calls);
        Assert.Equal("APPROVE", response.Karar);
        Assert.Equal(10, response.RiskSkoru);
        Assert.Equal("DUSUK", response.RiskSeviyesi);
    }

    [Fact]
    public async Task DegerlendirByIdAsync_OlmayanTeklif_404NotFoundDoner()
    {
        var service = new FakeTeklifYonetimService { ShouldFailNotFound = true };
        var controller = new TekliflerController(service);

        var actionResult = await controller.DegerlendirByIdAsync("TKLF-OLMAYAN", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task ListeleAsync_TumFiltreleriNormalizeEdipServiseIletir()
    {
        var service = new FakeTeklifYonetimService();
        var controller = new TekliflerController(service);
        var baslangic = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var bitis = new DateTimeOffset(2026, 8, 31, 23, 59, 0, TimeSpan.FromHours(3));

        var actionResult = await controller.ListeleAsync(
            " b001 ",
            " İstanbul ",
            " Marmara ",
            " taslak ",
            baslangic,
            bitis,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.NotNull(service.ReceivedFilter);
        Assert.Equal("B001", service.ReceivedFilter.BayiKodu);
        Assert.Equal("İstanbul", service.ReceivedFilter.Sehir);
        Assert.Equal("Marmara", service.ReceivedFilter.Bolge);
        Assert.Equal(TeklifDurumlari.Taslak, service.ReceivedFilter.Durum);
        Assert.Equal(baslangic.ToUniversalTime(), service.ReceivedFilter.BaslangicTarihiUtc);
        Assert.Equal(bitis.ToUniversalTime(), service.ReceivedFilter.BitisTarihiUtc);
    }

    [Fact]
    public async Task ListeleAsync_GecersizDurum_400Doner()
    {
        var service = new FakeTeklifYonetimService();
        var controller = new TekliflerController(service);

        var actionResult = await controller.ListeleAsync(
            null,
            null,
            null,
            "BILINMEYEN",
            null,
            null,
            CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Null(service.ReceivedFilter);
    }
}
