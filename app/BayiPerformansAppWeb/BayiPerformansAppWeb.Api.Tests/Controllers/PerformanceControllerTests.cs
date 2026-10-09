using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class PerformanceControllerTests
{
    [Fact]
    public async Task Listele_GecerliFiltreleriNormalizeEdip200Doner()
    {
        var repository = new StubRepository
        {
            PageResult = new BayiDonemPerformansPage(
                new DateOnly(2026, 8, 1),
                1,
                1,
                25,
                [CreateSnapshot("B0001")])
        };
        var controller = CreateController(repository);

        var action = await controller.ListeleAsync(
            "2026-08",
            " orta ",
            " Marmara ",
            " Platinum ",
            1,
            25,
            null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<PerformansBayiListesiResponseDto>(ok.Value);
        Assert.Equal("2026-08", response.Donem);
        Assert.Single(response.Bayiler);
        Assert.Equal("ORTA", repository.LastQuery?.RiskSeviyesi);
        Assert.Equal("Marmara", repository.LastQuery?.Bolge);
        Assert.Equal("Platinum", repository.LastQuery?.Segment);
    }

    [Theory]
    [InlineData("2026/08", null, 1, 50)]
    [InlineData("2026-08", "BILINMEYEN", 1, 50)]
    [InlineData("2026-08", null, 0, 50)]
    [InlineData("2026-08", null, 1, 101)]
    public async Task Listele_GecersizIsteklerde400Doner(
        string donem,
        string? karar,
        int sayfa,
        int sayfaBoyutu)
    {
        var controller = CreateController(new StubRepository());

        var action = await controller.ListeleAsync(
            donem,
            karar,
            null,
            null,
            sayfa,
            sayfaBoyutu,
            null,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task Getir_KayitVarsaAylikKarneAlanlariniDonderir()
    {
        var repository = new StubRepository
        {
            DetailResult = CreateSnapshot("B0001")
        };
        var controller = CreateController(repository);

        var action = await controller.GetirAsync(
            "b0001",
            "2026-08",
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<PerformansBayiResponseDto>(ok.Value);
        Assert.Equal("B0001", repository.LastBayiKodu);
        Assert.Equal("Örnek Bayi", response.BayiAdi);
        Assert.Equal(125000m, response.Ciro);
        Assert.Equal(14.2m, response.OrtalamaKarMarji);
    }

    [Fact]
    public async Task Getir_KayitYoksa404Doner()
    {
        var controller = CreateController(new StubRepository());

        var action = await controller.GetirAsync(
            "B9999",
            "2026-08",
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("PERFORMANS_BULUNAMADI", problem.Extensions["hata_kodu"]);
    }

    [Fact]
    public async Task DashboardStats_RepositorySonucunu200IleDonderir()
    {
        var repository = new StubRepository
        {
            StatsResult = new PerformansDashboardStatsReadModel(
                new DateOnly(2026, 8, 1),
                185,
                12_500_000m,
                15_000_000m,
                4,
                7,
                85.5m,
                14.2m,
                32.4m)
        };
        var controller = CreateController(repository);

        var action = await controller.GetirIstatistikAsync(
            "2026-08",
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<PerformansDashboardStatsResponseDto>(ok.Value);
        Assert.Equal(185, response.ToplamBayiSayisi);
        Assert.Equal(12_500_000m, response.ToplamCiro);
        Assert.Equal(85.5m, response.OrtalamaHedefTutmaOrani);
    }

    private static PerformanceController CreateController(StubRepository repository)
    {
        return new PerformanceController(repository)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static BayiDonemPerformansReadModel CreateSnapshot(string bayiKodu)
    {
        return new BayiDonemPerformansReadModel(
            bayiKodu,
            new DateOnly(2026, 8, 1),
            "Örnek Bayi",
            "Platinum",
            "Marmara",
            125000m,
            150000m,
            83.33m,
            14.2m,
            7.5m,
            1.2m,
            2,
            50,
            "ORTA",
            ["MARJ_ESIK_ALTI"]);
    }

    private sealed class StubRepository : IBayiDonemPerformansReadRepository
    {
        public BayiDonemPerformansPage PageResult { get; init; } =
            new(null, 0, 1, 50, []);

        public BayiDonemPerformansReadModel? DetailResult { get; init; }

        public PerformansDashboardStatsReadModel StatsResult { get; init; } =
            new(null, 0, 0, 0, 0, 0, null, null, null);

        public BayiDonemPerformansQuery? LastQuery { get; private set; }
        public string? LastBayiKodu { get; private set; }

        public Task<BayiDonemPerformansPage> ListAsync(
            BayiDonemPerformansQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(PageResult);
        }

        public Task<BayiDonemPerformansReadModel?> GetAsync(
            string bayiKodu,
            DateOnly? donem,
            CancellationToken cancellationToken)
        {
            LastBayiKodu = bayiKodu;
            return Task.FromResult(DetailResult);
        }

        public Task<PerformansDashboardStatsReadModel> GetDashboardStatsAsync(
            DateOnly? donem,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(StatsResult);
        }

        public Task<IReadOnlyList<BayiDonemPerformansReadModel>> GetHistoryAsync(
            string bayiKodu,
            int limit,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<BayiDonemPerformansReadModel>>([]);
        }
    }
}
