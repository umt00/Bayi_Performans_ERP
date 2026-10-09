using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BayiPerformansAppWeb.DataAccess.Persistence;

internal sealed class BayiRowConfiguration : IEntityTypeConfiguration<BayiRow>
{
    public void Configure(EntityTypeBuilder<BayiRow> builder)
    {
        builder.HasKey(row => row.BayiKodu);
        builder.ToTable("bayiler", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.BayiKodu).HasColumnName("BayiKodu").HasColumnType("character varying");
        builder.Property(row => row.BayiAdi).HasColumnName("BayiAdi").HasColumnType("character varying");
        builder.Property(row => row.Sehir).HasColumnName("Sehir").HasColumnType("character varying");
        builder.Property(row => row.Bolge).HasColumnName("Bolge").HasColumnType("character varying");
        builder.Property(row => row.Segment).HasColumnName("Segment").HasColumnType("character varying");
    }
}

internal sealed class DepoRowConfiguration : IEntityTypeConfiguration<DepoRow>
{
    public void Configure(EntityTypeBuilder<DepoRow> builder)
    {
        builder.HasKey(row => row.DepoKodu);
        builder.ToTable("depolar", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.DepoKodu).HasColumnName("DepoKodu").HasColumnType("character varying");
        builder.Property(row => row.DepoAdi).HasColumnName("DepoAdi").HasColumnType("character varying");
        builder.Property(row => row.Sehir).HasColumnName("Sehir").HasColumnType("character varying");
        builder.Property(row => row.Bolge).HasColumnName("Bolge").HasColumnType("character varying");
    }
}

internal sealed class HedefRowConfiguration : IEntityTypeConfiguration<HedefRow>
{
    public void Configure(EntityTypeBuilder<HedefRow> builder)
    {
        builder.HasKey(row => new { row.BayiKodu, row.Yil, row.Ay });
        builder.ToTable("hedefler", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.BayiKodu).HasColumnName("BayiKodu").HasColumnType("character varying");
        builder.Property(row => row.Yil).HasColumnName("Yil").HasColumnType("integer");
        builder.Property(row => row.Ay).HasColumnName("Ay").HasColumnType("integer");
        builder.Property(row => row.Hedef).HasColumnName("Hedef").HasColumnType("numeric");
    }
}

internal sealed class SatisRowConfiguration : IEntityTypeConfiguration<SatisRow>
{
    public void Configure(EntityTypeBuilder<SatisRow> builder)
    {
        builder.HasKey(row => row.SatisId);
        builder.ToTable("satis", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.SatisId).HasColumnName("SatisID").HasColumnType("character varying");
        builder.Property(row => row.Tarih).HasColumnName("Tarih").HasColumnType("date");
        builder.Property(row => row.BayiKodu).HasColumnName("BayiKodu").HasColumnType("character varying");
        builder.Property(row => row.UrunKodu).HasColumnName("UrunKodu").HasColumnType("character varying");
        builder.Property(row => row.DepoKodu).HasColumnName("DepoKodu").HasColumnType("character varying");
        builder.Property(row => row.Miktar).HasColumnName("Miktar").HasColumnType("numeric");
        builder.Property(row => row.BirimFiyat).HasColumnName("BirimFiyat").HasColumnType("numeric");
        builder.Property(row => row.IskontoOrani).HasColumnName("IskontoOrani").HasColumnType("numeric");
        builder.Property(row => row.SatisKanali).HasColumnName("SatisKanali").HasColumnType("character varying");
        builder.Property(row => row.IsIade).HasColumnName("IsIade").HasColumnType("boolean");
    }
}

internal sealed class StokRowConfiguration : IEntityTypeConfiguration<StokRow>
{
    public void Configure(EntityTypeBuilder<StokRow> builder)
    {
        builder.HasKey(row => new { row.UrunKodu, row.DepoKodu });
        builder.ToTable("stok", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.UrunKodu).HasColumnName("UrunKodu").HasColumnType("character varying");
        builder.Property(row => row.DepoKodu).HasColumnName("DepoKodu").HasColumnType("character varying");
        builder.Property(row => row.MevcutStok).HasColumnName("MevcutStok").HasColumnType("numeric");
        builder.Property(row => row.MinStokSeviyesi).HasColumnName("MinStokSeviyesi").HasColumnType("numeric");
        builder.Property(row => row.SonSayimTarihi).HasColumnName("SonSayimTarihi").HasColumnType("date");
    }
}

internal sealed class UrunRowConfiguration : IEntityTypeConfiguration<UrunRow>
{
    public void Configure(EntityTypeBuilder<UrunRow> builder)
    {
        builder.HasKey(row => row.UrunKodu);
        builder.ToTable("urunler", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.UrunKodu).HasColumnName("UrunKodu").HasColumnType("character varying");
        builder.Property(row => row.UrunAdi).HasColumnName("UrunAdi").HasColumnType("character varying");
        builder.Property(row => row.Kategori).HasColumnName("Kategori").HasColumnType("character varying");
        builder.Property(row => row.AltKategori).HasColumnName("AltKategori").HasColumnType("character varying");
        builder.Property(row => row.Marka).HasColumnName("Marka").HasColumnType("character varying");
        builder.Property(row => row.BirimMaliyet).HasColumnName("BirimMaliyet").HasColumnType("numeric");
    }
}

internal sealed class TeklifRowConfiguration : IEntityTypeConfiguration<TeklifRow>
{
    public void Configure(EntityTypeBuilder<TeklifRow> builder)
    {
        builder.HasKey(row => row.TeklifId);
        builder.ToTable("teklifler", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.TeklifId).HasColumnName("teklif_id").HasMaxLength(40);
        builder.Property(row => row.BayiKodu).HasColumnName("bayi_kodu").HasMaxLength(100);
        builder.Property(row => row.Aciklama).HasColumnName("aciklama").HasMaxLength(1000);
        builder.Property(row => row.Durum).HasColumnName("durum").HasMaxLength(30);
        builder.Property(row => row.ToplamNetTutar).HasColumnName("toplam_net_tutar").HasColumnType("numeric(18,2)");
        builder.Property(row => row.OlusturulmaTarihi).HasColumnName("olusturulma_tarihi").HasColumnType("timestamp with time zone");

        builder.HasMany(row => row.Kalemler)
            .WithOne(row => row.Teklif)
            .HasForeignKey(row => row.TeklifId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TeklifKalemRowConfiguration : IEntityTypeConfiguration<TeklifKalemRow>
{
    public void Configure(EntityTypeBuilder<TeklifKalemRow> builder)
    {
        builder.HasKey(row => new { row.TeklifId, row.SiraNo });
        builder.ToTable("teklif_kalemleri", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.TeklifId).HasColumnName("teklif_id").HasMaxLength(40);
        builder.Property(row => row.SiraNo).HasColumnName("sira_no");
        builder.Property(row => row.UrunKodu).HasColumnName("urun_kodu").HasMaxLength(100);
        builder.Property(row => row.DepoKodu).HasColumnName("depo_kodu").HasMaxLength(100);
        builder.Property(row => row.Miktar).HasColumnName("miktar").HasColumnType("numeric(18,4)");
        builder.Property(row => row.BirimFiyat).HasColumnName("birim_fiyat").HasColumnType("numeric(18,4)");
        builder.Property(row => row.IskontoOrani).HasColumnName("iskonto_orani").HasColumnType("numeric(9,6)");
        builder.Property(row => row.NetTutar).HasColumnName("net_tutar").HasColumnType("numeric(18,2)");
    }
}

internal sealed class TeklifDegerlendirmeRowConfiguration
    : IEntityTypeConfiguration<TeklifDegerlendirmeRow>
{
    public void Configure(EntityTypeBuilder<TeklifDegerlendirmeRow> builder)
    {
        builder.HasKey(row => row.TeklifId);
        builder.ToTable("teklif_degerlendirme", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.TeklifId).HasColumnName("teklif_id").HasMaxLength(40);
        builder.Property(row => row.BayiKodu).HasColumnName("bayi_kodu").HasMaxLength(100);
        builder.Property(row => row.Karar).HasColumnName("karar").HasMaxLength(30);
        builder.Property(row => row.RiskSkoru).HasColumnName("risk_skoru");
        builder.Property(row => row.RiskSeviyesi).HasColumnName("risk_seviyesi").HasMaxLength(20);
        builder.Property(row => row.GerekceKodlari).HasColumnName("gerekce_kodlari").HasColumnType("text[]");
        builder.Property(row => row.BayiMarji).HasColumnName("bayi_marji").HasColumnType("numeric(9,2)");
        builder.Property(row => row.HedefTutturmaYuzdesi).HasColumnName("hedef_tutturma_yuzdesi").HasColumnType("numeric(9,2)");
        builder.Property(row => row.OrtalamaIskontoOrani).HasColumnName("ortalama_iskonto_orani").HasColumnType("numeric(9,2)");
        builder.Property(row => row.IadeOrani).HasColumnName("iade_orani").HasColumnType("numeric(9,2)");
        builder.Property(row => row.DegerlendirmeTarihi).HasColumnName("degerlendirme_tarihi").HasColumnType("timestamp with time zone");

        builder.HasOne(row => row.Teklif)
            .WithOne(row => row.Degerlendirme)
            .HasForeignKey<TeklifDegerlendirmeRow>(row => row.TeklifId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TeklifAiYorumRowConfiguration
    : IEntityTypeConfiguration<TeklifAiYorumRow>
{
    public void Configure(EntityTypeBuilder<TeklifAiYorumRow> builder)
    {
        builder.HasKey(row => row.TeklifId);
        builder.ToTable("teklif_ai_yorumlari", "public", table => table.ExcludeFromMigrations());
        builder.Property(row => row.TeklifId).HasColumnName("teklif_id").HasMaxLength(40);
        builder.Property(row => row.AiComment).HasColumnName("ai_comment");
        builder.Property(row => row.AlertMessage).HasColumnName("alert_message");
        builder.Property(row => row.IsFallback).HasColumnName("is_fallback");
        builder.Property(row => row.CreatedTime).HasColumnName("created_time").HasColumnType("timestamp with time zone");

        builder.HasOne(row => row.Teklif)
            .WithOne(row => row.AiYorum)
            .HasForeignKey<TeklifAiYorumRow>(row => row.TeklifId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BayiDonemPerformansRowConfiguration
    : IEntityTypeConfiguration<BayiDonemPerformansRow>
{
    public void Configure(EntityTypeBuilder<BayiDonemPerformansRow> builder)
    {
        // Data ekibinin kesinleştirdiği aylık karne sözleşmesinde doğal anahtar
        // bayi+dönemdir; aynı ayın kaydı ETL tarafından güncellenir.
        builder.HasKey(row => new { row.BayiKodu, row.Donem });
        builder.ToTable(
            "bayi_donem_performans",
            "public",
            table => table.ExcludeFromMigrations());

        builder.Property(row => row.BayiKodu).HasColumnName("bayi_kodu");
        builder.Property(row => row.Donem).HasColumnName("donem").HasColumnType("date");
        builder.Property(row => row.Ciro).HasColumnName("ciro").HasColumnType("numeric(18,2)");
        builder.Property(row => row.Hedef).HasColumnName("hedef").HasColumnType("numeric(18,2)");
        builder.Property(row => row.HedefTutmaOrani).HasColumnName("hedef_tutma_orani").HasColumnType("numeric(9,2)");
        builder.Property(row => row.OrtalamaKarMarji).HasColumnName("ortalama_kar_marji").HasColumnType("numeric(9,2)");
        builder.Property(row => row.OrtalamaIskontoOrani).HasColumnName("ortalama_iskonto_orani").HasColumnType("numeric(9,2)");
        builder.Property(row => row.OrtalamaIadeOrani).HasColumnName("ortalama_iade_orani").HasColumnType("numeric(9,2)");
        builder.Property(row => row.KritikStokKalanUrunSayisi).HasColumnName("kritik_stok_kalan_urun_sayisi");
        builder.Property(row => row.RiskSkoru).HasColumnName("risk_skoru");
        builder.Property(row => row.RiskSeviyesi).HasColumnName("risk_seviyesi");
        builder.Property(row => row.GerekceKodlari).HasColumnName("gerekce_kodlari").HasColumnType("text[]");
    }
}
