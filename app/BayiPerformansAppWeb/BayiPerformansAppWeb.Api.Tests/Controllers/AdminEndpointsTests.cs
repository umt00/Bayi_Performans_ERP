using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class AdminEndpointsTests
{
    private static readonly BayilerOzetOutput DummyOzet = new(
        new DateOnly(2026, 1, 1), 0, 0, 0m, null, null, null, null, 0, 0, 0, 0, new Dictionary<string, int>());

    private static readonly StokEnvanterOutput DummyEnvanter = new(
        DateTimeOffset.UtcNow, Array.Empty<StokEnvanterItemOutput>(), 0, null, null);

    private static readonly StokOzetOutput DummyStokOzet = new(
        DateTimeOffset.UtcNow, 0, 0m, 0, 0, Array.Empty<DepoStokOzetOutput>());

    [Fact]
    public async Task BayilerController_GetirOzet_BasariliIse200VeDtoDonmelidir()
    {
        // ARRANGE
        var mockOzet = new BayilerOzetOutput(
            new DateOnly(2026, 1, 1),
            10,
            8,
            100000m,
            120000m,
            0.8333m,
            0.18m,
            0.08m,
            2,
            2,
            6,
            1,
            new Dictionary<string, int> { ["HEDEF_ALTI"] = 2 });

        var ozetService = new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(mockOzet));
        var stubRiskService = new StubRiskListesiService();

        var controller = new BayilerController(stubRiskService, ozetService);

        // ACT
        var actionResult = await controller.GetirOzetAsync("2026-01", null, null, CancellationToken.None);

        // ASSERT
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var responseDto = Assert.IsType<BayilerOzetResponseDto>(okResult.Value);
        Assert.Equal(10, responseDto.ToplamBayiSayisi);
        Assert.Equal(100000m, responseDto.ToplamGerceklesenCiro);
        Assert.Equal(0.8333m, responseDto.GenelHedefeUlasmaPct);
    }

    [Fact]
    public async Task BayilerController_MinRiskMaxRisktenBuyukse_400BadRequestDonmelidir()
    {
        // ARRANGE
        var stubRiskService = new StubRiskListesiService();
        var stubOzetService = new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(DummyOzet));
        var controller = new BayilerController(stubRiskService, stubOzetService);

        // ACT
        var actionResult = await controller.ListeleAsync(
            donem: "2026-01", segment: null, bolge: null, sehir: null,
            riskSeviyesi: null,
            minRiskSkoru: 80, maxRiskSkoru: 50, // min > max
            gerekceKodu: null, minHedefeUlasmaPct: null, maxHedefeUlasmaPct: null,
            minMarjPct: null, maxMarjPct: null, minIskontoPct: null, maxIskontoPct: null,
            minCiro: null, maxCiro: null, aktiviteDurumu: null, kritikStokVarMi: null,
            arama: null, siralaAlan: null, siralaYon: null, sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        // ASSERT
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task BayilerController_GecersizRiskSeviyesi_400BadRequestDonmelidir()
    {
        // ARRANGE
        var stubRiskService = new StubRiskListesiService();
        var stubOzetService = new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(DummyOzet));
        var controller = new BayilerController(stubRiskService, stubOzetService);

        // ACT
        var actionResult = await controller.ListeleAsync(
            donem: "2026-01", segment: null, bolge: null, sehir: null,
            riskSeviyesi: "GECERSIZ_SEVIYE",
            minRiskSkoru: null, maxRiskSkoru: null,
            gerekceKodu: null, minHedefeUlasmaPct: null, maxHedefeUlasmaPct: null,
            minMarjPct: null, maxMarjPct: null, minIskontoPct: null, maxIskontoPct: null,
            minCiro: null, maxCiro: null, aktiviteDurumu: null, kritikStokVarMi: null,
            arama: null, siralaAlan: null, siralaYon: null, sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        // ASSERT
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task BayilerController_SayfaBoyutu100denBuyukse_400BadRequestDonmelidir()
    {
        // ARRANGE
        var stubRiskService = new StubRiskListesiService();
        var stubOzetService = new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(DummyOzet));
        var controller = new BayilerController(stubRiskService, stubOzetService);

        // ACT
        var actionResult = await controller.ListeleAsync(
            donem: "2026-01", segment: null, bolge: null, sehir: null,
            riskSeviyesi: null, minRiskSkoru: null, maxRiskSkoru: null,
            gerekceKodu: null, minHedefeUlasmaPct: null, maxHedefeUlasmaPct: null,
            minMarjPct: null, maxMarjPct: null, minIskontoPct: null, maxIskontoPct: null,
            minCiro: null, maxCiro: null, aktiviteDurumu: null, kritikStokVarMi: null,
            arama: null, siralaAlan: null, siralaYon: null,
            sayfa: 1, sayfaBoyutu: 150, // > 100
            cancellationToken: CancellationToken.None);

        // ASSERT
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task BayilerController_RiskSkoruAralikDisindaysa_400BadRequestDonmelidir()
    {
        var controller = new BayilerController(
            new StubRiskListesiService(),
            new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(DummyOzet)));

        var actionResult = await controller.ListeleAsync(
            donem: "2026-01", segment: null, bolge: null, sehir: null,
            riskSeviyesi: null, minRiskSkoru: -1, maxRiskSkoru: null,
            gerekceKodu: null, minHedefeUlasmaPct: null, maxHedefeUlasmaPct: null,
            minMarjPct: null, maxMarjPct: null, minIskontoPct: null, maxIskontoPct: null,
            minCiro: null, maxCiro: null, aktiviteDurumu: null, kritikStokVarMi: null,
            arama: null, siralaAlan: null, siralaYon: null, sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task BayilerController_BusinessTarafindaDesteklenmeyenSiralama_400BadRequestDonmelidir()
    {
        var controller = new BayilerController(
            new StubRiskListesiService(),
            new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(DummyOzet)));

        var actionResult = await controller.ListeleAsync(
            donem: "2026-01", segment: null, bolge: null, sehir: null,
            riskSeviyesi: null, minRiskSkoru: null, maxRiskSkoru: null,
            gerekceKodu: null, minHedefeUlasmaPct: null, maxHedefeUlasmaPct: null,
            minMarjPct: null, maxMarjPct: null, minIskontoPct: null, maxIskontoPct: null,
            minCiro: null, maxCiro: null, aktiviteDurumu: null, kritikStokVarMi: null,
            arama: null, siralaAlan: "segment", siralaYon: "asc", sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task BayilerController_GetirSehirDagilimi_BasariliIse200VeDtoDonmelidir()
    {
        // ARRANGE
        var mockSehir = new BayiSehirDagilimiOutput(
            new DateOnly(2026, 1, 1),
            1,
            2,
            new List<BayiSehirItemOutput>
            {
                new("Istanbul", "Marmara", 2, 2, 50000m, 0, 1, 1, new List<BayiSehirDetayItemOutput>
                {
                    new("B001", "Bayi 1", "Kurumsal", 30000m, 20, "DUSUK"),
                    new("B002", "Bayi 2", "KOBI", 20000m, 50, "ORTA")
                })
            });

        var stubSehirService = new StubBayiSehirDagilimService(
            ServiceResult<BayiSehirDagilimiOutput>.Success(mockSehir),
            ServiceResult<BayiBolgeDagilimiOutput>.Success(new BayiBolgeDagilimiOutput(new DateOnly(2026, 1, 1), 0, 0, Array.Empty<BayiBolgeItemOutput>())));

        var controller = new BayilerController(new StubRiskListesiService(), new StubBayiOzetService(ServiceResult<BayilerOzetOutput>.Success(DummyOzet)), stubSehirService);

        // ACT
        var actionResult = await controller.GetirSehirDagilimiAsync("2026-01", null, null, CancellationToken.None);

        // ASSERT
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var responseDto = Assert.IsType<BayiSehirDagilimiResponseDto>(okResult.Value);
        Assert.Equal(1, responseDto.ToplamSehirSayisi);
        Assert.Equal(2, responseDto.ToplamBayiSayisi);
        Assert.Single(responseDto.Sehirler);
        Assert.Equal("Istanbul", responseDto.Sehirler[0].Sehir);
    }

    [Fact]
    public async Task BayiController_GetirGecmis_BasariliIse200VeDtoDonmelidir()
    {
        // ARRANGE
        var mockGecmis = new BayiGecmisPerformansOutput(
            "BAYI-001",
            "Kurumsal",
            "Ege",
            new List<BayiAylikPerformansItemOutput>
            {
                new(new DateOnly(2026, 1, 1), 50000m, 60000m, 0.8333m, 0.20m, 0.05m, 0.01m, 15, "DUSUK", Array.Empty<string>())
            });

        var stubBayiPerfService = new StubBayiPerformansService();
        var stubGecmisService = new StubBayiGecmisService(ServiceResult<BayiGecmisPerformansOutput>.Success(mockGecmis));

        var controller = new BayiController(stubBayiPerfService, stubGecmisService);

        // ACT
        var actionResult = await controller.GetirGecmisAsync("BAYI-001", 6, CancellationToken.None);

        // ASSERT
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var responseDto = Assert.IsType<BayiGecmisResponseDto>(okResult.Value);
        Assert.Equal("BAYI-001", responseDto.BayiKodu);
        Assert.Single(responseDto.AylikPerformanslar);
        Assert.Equal(50000m, responseDto.AylikPerformanslar[0].GerceklesenCiro);
    }

    [Fact]
    public async Task StokController_GetirEnvanter_BasariliIse200VeDtoDonmelidir()
    {
        // ARRANGE
        var mockEnvanter = new StokEnvanterOutput(
            DateTimeOffset.UtcNow,
            new List<StokEnvanterItemOutput>
            {
                new("U001", "D001", "Kat1", "Alt1", 50m, 10m, 90m, 50m, false, Array.Empty<string>(), "Urun 1")
            },
            1,
            1,
            50);

        var stubKritikService = new StubKritikStokService();
        var stubEnvanterService = new StubStokEnvanterService(
            ServiceResult<StokEnvanterOutput>.Success(mockEnvanter),
            ServiceResult<StokOzetOutput>.Success(new StokOzetOutput(DateTimeOffset.UtcNow, 1, 50m, 0, 0, Array.Empty<DepoStokOzetOutput>())));

        var controller = new StokController(stubKritikService, stubEnvanterService);

        // ACT
        var actionResult = await controller.ListeleEnvanterAsync(
            depoKodu: null, urunKodu: null, bolge: null, kategori: null, altKategori: null,
            stokDurumu: null, kritikMi: null, minMevcutStok: null, maxMevcutStok: null,
            maxStokGunu: null, arama: null, siralaAlan: null, siralaYon: null,
            sayfa: 1, sayfaBoyutu: 50,
            cancellationToken: CancellationToken.None);

        // ASSERT
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var responseDto = Assert.IsType<StokEnvanterResponseDto>(okResult.Value);
        Assert.Equal(1, responseDto.Toplam);
        Assert.Single(responseDto.Stoklar);
        Assert.Equal("U001", responseDto.Stoklar[0].UrunKodu);
    }

    [Fact]
    public async Task StokController_GetirUrunDagilimi_BasariliIse200VeDtoDonmelidir()
    {
        // ARRANGE
        var mockMatris = new UrunDepoStokMatrisiOutput(
            DateTimeOffset.UtcNow,
            1,
            new List<UrunDepoMatrisItemOutput>
            {
                new("U001", "Laptop", "Bilgisayar", "Laptop", 60m, 30m, 0, new List<UrunDepoDetayItemOutput>
                {
                    new("DP01", "Ana Depo", "Istanbul", "Marmara", 60m, 10m, 30m, 60m, false)
                })
            },
            new List<DepoTanimOutput> { new("DP01", "Marmara", "Ana Depo", "Istanbul") });

        var stubMatrisService = new StubUrunStokMatrisiService(ServiceResult<UrunDepoStokMatrisiOutput>.Success(mockMatris));
        var controller = new StokController(new StubKritikStokService(), new StubStokEnvanterService(ServiceResult<StokEnvanterOutput>.Success(DummyEnvanter), ServiceResult<StokOzetOutput>.Success(DummyStokOzet)), stubMatrisService);

        // ACT
        var actionResult = await controller.GetirUrunDagilimiAsync("U001", null, null, null, null, 1, 50, CancellationToken.None);

        // ASSERT
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var responseDto = Assert.IsType<UrunStokMatrisiResponseDto>(okResult.Value);
        Assert.Equal(1, responseDto.Toplam);
        Assert.Single(responseDto.Urunler);
        Assert.Equal("U001", responseDto.Urunler[0].UrunKodu);
        Assert.Equal(60m, responseDto.Urunler[0].ToplamMevcutStok);
    }

    [Fact]
    public async Task StokController_MinMevcutMaxMevcuttanBuyukse_400BadRequestDonmelidir()
    {
        // ARRANGE
        var stubKritikService = new StubKritikStokService();
        var stubEnvanterService = new StubStokEnvanterService(
            ServiceResult<StokEnvanterOutput>.Success(DummyEnvanter),
            ServiceResult<StokOzetOutput>.Success(DummyStokOzet));

        var controller = new StokController(stubKritikService, stubEnvanterService);

        // ACT
        var actionResult = await controller.ListeleEnvanterAsync(
            depoKodu: null, urunKodu: null, bolge: null, kategori: null, altKategori: null,
            stokDurumu: null, kritikMi: null,
            minMevcutStok: 100m, maxMevcutStok: 50m, // min > max
            maxStokGunu: null, arama: null, siralaAlan: null, siralaYon: null,
            sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        // ASSERT
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task StokController_GecersizStokDurumu_400BadRequestDonmelidir()
    {
        // ARRANGE
        var stubKritikService = new StubKritikStokService();
        var stubEnvanterService = new StubStokEnvanterService(
            ServiceResult<StokEnvanterOutput>.Success(DummyEnvanter),
            ServiceResult<StokOzetOutput>.Success(DummyStokOzet));

        var controller = new StokController(stubKritikService, stubEnvanterService);

        // ACT
        var actionResult = await controller.ListeleEnvanterAsync(
            depoKodu: null, urunKodu: null, bolge: null, kategori: null, altKategori: null,
            stokDurumu: "BILINMEYEN_DURUM",
            kritikMi: null, minMevcutStok: null, maxMevcutStok: null,
            maxStokGunu: null, arama: null, siralaAlan: null, siralaYon: null,
            sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        // ASSERT
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task StokController_NegatifMaxStokGunu_400BadRequestDonmelidir()
    {
        var controller = new StokController(
            new StubKritikStokService(),
            new StubStokEnvanterService(
                ServiceResult<StokEnvanterOutput>.Success(DummyEnvanter),
                ServiceResult<StokOzetOutput>.Success(DummyStokOzet)));

        var actionResult = await controller.ListeleEnvanterAsync(
            depoKodu: null, urunKodu: null, bolge: null, kategori: null, altKategori: null,
            stokDurumu: null, kritikMi: null, minMevcutStok: null, maxMevcutStok: null,
            maxStokGunu: -1m, arama: null, siralaAlan: null, siralaYon: null,
            sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
    }

    [Fact]
    public async Task StokController_SatisAdediSiralamaTakmaAdiniBusinessSozlesmesineDonusturur()
    {
        var envanterService = new StubStokEnvanterService(
            ServiceResult<StokEnvanterOutput>.Success(DummyEnvanter),
            ServiceResult<StokOzetOutput>.Success(DummyStokOzet));
        var controller = new StokController(new StubKritikStokService(), envanterService);

        await controller.ListeleEnvanterAsync(
            depoKodu: null, urunKodu: null, bolge: null, kategori: null, altKategori: null,
            stokDurumu: null, kritikMi: null, minMevcutStok: null, maxMevcutStok: null,
            maxStokGunu: null, arama: null, siralaAlan: "son_uc_ay_satis_adedi", siralaYon: "desc",
            sayfa: null, sayfaBoyutu: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(envanterService.LastQuery);
        Assert.Equal("son_uc_ay_satis", envanterService.LastQuery!.SiralaAlan);
    }

    [Fact]
    public async Task TanimlarController_GetirFiltreSecenekleri_BasariliIse200VeDtoDonmelidir()
    {
        // ARRANGE
        var mockSecenekler = new FiltreSecenekleriOutput(
            new[] { "Ege", "Marmara" },
            new[] { "Bireysel", "Kurumsal" },
            new[] { new DepoTanimOutput("D1", "Istanbul") },
            new[] { new KategoriTanimOutput("Elektronik", new[] { "Telefon", "Laptop" }) },
            new[] { "2026-01", "2025-12" },
            new[] { "DUSUK", "ORTA", "YUKSEK" },
            new[] { "HEDEF_ALTI", "DUSUK_MARJ" },
            new[] { "STOK_YETERSIZ" });

        var stubTanimlarService = new StubTanimlarService(ServiceResult<FiltreSecenekleriOutput>.Success(mockSecenekler));
        var controller = new TanimlarController(stubTanimlarService);

        // ACT
        var actionResult = await controller.GetirFiltreSecenekleriAsync(CancellationToken.None);

        // ASSERT
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var responseDto = Assert.IsType<FiltreSecenekleriResponseDto>(okResult.Value);
        Assert.Equal(2, responseDto.Bolgeler.Count);
        Assert.Equal(2, responseDto.Segmentler.Count);
        Assert.Single(responseDto.Depolar);
        Assert.Single(responseDto.Kategoriler);
        Assert.Equal(2, responseDto.Donemler.Count);
        Assert.Equal(2, responseDto.BayiRiskGerekceKodlari.Count);
        Assert.Single(responseDto.StokRiskGerekceKodlari);
    }

    #region Stubs

    private sealed class StubBayiOzetService : IBayiOzetService
    {
        private readonly ServiceResult<BayilerOzetOutput> _result;
        public StubBayiOzetService(ServiceResult<BayilerOzetOutput> result) => _result = result;
        public Task<ServiceResult<BayilerOzetOutput>> GetirOzetAsync(BayilerOzetQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    private sealed class StubRiskListesiService : IRiskListesiService
    {
        public Task<ServiceResult<RiskListesiOutput>> ListeleAsync(RiskListesiQuery query, CancellationToken cancellationToken)
            => Task.FromResult(ServiceResult<RiskListesiOutput>.Success(new RiskListesiOutput(new DateOnly(2026, 1, 1), Array.Empty<RiskListesiItemOutput>(), 0, 1, 50)));
    }

    private sealed class StubBayiGecmisService : IBayiGecmisService
    {
        private readonly ServiceResult<BayiGecmisPerformansOutput> _result;
        public StubBayiGecmisService(ServiceResult<BayiGecmisPerformansOutput> result) => _result = result;
        public Task<ServiceResult<BayiGecmisPerformansOutput>> GetirGecmisAsync(BayiGecmisQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    private sealed class StubBayiPerformansService : IBayiPerformansService
    {
        public Task<ServiceResult<BayiPerformansOutput>> GetirAsync(string bayiKodu, DateOnly? donem, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private sealed class StubBayiSehirDagilimService : IBayiSehirDagilimService
    {
        private readonly ServiceResult<BayiSehirDagilimiOutput> _sehirResult;
        private readonly ServiceResult<BayiBolgeDagilimiOutput> _bolgeResult;

        public StubBayiSehirDagilimService(
            ServiceResult<BayiSehirDagilimiOutput> sehirResult,
            ServiceResult<BayiBolgeDagilimiOutput> bolgeResult)
        {
            _sehirResult = sehirResult;
            _bolgeResult = bolgeResult;
        }

        public Task<ServiceResult<BayiSehirDagilimiOutput>> GetirSehirDagilimiAsync(BayiSehirDagilimiQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_sehirResult);

        public Task<ServiceResult<BayiBolgeDagilimiOutput>> GetirBolgeDagilimiAsync(BayiBolgeDagilimiQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_bolgeResult);
    }

    private sealed class StubKritikStokService : IKritikStokService
    {
        public Task<ServiceResult<KritikStokOutput>> ListeleAsync(KritikStokQuery query, CancellationToken cancellationToken)
            => Task.FromResult(ServiceResult<KritikStokOutput>.Success(new KritikStokOutput(DateTimeOffset.UtcNow, Array.Empty<KritikStokItemOutput>(), 0, 1, 50)));
    }

    private sealed class StubStokEnvanterService : IStokEnvanterService
    {
        private readonly ServiceResult<StokEnvanterOutput> _envanterResult;
        private readonly ServiceResult<StokOzetOutput> _ozetResult;
        public StokEnvanterQuery? LastQuery { get; private set; }

        public StubStokEnvanterService(
            ServiceResult<StokEnvanterOutput> envanterResult,
            ServiceResult<StokOzetOutput> ozetResult)
        {
            _envanterResult = envanterResult;
            _ozetResult = ozetResult;
        }

        public Task<ServiceResult<StokEnvanterOutput>> ListeleAsync(StokEnvanterQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(_envanterResult);
        }

        public Task<ServiceResult<StokOzetOutput>> GetirOzetAsync(StokOzetQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_ozetResult);
    }

    private sealed class StubUrunStokMatrisiService : IUrunStokMatrisiService
    {
        private readonly ServiceResult<UrunDepoStokMatrisiOutput> _result;
        public StubUrunStokMatrisiService(ServiceResult<UrunDepoStokMatrisiOutput> result) => _result = result;
        public Task<ServiceResult<UrunDepoStokMatrisiOutput>> GetirMatrisAsync(UrunStokMatrisiQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    private sealed class StubTanimlarService : ITanimlarService
    {
        private readonly ServiceResult<FiltreSecenekleriOutput> _result;
        public StubTanimlarService(ServiceResult<FiltreSecenekleriOutput> result) => _result = result;

        public Task<ServiceResult<IReadOnlyList<BayiReadModel>>> ListeleBayilerAsync(
            string? segment,
            string? bolge,
            CancellationToken cancellationToken)
            => Task.FromResult(ServiceResult<IReadOnlyList<BayiReadModel>>.Success(Array.Empty<BayiReadModel>()));

        public Task<ServiceResult<IReadOnlyList<DepoReadModel>>> ListeleDepolarAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(ServiceResult<IReadOnlyList<DepoReadModel>>.Success(Array.Empty<DepoReadModel>()));

        public Task<ServiceResult<FiltreSecenekleriOutput>> GetirFiltreSecenekleriAsync(CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    #endregion
}
