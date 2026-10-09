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

public sealed class StokEnvanterService : IStokEnvanterService
{
    private readonly IStokReadRepository _stokReadRepository;
    private readonly ISatisReadRepository _satisReadRepository;
    private readonly IUrunReadRepository _urunReadRepository;
    private readonly IDepoReadRepository _depoReadRepository;
    private readonly StockMetricsCalculator _stockMetricsCalculator;
    private readonly TimeProvider _timeProvider;

    public StokEnvanterService(
        IStokReadRepository stokReadRepository,
        ISatisReadRepository satisReadRepository,
        IUrunReadRepository urunReadRepository,
        IDepoReadRepository depoReadRepository,
        BusinessRulesOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(stokReadRepository);
        ArgumentNullException.ThrowIfNull(satisReadRepository);
        ArgumentNullException.ThrowIfNull(urunReadRepository);
        ArgumentNullException.ThrowIfNull(depoReadRepository);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _stokReadRepository = stokReadRepository;
        _satisReadRepository = satisReadRepository;
        _urunReadRepository = urunReadRepository;
        _depoReadRepository = depoReadRepository;
        _stockMetricsCalculator = new StockMetricsCalculator(options.StokGunEsigi);
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<StokEnvanterOutput>> ListeleAsync(
        StokEnvanterQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        DateOnly? latestSaleDate = await _satisReadRepository.GetLatestSaleDateAsync(cancellationToken);
        DateOnly bitisTarihi = latestSaleDate ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateTimeOffset hesaplamaTarihi = latestSaleDate.HasValue
            ? new DateTimeOffset(latestSaleDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : _timeProvider.GetUtcNow();

        DateOnly baslangicTarihi = bitisTarihi.AddDays(-89);

        // Parsing multi-value parameters
        HashSet<string>? depoSet = ParseCommaSeparated(query.DepoKodu);
        HashSet<string>? urunSet = ParseCommaSeparated(query.UrunKodu);
        HashSet<string>? bolgeSet = ParseCommaSeparated(query.Bolge);
        HashSet<string>? kategoriSet = ParseCommaSeparated(query.Kategori);
        HashSet<string>? altKategoriSet = ParseCommaSeparated(query.AltKategori);
        HashSet<string>? stokDurumuSet = ParseCommaSeparated(query.StokDurumu);

        string? repoDepo = depoSet?.Count == 1 ? depoSet.First() : null;
        string? repoUrun = urunSet?.Count == 1 ? urunSet.First() : null;

        IReadOnlyList<StokReadModel> allStoklar = await _stokReadRepository.ListAsync(
            repoDepo,
            repoUrun,
            cancellationToken);

        if (allStoklar.Count == 0)
        {
            return ServiceResult<StokEnvanterOutput>.Success(
                new StokEnvanterOutput(
                    HesaplamaTarihi: hesaplamaTarihi,
                    Stoklar: Array.Empty<StokEnvanterItemOutput>(),
                    ToplamKayitSayisi: 0,
                    Sayfa: query.Sayfa,
                    SayfaBoyutu: query.SayfaBoyutu));
        }

        foreach (var stock in allStoklar)
        {
            if (stock is null || !stock.MevcutStok.HasValue)
            {
                return ServiceResult<StokEnvanterOutput>.Fail(
                    ServiceErrorKind.Unavailable,
                    "INVALID_STOCK_DATA",
                    "Stok envanteri hesabı için gerekli stok verisi eksik.");
            }
        }

        IReadOnlyList<SatisReadModel> satislar = await _satisReadRepository.ListByPeriodAsync(
            baslangicTarihi,
            bitisTarihi.AddDays(1),
            cancellationToken);

        foreach (var sale in satislar)
        {
            if (sale is null || !sale.Miktar.HasValue)
            {
                return ServiceResult<StokEnvanterOutput>.Fail(
                    ServiceErrorKind.Unavailable,
                    "INVALID_SALE_DATA",
                    "Stok envanteri hesabı için gerekli satış verisi eksik.");
            }
        }

        var satisGruplari = satislar
            .GroupBy(x => (x.UrunKodu, x.DepoKodu))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.IsIade == true ? -Math.Abs(s.Miktar!.Value) : s.Miktar!.Value));

        var urunKodlari = allStoklar
            .Select(s => s.UrunKodu)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var urunler = await _urunReadRepository.ListByCodesAsync(urunKodlari, cancellationToken);
        var urunByCode = urunler.ToDictionary(u => u.UrunKodu, StringComparer.Ordinal);

        var depolar = await _depoReadRepository.ListAsync(cancellationToken);
        var depoByCode = depolar.ToDictionary(d => d.DepoKodu, StringComparer.Ordinal);

        List<StokEnvanterItemOutput> items = new();

        foreach (var stock in allStoklar)
        {
            // Depo filtreleri
            if (depoSet is not null && !depoSet.Contains(stock.DepoKodu.ToUpperInvariant()))
                continue;

            // Ürün filtreleri
            if (urunSet is not null && !urunSet.Contains(stock.UrunKodu.ToUpperInvariant()))
                continue;

            // Bölge filtreleri (deponun bölgesi)
            depoByCode.TryGetValue(stock.DepoKodu, out var depoInfo);
            if (bolgeSet is not null && (depoInfo?.Bolge is null || !bolgeSet.Contains(depoInfo.Bolge.ToUpperInvariant())))
                continue;

            decimal sonUcAySatisAdedi = satisGruplari.TryGetValue((stock.UrunKodu, stock.DepoKodu), out decimal sAdet)
                ? sAdet
                : 0m;

            StockMetricsResult metrics = _stockMetricsCalculator.Calculate(stock, sonUcAySatisAdedi);
            urunByCode.TryGetValue(stock.UrunKodu, out var urunInfo);

            items.Add(new StokEnvanterItemOutput(
                UrunKodu: stock.UrunKodu,
                DepoKodu: stock.DepoKodu,
                Kategori: urunInfo?.Kategori,
                AltKategori: urunInfo?.AltKategori,
                MevcutStok: metrics.MevcutStok,
                MinStokSeviyesi: metrics.MinStokSeviyesi,
                SonUcAySatisAdedi: metrics.SonUcAySatisAdedi,
                TahminiStokGunu: metrics.TahminiStokGunu,
                KritikMi: metrics.KritikMi,
                GerekceKodlari: metrics.GerekceKodlari,
                UrunAdi: urunInfo?.UrunAdi));
        }

        IEnumerable<StokEnvanterItemOutput> filtered = items;

        if (kategoriSet is not null)
        {
            filtered = filtered.Where(i => i.Kategori != null && kategoriSet.Contains(i.Kategori.ToUpperInvariant()));
        }

        if (altKategoriSet is not null)
        {
            filtered = filtered.Where(i => i.AltKategori != null && altKategoriSet.Contains(i.AltKategori.ToUpperInvariant()));
        }

        if (stokDurumuSet is not null)
        {
            filtered = filtered.Where(i =>
            {
                bool matches = false;
                if (stokDurumuSet.Contains("KRITIK") && i.KritikMi) matches = true;
                if (stokDurumuSet.Contains("TUKENDI") && i.MevcutStok <= 0) matches = true;
                if (stokDurumuSet.Contains("NORMAL") && !i.KritikMi && i.MevcutStok > 0) matches = true;
                return matches;
            });
        }

        if (query.KritikMi.HasValue)
        {
            filtered = filtered.Where(i => i.KritikMi == query.KritikMi.Value);
        }

        if (query.MinMevcutStok.HasValue)
        {
            filtered = filtered.Where(i => i.MevcutStok >= query.MinMevcutStok.Value);
        }

        if (query.MaxMevcutStok.HasValue)
        {
            filtered = filtered.Where(i => i.MevcutStok <= query.MaxMevcutStok.Value);
        }

        if (query.MaxStokGunu.HasValue)
        {
            filtered = filtered.Where(i => i.TahminiStokGunu.HasValue && i.TahminiStokGunu.Value <= query.MaxStokGunu.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Arama))
        {
            var searchTerm = query.Arama.Trim();
            filtered = filtered.Where(i =>
                i.UrunKodu.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(i.UrunAdi) && i.UrunAdi.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                || i.DepoKodu.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(i.Kategori) && i.Kategori.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(i.AltKategori) && i.AltKategori.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filtered.ToList();
        int totalCount = filteredList.Count;

        bool isAsc = string.Equals(query.SiralaYon, "asc", StringComparison.OrdinalIgnoreCase);
        var sortField = query.SiralaAlan?.Trim().ToLowerInvariant();

        IEnumerable<StokEnvanterItemOutput> sorted = sortField switch
        {
            "mevcut_stok" => isAsc
                ? filteredList.OrderBy(x => x.MevcutStok).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.MevcutStok).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "min_stok_seviyesi" => isAsc
                ? filteredList.OrderBy(x => x.MinStokSeviyesi ?? decimal.MaxValue).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.MinStokSeviyesi ?? decimal.MinValue).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "son_uc_ay_satis" => isAsc
                ? filteredList.OrderBy(x => x.SonUcAySatisAdedi).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.SonUcAySatisAdedi).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "tahmini_stok_gunu" => isAsc
                ? filteredList.OrderBy(x => x.TahminiStokGunu ?? decimal.MaxValue).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.TahminiStokGunu ?? decimal.MinValue).ThenBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal),

            "depo_kodu" => isAsc
                ? filteredList.OrderBy(x => x.DepoKodu, StringComparer.Ordinal).ThenBy(x => x.UrunKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.DepoKodu, StringComparer.Ordinal).ThenBy(x => x.UrunKodu, StringComparer.Ordinal),

            _ => isAsc
                ? filteredList.OrderBy(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
                : filteredList.OrderByDescending(x => x.UrunKodu, StringComparer.Ordinal).ThenBy(x => x.DepoKodu, StringComparer.Ordinal)
        };

        var resultList = sorted.ToList();

        if (query.Sayfa.HasValue || query.SayfaBoyutu.HasValue)
        {
            int pageNumber = query.Sayfa.GetValueOrDefault(1);
            if (pageNumber < 1) pageNumber = 1;
            int pageSize = query.SayfaBoyutu.GetValueOrDefault(50);
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            resultList = resultList
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        return ServiceResult<StokEnvanterOutput>.Success(
            new StokEnvanterOutput(
                HesaplamaTarihi: hesaplamaTarihi,
                Stoklar: resultList,
                ToplamKayitSayisi: totalCount,
                Sayfa: query.Sayfa,
                SayfaBoyutu: query.SayfaBoyutu));
    }

    public async Task<ServiceResult<StokOzetOutput>> GetirOzetAsync(
        StokOzetQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        DateOnly? latestSaleDate = await _satisReadRepository.GetLatestSaleDateAsync(cancellationToken);
        DateOnly bitisTarihi = latestSaleDate ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateTimeOffset hesaplamaTarihi = latestSaleDate.HasValue
            ? new DateTimeOffset(latestSaleDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : _timeProvider.GetUtcNow();

        DateOnly baslangicTarihi = bitisTarihi.AddDays(-89);

        IReadOnlyList<StokReadModel> stoklar = await _stokReadRepository.ListAsync(
            query.DepoKodu,
            null,
            cancellationToken);

        if (stoklar.Count == 0)
        {
            return ServiceResult<StokOzetOutput>.Success(
                new StokOzetOutput(
                    HesaplamaTarihi: hesaplamaTarihi,
                    ToplamStokKalemSayisi: 0,
                    ToplamFizikselStokAdedi: 0m,
                    KritikSeviyedekiKalemSayisi: 0,
                    TukenenKalemSayisi: 0,
                    DepoBazliOzetler: Array.Empty<DepoStokOzetOutput>()));
        }

        foreach (var stock in stoklar)
        {
            if (stock is null || !stock.MevcutStok.HasValue)
            {
                return ServiceResult<StokOzetOutput>.Fail(
                    ServiceErrorKind.Unavailable,
                    "INVALID_STOCK_DATA",
                    "Stok özeti hesabı için gerekli stok verisi eksik.");
            }
        }

        IReadOnlyList<SatisReadModel> satislar = await _satisReadRepository.ListByPeriodAsync(
            baslangicTarihi,
            bitisTarihi.AddDays(1),
            cancellationToken);

        foreach (var sale in satislar)
        {
            if (sale is null || !sale.Miktar.HasValue)
            {
                return ServiceResult<StokOzetOutput>.Fail(
                    ServiceErrorKind.Unavailable,
                    "INVALID_SALE_DATA",
                    "Stok özeti hesabı için gerekli satış verisi eksik.");
            }
        }

        var satisGruplari = satislar
            .GroupBy(x => (x.UrunKodu, x.DepoKodu))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.IsIade == true ? -Math.Abs(s.Miktar!.Value) : s.Miktar!.Value));

        var depolar = await _depoReadRepository.ListAsync(cancellationToken);
        var depoByCode = depolar.ToDictionary(d => d.DepoKodu, StringComparer.Ordinal);

        int toplamKalem = stoklar.Count;
        decimal toplamMevcut = 0m;
        int kritikKalem = 0;
        int tukenenKalem = 0;

        var depoMap = new Dictionary<string, (int ToplamKalem, int KritikKalem, decimal ToplamStok)>();

        foreach (var stock in stoklar)
        {
            decimal sonUcAySatisAdedi = satisGruplari.TryGetValue((stock.UrunKodu, stock.DepoKodu), out decimal sAdet)
                ? sAdet
                : 0m;

            StockMetricsResult metrics = _stockMetricsCalculator.Calculate(stock, sonUcAySatisAdedi);

            toplamMevcut += metrics.MevcutStok;
            if (metrics.KritikMi) kritikKalem++;
            if (metrics.MevcutStok <= 0) tukenenKalem++;

            if (!depoMap.TryGetValue(stock.DepoKodu, out var current))
            {
                current = (0, 0, 0m);
            }

            depoMap[stock.DepoKodu] = (
                current.ToplamKalem + 1,
                current.KritikKalem + (metrics.KritikMi ? 1 : 0),
                current.ToplamStok + metrics.MevcutStok);
        }

        var depoOzetler = depoMap
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new DepoStokOzetOutput(
                DepoKodu: kv.Key,
                Bolge: depoByCode.TryGetValue(kv.Key, out var d) ? d.Bolge : null,
                ToplamKalemSayisi: kv.Value.ToplamKalem,
                KritikKalemSayisi: kv.Value.KritikKalem,
                ToplamMevcutStok: kv.Value.ToplamStok))
            .ToList();

        return ServiceResult<StokOzetOutput>.Success(
            new StokOzetOutput(
                HesaplamaTarihi: hesaplamaTarihi,
                ToplamStokKalemSayisi: toplamKalem,
                ToplamFizikselStokAdedi: toplamMevcut,
                KritikSeviyedekiKalemSayisi: kritikKalem,
                TukenenKalemSayisi: tukenenKalem,
                DepoBazliOzetler: depoOzetler));
    }

    private static HashSet<string>? ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var set = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return set.Count > 0 ? set : null;
    }
}
