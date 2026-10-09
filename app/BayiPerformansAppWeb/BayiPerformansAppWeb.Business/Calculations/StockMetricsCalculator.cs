using System;
using System.Collections.Generic;
using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.Business.Calculations;

/// <summary>
/// Stok seviyesini satış hızına göre analiz eden Business sınıfıdır.
/// </summary>
public sealed class StockMetricsCalculator
{
    private readonly decimal _stokGunEsigi;

    public StockMetricsCalculator(decimal stokGunEsigi)
    {
        if (stokGunEsigi < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stokGunEsigi),
                "Stok gün eşiği negatif olamaz.");
        }

        _stokGunEsigi = stokGunEsigi;
    }

    public decimal? CalculateAverageDailySales(decimal sonUcAySatisAdedi)
    {
        if (sonUcAySatisAdedi <= 0)
        {
            return null;
        }

        return sonUcAySatisAdedi / 90m;
    }

    public decimal? CalculateEstimatedStockDays(
        decimal mevcutStok,
        decimal? ortalamaGunlukSatis)
    {
        if (mevcutStok < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mevcutStok),
                "Mevcut stok negatif olamaz.");
        }

        if (!ortalamaGunlukSatis.HasValue || ortalamaGunlukSatis.Value <= 0)
        {
            return null;
        }

        return mevcutStok / ortalamaGunlukSatis.Value;
    }

    public bool IsCritical(decimal? tahminiStokGunu)
    {
        if (!tahminiStokGunu.HasValue)
        {
            return false;
        }

        return tahminiStokGunu.Value < _stokGunEsigi;
    }

    public StockMetricsResult Calculate(
        StokReadModel stock,
        decimal sonUcAySatisAdedi)
    {
        ArgumentNullException.ThrowIfNull(stock);

        if (!stock.MevcutStok.HasValue)
        {
            throw new InvalidOperationException(
                $"Stok kaydında MevcutStok bulunamadı. " +
                $"UrunKodu={stock.UrunKodu}, " +
                $"DepoKodu={stock.DepoKodu}");
        }

        decimal mevcutStok = stock.MevcutStok.Value;
        decimal? minStok = stock.MinStokSeviyesi;

        decimal? ortalamaGunlukSatis = CalculateAverageDailySales(sonUcAySatisAdedi);
        decimal? tahminiStokGunu = CalculateEstimatedStockDays(mevcutStok, ortalamaGunlukSatis);

        bool satisHizinaGoreKritik = IsCritical(tahminiStokGunu);
        bool minStokAltinda = minStok.HasValue && mevcutStok < minStok.Value;

        bool kritikMi = satisHizinaGoreKritik || minStokAltinda;

        List<string> gerekceKodlari = new();

        if (kritikMi)
        {
            gerekceKodlari.Add("STOK_YETERSIZ");
        }

        return new StockMetricsResult(
            UrunKodu: stock.UrunKodu,
            DepoKodu: stock.DepoKodu,
            MevcutStok: mevcutStok,
            MinStokSeviyesi: minStok,
            SonUcAySatisAdedi: sonUcAySatisAdedi,
            OrtalamaGunlukSatis: ortalamaGunlukSatis,
            TahminiStokGunu: tahminiStokGunu,
            KritikMi: kritikMi,
            GerekceKodlari: gerekceKodlari);
    }
}

public sealed record StockMetricsResult(
    string UrunKodu,
    string DepoKodu,
    decimal MevcutStok,
    decimal? MinStokSeviyesi,
    decimal SonUcAySatisAdedi,
    decimal? OrtalamaGunlukSatis,
    decimal? TahminiStokGunu,
    bool KritikMi,
    IReadOnlyList<string> GerekceKodlari);
