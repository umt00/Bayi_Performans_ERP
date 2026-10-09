using System.Reflection;
using BayiPerformansAppWeb.DataAccess;
using BayiPerformansAppWeb.DataAccess.Repositories;
using BayiPerformansAppWeb.Entities.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BayiPerformansAppWeb.Api.Tests.DataAccess;

public sealed class PersistenceWriteRepositoryTests
{
    [Fact]
    public async Task BayiGuncelleAsync_GlobalNoTrackingAcikkenDegisikligiKaliciYazar()
    {
        await using var context = CreateContext();
        var repository = new BayiWriteRepository(
            context,
            NullLogger<BayiWriteRepository>.Instance);

        await repository.EkleAsync(
            new BayiReadModel("B900", "KOBI", "Marmara", "Istanbul", "Eski Bayi"));
        context.ChangeTracker.Clear();

        await repository.GuncelleAsync(
            new BayiReadModel("B900", "Kurumsal", "Ege", "Izmir", "Yeni Bayi"));

        var row = await FindRowAsync(context, "BayiRow", "B900");
        Assert.NotNull(row);
        Assert.Equal("Yeni Bayi", ReadProperty<string>(row!, "BayiAdi"));
        Assert.Equal("Kurumsal", ReadProperty<string>(row!, "Segment"));
        Assert.Equal("Ege", ReadProperty<string>(row!, "Bolge"));
    }

    [Fact]
    public async Task UrunGuncelleAsync_GlobalNoTrackingAcikkenDegisikligiKaliciYazar()
    {
        await using var context = CreateContext();
        var repository = new UrunWriteRepository(
            context,
            NullLogger<UrunWriteRepository>.Instance);

        await repository.EkleAsync(
            new UrunReadModel("U900", "Elektronik", "Ses", 100m, "Eski Ürün", "Marka A"));
        context.ChangeTracker.Clear();

        await repository.GuncelleAsync(
            new UrunReadModel("U900", "Elektronik", "Ses", 150m, "Yeni Ürün", "Marka B"));

        var row = await FindRowAsync(context, "UrunRow", "U900");
        Assert.NotNull(row);
        Assert.Equal("Yeni Ürün", ReadProperty<string>(row!, "UrunAdi"));
        Assert.Equal("Marka B", ReadProperty<string>(row!, "Marka"));
        Assert.Equal(150m, ReadProperty<decimal?>(row!, "BirimMaliyet"));
    }

    [Fact]
    public async Task StokKaydetVeyaGuncelleAsync_VarOlanKaydiKaliciGunceller()
    {
        await using var context = CreateContext();
        var repository = new StokWriteRepository(
            context,
            NullLogger<StokWriteRepository>.Instance);

        await repository.KaydetVeyaGuncelleAsync(
            new StokReadModel("U900", "DP01", 10m, 5m, new DateOnly(2026, 8, 1)));
        context.ChangeTracker.Clear();

        await repository.KaydetVeyaGuncelleAsync(
            new StokReadModel("U900", "DP01", 25m, 8m, new DateOnly(2026, 8, 20)));

        var row = await FindRowAsync(context, "StokRow", "U900", "DP01");
        Assert.NotNull(row);
        Assert.Equal(25m, ReadProperty<decimal?>(row!, "MevcutStok"));
        Assert.Equal(8m, ReadProperty<decimal?>(row!, "MinStokSeviyesi"));
        Assert.Equal(new DateOnly(2026, 8, 20), ReadProperty<DateOnly?>(row!, "SonSayimTarihi"));
    }

    [Fact]
    public async Task BayiGuncelleAsync_KayitYoksaBasariTaklidiYapmaz()
    {
        await using var context = CreateContext();
        var repository = new BayiWriteRepository(
            context,
            NullLogger<BayiWriteRepository>.Instance);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            repository.GuncelleAsync(
                new BayiReadModel("B404", "KOBI", "Marmara", "Istanbul", "Olmayan Bayi")));
    }

    [Fact]
    public async Task BayiEkleAsync_DbContextKullanilamiyorsaHatayiYutmaz()
    {
        var context = CreateContext();
        var repository = new BayiWriteRepository(
            context,
            NullLogger<BayiWriteRepository>.Instance);
        await context.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.EkleAsync(
                new BayiReadModel("B900", "KOBI", "Marmara", "Istanbul", "Test Bayi")));
    }

    private static PentaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PentaDbContext>()
            .UseInMemoryDatabase($"write-repository-tests-{Guid.NewGuid():N}")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new PentaDbContext(options);
    }

    private static async Task<object?> FindRowAsync(
        PentaDbContext context,
        string entityTypeName,
        params object?[] keyValues)
    {
        context.ChangeTracker.Clear();
        Type clrType = context.Model
            .GetEntityTypes()
            .Single(entityType => entityType.ClrType.Name == entityTypeName)
            .ClrType;

        return await context.FindAsync(clrType, keyValues);
    }

    private static T? ReadProperty<T>(object instance, string propertyName)
    {
        PropertyInfo property = instance.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"{propertyName} özelliği bulunamadı.");

        return (T?)property.GetValue(instance);
    }
}
