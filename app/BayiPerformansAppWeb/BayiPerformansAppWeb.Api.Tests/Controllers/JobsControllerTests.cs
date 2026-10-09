using System;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Api.Contracts;
using BayiPerformansAppWeb.Api.Controllers;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Controllers;

public sealed class JobsControllerTests
{
    private sealed class FakeScheduledScoreJobService : IScheduledScoreJobService
    {
        public Task<ServiceResult<JobDurumBilgisi>> SkorlariHesaplaVeOnbellekleAsync(CancellationToken ct = default)
        {
            var info = new JobDurumBilgisi(
                IslemAdi: "BAYI_SKOR_HESAPLAMA_JOB",
                Durum: "BASARILI",
                SonCalismaZamaniUtc: DateTimeOffset.UtcNow,
                SonrakiCalismaZamaniUtc: DateTimeOffset.UtcNow.AddHours(24),
                IslenenBayiSayisi: 204,
                SonHataMesaji: null);

            return Task.FromResult(ServiceResult<JobDurumBilgisi>.Success(info));
        }

        public Task<ServiceResult<JobDurumBilgisi>> GetirJobDurumuAsync(CancellationToken ct = default)
        {
            var info = new JobDurumBilgisi(
                IslemAdi: "BAYI_SKOR_HESAPLAMA_JOB",
                Durum: "HAZIR",
                SonCalismaZamaniUtc: null,
                SonrakiCalismaZamaniUtc: null,
                IslenenBayiSayisi: 0,
                SonHataMesaji: null);

            return Task.FromResult(ServiceResult<JobDurumBilgisi>.Success(info));
        }
    }

    [Fact]
    public async Task GetirDurumAsync_200OkVeJobDurumuDoner()
    {
        var service = new FakeScheduledScoreJobService();
        var controller = new JobsController(service);

        var actionResult = await controller.GetirDurumAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<JobDurumResponseDto>(okResult.Value);
        Assert.Equal("HAZIR", response.Durum);
    }

    [Fact]
    public void Controller_ManuelTetiklemeEndpointiYayinlamaz()
    {
        var postActions = typeof(JobsController)
            .GetMethods()
            .Where(method => method
                .GetCustomAttributes(inherit: true)
                .OfType<HttpMethodAttribute>()
                .Any(attribute => attribute.HttpMethods.Contains("POST")));

        Assert.Empty(postActions);
    }
}
