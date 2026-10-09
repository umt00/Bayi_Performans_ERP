using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Services;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.DataAccess.Repositories;
using BayiPerformansAppWeb.Entities;
using BayiPerformansAppWeb.Entities.ReadModels;
using Xunit;

namespace BayiPerformansAppWeb.Business.Tests;

public sealed class VarlikYonetimServiceTests
{
    private sealed class InMemoryBayiReadRepo : IBayiReadRepository
    {
        public readonly Dictionary<string, BayiReadModel> Bayiler = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<BayiReadModel>> ListAsync(string? segment, string? bolge, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BayiReadModel>>(Bayiler.Values.ToList());

        public Task<BayiReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default)
        {
            Bayiler.TryGetValue(kod, out var b);
            return Task.FromResult(b);
        }
    }

    private sealed class InMemoryUrunReadRepo : IUrunReadRepository
    {
        public readonly Dictionary<string, UrunReadModel> Urunler = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<UrunReadModel>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UrunReadModel>>(Urunler.Values.ToList());

        public Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
            ListAllAsync(ct);

        public Task<UrunReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default)
        {
            Urunler.TryGetValue(kod, out var u);
            return Task.FromResult(u);
        }
    }

    private sealed class InMemoryDepoReadRepo : IDepoReadRepository
    {
        public readonly Dictionary<string, DepoReadModel> Depolar = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<DepoReadModel>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DepoReadModel>>(Depolar.Values.ToList());

        public Task<DepoReadModel?> GetByCodeAsync(string kod, CancellationToken ct = default)
        {
            Depolar.TryGetValue(kod, out var d);
            return Task.FromResult(d);
        }
    }

    private sealed class InMemoryBayiWriteRepo(InMemoryBayiReadRepo readRepo) : IBayiWriteRepository
    {
        public Task<bool> EkleAsync(BayiReadModel bayi, CancellationToken ct = default)
        {
            readRepo.Bayiler[bayi.BayiKodu] = bayi;
            return Task.FromResult(true);
        }

        public Task<bool> GuncelleAsync(BayiReadModel bayi, CancellationToken ct = default)
        {
            readRepo.Bayiler[bayi.BayiKodu] = bayi;
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryUrunWriteRepo(InMemoryUrunReadRepo readRepo) : IUrunWriteRepository
    {
        public Task<bool> EkleAsync(UrunReadModel urun, CancellationToken ct = default)
        {
            readRepo.Urunler[urun.UrunKodu] = urun;
            return Task.FromResult(true);
        }

        public Task<bool> GuncelleAsync(UrunReadModel urun, CancellationToken ct = default)
        {
            readRepo.Urunler[urun.UrunKodu] = urun;
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryStokWriteRepo : IStokWriteRepository
    {
        public readonly Dictionary<string, StokReadModel> Stoklar = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> KaydetVeyaGuncelleAsync(StokReadModel stok, CancellationToken ct = default)
        {
            Stoklar[$"{stok.UrunKodu}:{stok.DepoKodu}"] = stok;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task BayiYonetimService_EkleAsync_GecerliBayi_BasariylaEkler()
    {
        var readRepo = new InMemoryBayiReadRepo();
        var writeRepo = new InMemoryBayiWriteRepo(readRepo);
        var service = new BayiYonetimService(readRepo, writeRepo);

        var command = new YeniBayiCommand("B900", "Yeni Test Bayi", "Ankara", "Ic Anadolu", "KOBI");
        var result = await service.EkleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("B900", result.Value.BayiKodu);
        Assert.Equal("Yeni Test Bayi", result.Value.BayiAdi);
    }

    [Fact]
    public async Task BayiYonetimService_EkleAsync_MukerrerBayi_ConflictHatasiDoner()
    {
        var readRepo = new InMemoryBayiReadRepo();
        readRepo.Bayiler["B900"] = new BayiReadModel("B900", "KOBI", "Ic Anadolu", "Ankara", "Eski Bayi");
        var writeRepo = new InMemoryBayiWriteRepo(readRepo);
        var service = new BayiYonetimService(readRepo, writeRepo);

        var command = new YeniBayiCommand("B900", "Yeni Test Bayi", "Ankara", "Ic Anadolu", "KOBI");
        var result = await service.EkleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("BAYI_ZATEN_MEVCUT", result.ErrorCode);
    }

    [Fact]
    public async Task UrunYonetimService_EkleAsync_GecerliUrun_BasariylaEkler()
    {
        var readRepo = new InMemoryUrunReadRepo();
        var writeRepo = new InMemoryUrunWriteRepo(readRepo);
        var service = new UrunYonetimService(readRepo, writeRepo);

        var command = new YeniUrunCommand("U900", "Kablosuz Kulaklık", "Elektronik", "Ses", "Sony", 1500m);
        var result = await service.EkleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("U900", result.Value.UrunKodu);
        Assert.Equal(1500m, result.Value.BirimMaliyet);
    }

    [Fact]
    public async Task StokYonetimService_KaydetVeyaGuncelleAsync_GecerliStok_BasariylaKaydeder()
    {
        var urunRepo = new InMemoryUrunReadRepo();
        urunRepo.Urunler["U900"] = new UrunReadModel("U900", "Elektronik", "Ses", 1500m, "Kulaklık", "Sony");

        var depoRepo = new InMemoryDepoReadRepo();
        depoRepo.Depolar["DP01"] = new DepoReadModel("DP01", "Marmara", "Merkez Depo", "Istanbul");

        var writeRepo = new InMemoryStokWriteRepo();
        var service = new StokYonetimService(urunRepo, depoRepo, writeRepo);

        var command = new YeniStokCommand("U900", "DP01", 100m, 15m);
        var result = await service.KaydetVeyaGuncelleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("U900", result.Value.UrunKodu);
        Assert.Equal(100m, result.Value.MevcutStok);
    }
}
