using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Contracts.Responses;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class KatalogEndpointsTests
{
    [Fact]
    public async Task BayilerController_ListeleBayiler_BasariliIse200VeListeDonmelidir()
    {
        var tanimlarService = new StubTanimlarService(
            bayiler:
            [
                new BayiReadModel("B001", "Kurumsal", "Ege", "Izmir", "Bayi Bir")
            ]);
        var controller = new BayilerController(
            new StubRiskListesiService(),
            new StubBayiOzetService(),
            tanimlarService: tanimlarService);

        var actionResult = await controller.ListeleBayilerAsync(
            " Kurumsal ",
            " Ege ",
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<BayiDetayResponseDto>>(okResult.Value);
        var bayi = Assert.Single(response);
        Assert.Equal("B001", bayi.BayiKodu);
        Assert.Equal("Kurumsal", tanimlarService.LastSegment);
        Assert.Equal("Ege", tanimlarService.LastBolge);
    }

    [Fact]
    public async Task DepolarController_Listele_BasariliIse200VeListeDonmelidir()
    {
        var tanimlarService = new StubTanimlarService(
            depolar:
            [
                new DepoReadModel("DP01", "Marmara", "Merkez Depo", "Istanbul")
            ]);
        var controller = new DepolarController(tanimlarService);

        var actionResult = await controller.ListeleAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<DepoTanimDto>>(okResult.Value);
        var depo = Assert.Single(response);
        Assert.Equal("DP01", depo.DepoKodu);
        Assert.Equal("Merkez Depo", depo.DepoAdi);
    }

    private sealed class StubTanimlarService : ITanimlarService
    {
        private readonly IReadOnlyList<BayiReadModel> _bayiler;
        private readonly IReadOnlyList<DepoReadModel> _depolar;

        public StubTanimlarService(
            IReadOnlyList<BayiReadModel>? bayiler = null,
            IReadOnlyList<DepoReadModel>? depolar = null)
        {
            _bayiler = bayiler ?? Array.Empty<BayiReadModel>();
            _depolar = depolar ?? Array.Empty<DepoReadModel>();
        }

        public string? LastSegment { get; private set; }
        public string? LastBolge { get; private set; }

        public Task<ServiceResult<IReadOnlyList<BayiReadModel>>> ListeleBayilerAsync(
            string? segment,
            string? bolge,
            CancellationToken cancellationToken)
        {
            LastSegment = segment;
            LastBolge = bolge;
            return Task.FromResult(ServiceResult<IReadOnlyList<BayiReadModel>>.Success(_bayiler));
        }

        public Task<ServiceResult<IReadOnlyList<DepoReadModel>>> ListeleDepolarAsync(
            CancellationToken cancellationToken)
            => Task.FromResult(ServiceResult<IReadOnlyList<DepoReadModel>>.Success(_depolar));

        public Task<ServiceResult<FiltreSecenekleriOutput>> GetirFiltreSecenekleriAsync(
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StubRiskListesiService : IRiskListesiService
    {
        public Task<ServiceResult<RiskListesiOutput>> ListeleAsync(
            RiskListesiQuery query,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class StubBayiOzetService : IBayiOzetService
    {
        public Task<ServiceResult<BayilerOzetOutput>> GetirOzetAsync(
            BayilerOzetQuery query,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
