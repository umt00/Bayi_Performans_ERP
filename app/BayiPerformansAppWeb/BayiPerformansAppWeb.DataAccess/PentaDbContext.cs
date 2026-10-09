using BayiPerformansAppWeb.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BayiPerformansAppWeb.DataAccess;

public sealed class PentaDbContext(DbContextOptions<PentaDbContext> options)
    : DbContext(options)
{
    internal DbSet<BayiRow> Bayiler => Set<BayiRow>();
    internal DbSet<DepoRow> Depolar => Set<DepoRow>();
    internal DbSet<HedefRow> Hedefler => Set<HedefRow>();
    internal DbSet<SatisRow> Satislar => Set<SatisRow>();
    internal DbSet<StokRow> Stoklar => Set<StokRow>();
    internal DbSet<UrunRow> Urunler => Set<UrunRow>();
    internal DbSet<TeklifRow> Teklifler => Set<TeklifRow>();
    internal DbSet<TeklifKalemRow> TeklifKalemleri => Set<TeklifKalemRow>();
    internal DbSet<TeklifDegerlendirmeRow> TeklifDegerlendirmeleri =>
        Set<TeklifDegerlendirmeRow>();
    internal DbSet<TeklifAiYorumRow> TeklifAiYorumlari => Set<TeklifAiYorumRow>();
    internal DbSet<BayiDonemPerformansRow> BayiDonemPerformanslari =>
        Set<BayiDonemPerformansRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PentaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
