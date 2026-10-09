using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Abstractions;
using BayiPerformansAppWeb.Business.Calculations;
using BayiPerformansAppWeb.Business.Configuration;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.DataAccess.Abstractions;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Services;

/// <summary>
/// Kritik stok listesini oluşturan Business servisidir.
///
/// /stok/kritik endpoint'i bayi bazlı değildir.
/// Stok tablosundaki ürün/depo kayıtları üzerinden çalışır.
///
/// Son 90 günlük satışlar yalnızca ürün + depo bazında
/// satış hızını hesaplamak için kullanılır.
/// </summary>
public sealed class KritikStokService : IKritikStokService
{
    private readonly IStokReadRepository _stokReadRepository;
    private readonly ISatisReadRepository _satisReadRepository;
    private readonly IUrunReadRepository? _urunReadRepository;
    private readonly StockMetricsCalculator _stockMetricsCalculator;
    private readonly TimeProvider _timeProvider;

    public KritikStokService(
        IStokReadRepository stokReadRepository,
        ISatisReadRepository satisReadRepository,
        BusinessRulesOptions options,
        TimeProvider timeProvider,
        IUrunReadRepository? urunReadRepository = null)
    {
        ArgumentNullException.ThrowIfNull(stokReadRepository);
        ArgumentNullException.ThrowIfNull(satisReadRepository);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _stokReadRepository = stokReadRepository;
        _satisReadRepository = satisReadRepository;
        _urunReadRepository = urunReadRepository;

        _stockMetricsCalculator =
            new StockMetricsCalculator(options.StokGunEsigi);

        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<KritikStokOutput>> ListeleAsync(
        KritikStokQuery query,
        CancellationToken cancellationToken)
    {
        // --------------------------------------------------
        // NULL QUERY KONTROLÜ
        // --------------------------------------------------
        if (query is null)
        {
            return ServiceResult<KritikStokOutput>.Fail(
                ServiceErrorKind.Validation,
                "QUERY_NULL",
                "Kritik stok sorgu parametresi null olamaz.");
        }

        DateOnly? latestSaleDate = await _satisReadRepository.GetLatestSaleDateAsync(cancellationToken);
        DateOnly bitisTarihi = latestSaleDate ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateTimeOffset hesaplamaTarihi = latestSaleDate.HasValue
            ? new DateTimeOffset(latestSaleDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : _timeProvider.GetUtcNow();

        // 90 günlük periyot: [Bitiş - 89, Bitiş + 1)
        DateOnly baslangicTarihi = bitisTarihi.AddDays(-89);

        // --------------------------------------------------
        // 1. STOK KAYITLARINI GETİR VE VERİ BÜTÜNLÜĞÜNÜ DOĞRULA
        // --------------------------------------------------
        IReadOnlyList<StokReadModel> stoklar =
            await _stokReadRepository.ListAsync(
                query.DepoKodu,
                query.UrunKodu,
                cancellationToken);

        if (stoklar.Count == 0)
        {
            return ServiceResult<KritikStokOutput>.Success(
                new KritikStokOutput(
                    HesaplamaTarihi: hesaplamaTarihi,
                    Urunler: Array.Empty<KritikStokItemOutput>(),
                    ToplamKayitSayisi: 0,
                    Sayfa: query.Sayfa,
                    SayfaBoyutu: query.SayfaBoyutu));
        }

        // MevcutStok null olan bozuk veri kontrolü (Gizlice 0'a çevrilmez)
        foreach (StokReadModel stock in stoklar)
        {
            if (stock is null || !stock.MevcutStok.HasValue)
            {
                return ServiceResult<KritikStokOutput>.Fail(
                    ServiceErrorKind.Unavailable,
                    "INVALID_STOCK_DATA",
                    "Kritik stok hesabı için gerekli stok verisi eksik.");
            }
        }

        // --------------------------------------------------
        // 2. SON 90 GÜNLÜK SATIŞLARI GETİR (Toplu Sorgu - N+1 Engellendi)
        // --------------------------------------------------
        IReadOnlyList<SatisReadModel> satislar =
            await _satisReadRepository.ListByPeriodAsync(
                baslangicTarihi,
                bitisTarihi.AddDays(1),
                cancellationToken);

        // Satış Miktarı null olan bozuk veri kontrolü (Gizlice 0'a çevrilmez)
        foreach (SatisReadModel sale in satislar)
        {
            if (sale is null || !sale.Miktar.HasValue)
            {
                return ServiceResult<KritikStokOutput>.Fail(
                    ServiceErrorKind.Unavailable,
                    "INVALID_SALE_DATA",
                    "Kritik stok hesabı için gerekli satış verisi eksik.");
            }
        }

        // --------------------------------------------------
        // 3. ÜRÜN BİLGİLERİNİ GETİR (Kategori / Alt Kategori için)
        // --------------------------------------------------
        IReadOnlyList<UrunReadModel> urunler = _urunReadRepository is not null
            ? await _urunReadRepository.ListAllAsync(cancellationToken)
            : Array.Empty<UrunReadModel>();

        Dictionary<string, UrunReadModel> urunMap = urunler
            .Where(u => !string.IsNullOrWhiteSpace(u.UrunKodu))
            .GroupBy(u => u.UrunKodu, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // --------------------------------------------------
        // 4. SATIŞ HIZLARINI HESAPLA
        // --------------------------------------------------
        Dictionary<(string UrunKodu, string DepoKodu), decimal> satisMiktarlari = satislar
            .GroupBy(sale => (sale.UrunKodu, sale.DepoKodu))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(CalculateSaleQuantity));

        // --------------------------------------------------
        // 5. KRİTİK STOK LİSTESİNİ OLUŞTUR
        // --------------------------------------------------
        List<KritikStokItemOutput> items = new();

        foreach (StokReadModel stock in stoklar)
        {
            (string UrunKodu, string DepoKodu) key = (stock.UrunKodu, stock.DepoKodu);

            satisMiktarlari.TryGetValue(key, out decimal sonUcAySatisAdedi);

            StockMetricsResult metrics =
                _stockMetricsCalculator.Calculate(stock, sonUcAySatisAdedi);

            if (metrics.KritikMi)
            {
                urunMap.TryGetValue(stock.UrunKodu, out var urun);

                items.Add(new KritikStokItemOutput(
                    UrunKodu: stock.UrunKodu,
                    DepoKodu: stock.DepoKodu,
                    MevcutStok: metrics.MevcutStok,
                    MinStokSeviyesi: metrics.MinStokSeviyesi,
                    SonUcAySatisAdedi: metrics.SonUcAySatisAdedi,
                    OrtalamaGunlukSatis: metrics.OrtalamaGunlukSatis,
                    TahminiStokGunu: metrics.TahminiStokGunu,
                    GerekceKodlari: metrics.GerekceKodlari,
                    Kategori: urun?.Kategori,
                    AltKategori: urun?.AltKategori));
            }
        }

        // --------------------------------------------------
        // 6. FİLTRELEME
        // --------------------------------------------------
        IEnumerable<KritikStokItemOutput> filteredQuery = items;

        if (!string.IsNullOrWhiteSpace(query.Kategori))
        {
            filteredQuery = filteredQuery.Where(x =>
                string.Equals(x.Kategori, query.Kategori.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.AltKategori))
        {
            filteredQuery = filteredQuery.Where(x =>
                string.Equals(x.AltKategori, query.AltKategori.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.GerekceKodu))
        {
            filteredQuery = filteredQuery.Where(x =>
                x.GerekceKodlari.Any(g => string.Equals(g, query.GerekceKodu.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        if (query.MaxStokGunu.HasValue)
        {
            filteredQuery = filteredQuery.Where(x =>
                x.TahminiStokGunu.HasValue && x.TahminiStokGunu.Value <= query.MaxStokGunu.Value);
        }

        var filteredList = filteredQuery.ToList();
        int totalFilteredCount = filteredList.Count;

        // --------------------------------------------------
        // 7. SIRALAMA (SORTING)
        // --------------------------------------------------
        bool isAscending = string.Equals(query.SiralaYon, "asc", StringComparison.OrdinalIgnoreCase);
        var sortField = query.SiralaAlan?.Trim().ToLowerInvariant();

        IEnumerable<KritikStokItemOutput> sortedList = sortField switch
        {
            "tahmini_stok_gunu" => isAscending
                ? filteredList.OrderBy(x => x.TahminiStokGunu ?? decimal.MaxValue).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.TahminiStokGunu ?? decimal.MinValue).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "mevcut_stok" => isAscending
                ? filteredList.OrderBy(x => x.MevcutStok).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.MevcutStok).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "son_uc_ay_satis_adedi" => isAscending
                ? filteredList.OrderBy(x => x.SonUcAySatisAdedi).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.SonUcAySatisAdedi).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "depo_kodu" => isAscending
                ? filteredList.OrderBy(x => x.DepoKodu, StringComparer.Ordinal).ThenBy(x => x.UrunKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.DepoKodu, StringComparer.Ordinal).ThenBy(x => x.UrunKodu, StringComparer.Ordinal),

            _ => isAscending
                ? filteredList.OrderBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
        };

        // --------------------------------------------------
        // 8. SAYFALAMA (PAGINATION)
        // --------------------------------------------------
        var resultItems = sortedList.ToList();

        if (query.Sayfa.HasValue || query.SayfaBoyutu.HasValue)
        {
            int pageNumber = query.Sayfa.GetValueOrDefault(1);
            if (pageNumber < 1) pageNumber = 1;
            int pageSize = query.SayfaBoyutu.GetValueOrDefault(50);
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            resultItems = resultItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        // --------------------------------------------------
        // 9. SUCCESS OUTPUT DÖNDÜR
        // --------------------------------------------------
        return ServiceResult<KritikStokOutput>.Success(
            new KritikStokOutput(
                HesaplamaTarihi: hesaplamaTarihi,
                Urunler: resultItems,
                ToplamKayitSayisi: totalFilteredCount,
                Sayfa: query.Sayfa,
                SayfaBoyutu: query.SayfaBoyutu));
    }

    private static decimal CalculateSaleQuantity(SatisReadModel sale)
    {
        decimal miktar = sale.Miktar!.Value;

        if (sale.IsIade == true)
        {
            return -Math.Abs(miktar);
        }

        return miktar;
    }
}
