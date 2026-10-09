using BayiPerformansAppWeb.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BayiPerformansAppWeb.Api.Tests.DataAccess;

public sealed class PerformanceSnapshotMappingTests
{
    [Fact]
    public void DbContext_AylikPerformansKolonlariniKesinlesenSemayaMapler()
    {
        var options = new DbContextOptionsBuilder<PentaDbContext>()
            .UseInMemoryDatabase($"performance-mapping-{Guid.NewGuid():N}")
            .Options;

        using var context = new PentaDbContext(options);
        var entityType = Assert.Single(
            context.Model.GetEntityTypes(),
            entity => entity.ClrType.Name == "BayiDonemPerformansRow");

        Assert.Equal("bayi_donem_performans", entityType.GetTableName());
        Assert.Equal("public", entityType.GetSchema());

        var table = StoreObjectIdentifier.Table(
            entityType.GetTableName()!,
            entityType.GetSchema());
        Assert.Equal(
            "ciro",
            entityType.FindProperty("Ciro")?.GetColumnName(table));
        Assert.Equal(
            "hedef_tutma_orani",
            entityType.FindProperty("HedefTutmaOrani")?.GetColumnName(table));
        Assert.Equal(
            "ortalama_kar_marji",
            entityType.FindProperty("OrtalamaKarMarji")?.GetColumnName(table));
        Assert.Equal(
            "kritik_stok_kalan_urun_sayisi",
            entityType.FindProperty("KritikStokKalanUrunSayisi")?.GetColumnName(table));
    }
}
