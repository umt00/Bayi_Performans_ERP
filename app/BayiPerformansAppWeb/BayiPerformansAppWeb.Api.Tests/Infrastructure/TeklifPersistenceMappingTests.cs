using BayiPerformansAppWeb.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace BayiPerformansAppWeb.Api.Tests.Infrastructure;

public sealed class TeklifPersistenceMappingTests
{
    [Fact]
    public void EfModel_NormalizeTeklifTablolariniKesinlesenSemayaEsler()
    {
        var options = new DbContextOptionsBuilder<PentaDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test")
            .Options;

        using var context = new PentaDbContext(options);
        var teklif = context.Model.GetEntityTypes()
            .Single(entity => entity.ClrType.Name == "TeklifRow");
        var kalem = context.Model.GetEntityTypes()
            .Single(entity => entity.ClrType.Name == "TeklifKalemRow");
        var degerlendirme = context.Model.GetEntityTypes()
            .Single(entity => entity.ClrType.Name == "TeklifDegerlendirmeRow");
        var aiYorum = context.Model.GetEntityTypes()
            .Single(entity => entity.ClrType.Name == "TeklifAiYorumRow");

        Assert.Equal("teklifler", teklif.GetTableName());
        Assert.Equal("teklif_kalemleri", kalem.GetTableName());
        Assert.Equal("public", teklif.GetSchema());
        Assert.Equal("public", kalem.GetSchema());
        Assert.Equal("teklif_degerlendirme", degerlendirme.GetTableName());
        Assert.Equal("teklif_ai_yorumlari", aiYorum.GetTableName());

        var store = StoreObjectIdentifier.Table("teklifler", "public");
        Assert.Equal(
            "olusturulma_tarihi",
            teklif.FindProperty("OlusturulmaTarihi")!.GetColumnName(store));

        var evaluationStore = StoreObjectIdentifier.Table(
            "teklif_degerlendirme",
            "public");
        Assert.Equal(
            "gerekce_kodlari",
            degerlendirme.FindProperty("GerekceKodlari")!
                .GetColumnName(evaluationStore));

        var aiStore = StoreObjectIdentifier.Table(
            "teklif_ai_yorumlari",
            "public");
        Assert.Equal(
            "alert_message",
            aiYorum.FindProperty("AlertMessage")!.GetColumnName(aiStore));
    }
}
