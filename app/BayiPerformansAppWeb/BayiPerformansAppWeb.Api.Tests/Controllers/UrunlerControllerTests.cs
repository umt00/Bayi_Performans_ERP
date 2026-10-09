using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class UrunlerControllerTests
{
    private sealed class FakeUrunYonetimService : IUrunYonetimService
    {
        public Task<ServiceResult<UrunReadModel>> EkleAsync(YeniUrunCommand command, CancellationToken ct = default)
        {
            var model = new UrunReadModel(command.UrunKodu, command.Kategori, command.AltKategori, command.BirimMaliyet, command.UrunAdi, command.Marka);
            return Task.FromResult(ServiceResult<UrunReadModel>.Success(model));
        }

        public Task<ServiceResult<IReadOnlyList<UrunReadModel>>> ListeleAsync(string? kategori = null, string? arama = null, CancellationToken ct = default)
        {
            var list = new List<UrunReadModel>
            {
                new("U001", "Elektronik", "Bilgisayar", 10000m, "Laptop", "Penta")
            };
            return Task.FromResult(ServiceResult<IReadOnlyList<UrunReadModel>>.Success(list));
        }

        public Task<ServiceResult<UrunReadModel>> GetirByKodAsync(string urunKodu, CancellationToken ct = default)
        {
            var model = new UrunReadModel(urunKodu, "Elektronik", "Bilgisayar", 10000m, "Laptop", "Penta");
            return Task.FromResult(ServiceResult<UrunReadModel>.Success(model));
        }
    }

    [Fact]
    public async Task EkleAsync_GecerliUrun_201CreatedDoner()
    {
        var service = new FakeUrunYonetimService();
        var controller = new UrunlerController(service);

        var request = new YeniUrunRequestDto("U100", "Monitör", "Elektronik", "Ekran", "LG", 4000);
        var actionResult = await controller.EkleAsync(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<UrunDetayResponseDto>(createdResult.Value);
        Assert.Equal("U100", response.UrunKodu);
    }

    [Fact]
    public async Task ListeleAsync_200OkVeUrunListesiDoner()
    {
        var service = new FakeUrunYonetimService();
        var controller = new UrunlerController(service);

        var actionResult = await controller.ListeleAsync(null, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<UrunDetayResponseDto>>(okResult.Value);
        Assert.NotEmpty(list);
    }
}
