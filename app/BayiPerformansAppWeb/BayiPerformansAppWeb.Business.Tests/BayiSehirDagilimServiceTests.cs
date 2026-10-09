using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class BayiSehirDagilimServiceTests
{
    [Fact]
    public async Task GetirSehirDagilimi_SehirlereGoreGruplayipBayileriVeMetrikleriDonmelidir()
    {
        // ARRANGE
        var donem = new DateOnly(2025, 6, 1);
        var mevcudat = new List<BayiDonemPerformansReadModel>
        {
            new("B001", donem, "Bayi 1", "Kurumsal", "Marmara", 900m, 1000m, 0.9m, 0.2m, 0.05m, 0m, 0, 10, "DUSUK", [], "Istanbul"),
            new("B002", donem, "Bayi 2", "KOBI", "Marmara", 0m, 500m, 0m, 0m, 0m, 0m, 0, 20, "DUSUK", [], "Istanbul"),
            new("B003", donem, "Bayi 3", "Kurumsal", "Ege", 450m, 500m, 0.9m, 0.18m, 0.05m, 0m, 0, 15, "DUSUK", [], "Izmir")
        };

        var service = new BayiSehirDagilimService(new FakeBayiDonemPerformansReadRepo(mevcudat));

        // ACT
        var result = await service.GetirSehirDagilimiAsync(new BayiSehirDagilimiQuery(donem), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.ToplamSehirSayisi);
        Assert.Equal(3, result.Value.ToplamBayiSayisi);

        var istanbul = result.Value.Sehirler.First(s => s.Sehir == "Istanbul");
        Assert.Equal("Marmara", istanbul.Bolge);
        Assert.Equal(2, istanbul.ToplamBayiSayisi);
        Assert.Equal(1, istanbul.AktifBayiSayisi);
        Assert.Equal(900m, istanbul.ToplamCiro);
        Assert.Equal(2, istanbul.Bayiler.Count);

        var izmir = result.Value.Sehirler.First(s => s.Sehir == "Izmir");
        Assert.Equal(1, izmir.ToplamBayiSayisi);
        Assert.Equal(1, izmir.AktifBayiSayisi);
        Assert.Equal(450m, izmir.ToplamCiro);
    }

    [Fact]
    public async Task GetirBolgeDagilimi_BolgelereGoreGruplayipHedefVeCiroAnaliziYapmalidir()
    {
        // ARRANGE
        var donem = new DateOnly(2025, 6, 1);
        var mevcudat = new List<BayiDonemPerformansReadModel>
        {
            new("B001", donem, "Bayi 1", "Kurumsal", "Marmara", 900m, 1000m, 0.9m, 0.2m, 0.05m, 0m, 0, 10, "DUSUK", [], "Istanbul"),
            new("B002", donem, "Bayi 2", "KOBI", "Marmara", 0m, null, null, 0m, 0m, 0m, 0, 20, "DUSUK", [], "Bursa"),
            new("B003", donem, "Bayi 3", "Kurumsal", "Ege", 450m, 500m, 0.9m, 0.18m, 0.05m, 0m, 0, 15, "DUSUK", [], "Izmir")
        };

        var service = new BayiSehirDagilimService(new FakeBayiDonemPerformansReadRepo(mevcudat));

        // ACT
        var result = await service.GetirBolgeDagilimiAsync(new BayiBolgeDagilimiQuery(donem), CancellationToken.None);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.ToplamBolgeSayisi);

        var marmara = result.Value.Bolgeler.First(b => b.Bolge == "Marmara");
        Assert.Equal(2, marmara.ToplamBayiSayisi);
        Assert.Equal(2, marmara.Sehirler.Count); // Istanbul, Bursa
        Assert.Equal(1, marmara.AktifBayiSayisi);
        Assert.Equal(900m, marmara.ToplamCiro);
        Assert.Equal(1000m, marmara.ToplamHedefCiro);
        Assert.Equal(0.9m, marmara.GenelHedefeUlasmaPct); // 900 / 1000 = 0.90
    }

    private sealed class FakeBayiDonemPerformansReadRepo(List<BayiDonemPerformansReadModel> items) : IBayiDonemPerformansReadRepository
    {
        public Task<BayiDonemPerformansPage> ListAsync(BayiDonemPerformansQuery query, CancellationToken cancellationToken)
        {
            var filtered = items.AsEnumerable();
            if (query.Donem.HasValue)
            {
                filtered = filtered.Where(i => i.Donem == query.Donem.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Bolge))
            {
                filtered = filtered.Where(i => string.Equals(i.Bolge, query.Bolge, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Segment))
            {
                filtered = filtered.Where(i => string.Equals(i.Segment, query.Segment, StringComparison.OrdinalIgnoreCase));
            }

            var list = filtered.ToList();
            var effectivePeriod = query.Donem ?? list.FirstOrDefault()?.Donem;
            return Task.FromResult(new BayiDonemPerformansPage(effectivePeriod, list.Count, query.Sayfa, query.SayfaBoyutu, list));
        }

        public Task<BayiDonemPerformansReadModel?> GetAsync(string bayiKodu, DateOnly? donem, CancellationToken cancellationToken)
            => Task.FromResult(items.FirstOrDefault(i => i.BayiKodu == bayiKodu && (!donem.HasValue || i.Donem == donem.Value)));

        public Task<PerformansDashboardStatsReadModel> GetDashboardStatsAsync(DateOnly? donem, CancellationToken cancellationToken)
            => Task.FromResult(new PerformansDashboardStatsReadModel(donem, items.Count, items.Sum(i => i.Ciro ?? 0m), items.Sum(i => i.Hedef ?? 0m), 0, 0, null, null, null));

        public Task<IReadOnlyList<BayiDonemPerformansReadModel>> GetHistoryAsync(string bayiKodu, int limit, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BayiDonemPerformansReadModel>>(items.Where(i => i.BayiKodu == bayiKodu).Take(limit).ToList());
    }
}
