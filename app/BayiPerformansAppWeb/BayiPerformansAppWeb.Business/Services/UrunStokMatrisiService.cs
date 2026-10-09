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

public sealed class UrunStokMatrisiService : IUrunStokMatrisiService
{
    private readonly IUrunReadRepository _urunRepository;
    private readonly IDepoReadRepository _depoRepository;
    private readonly IStokReadRepository _stokRepository;
    private readonly ISatisReadRepository _satisRepository;
    private readonly StockMetricsCalculator _stockMetricsCalculator;
    private readonly TimeProvider _timeProvider;

    public UrunStokMatrisiService(
        IUrunReadRepository urunRepository,
        IDepoReadRepository depoRepository,
        IStokReadRepository stokRepository,
        ISatisReadRepository satisRepository,
        BusinessRulesOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(urunRepository);
        ArgumentNullException.ThrowIfNull(depoRepository);
        ArgumentNullException.ThrowIfNull(stokRepository);
        ArgumentNullException.ThrowIfNull(satisRepository);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _urunRepository = urunRepository;
        _depoRepository = depoRepository;
        _stokRepository = stokRepository;
        _satisRepository = satisRepository;
        _stockMetricsCalculator = new StockMetricsCalculator(options.StokGunEsigi);
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<UrunDepoStokMatrisiOutput>> GetirMatrisAsync(
        UrunStokMatrisiQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        DateOnly? latestSaleDate = await _satisRepository.GetLatestSaleDateAsync(cancellationToken);
        DateOnly bitisTarihi = latestSaleDate ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        DateTimeOffset hesaplamaTarihi = latestSaleDate.HasValue
            ? new DateTimeOffset(latestSaleDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : _timeProvider.GetUtcNow();

        DateOnly baslangicTarihi = bitisTarihi.AddDays(-89);

        IReadOnlyList<UrunReadModel> allUrunler = await _urunRepository.ListAllAsync(cancellationToken);
        IReadOnlyList<DepoReadModel> allDepolar = await _depoRepository.ListAsync(cancellationToken);
        IReadOnlyList<StokReadModel> allStoklar = await _stokRepository.ListAsync(null, null, cancellationToken);

        IReadOnlyList<SatisReadModel> satislar = await _satisRepository.ListByPeriodAsync(
            baslangicTarihi,
            bitisTarihi.AddDays(1),
            cancellationToken);

        var satisGruplari = satislar
            .GroupBy(x => (x.UrunKodu, x.DepoKodu))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(s => s.IsIade == true ? -Math.Abs(s.Miktar ?? 0m) : (s.Miktar ?? 0m)));

        var stokMap = allStoklar
            .GroupBy(s => (s.UrunKodu, s.DepoKodu))
            .ToDictionary(g => g.Key, g => g.First());

        var depoTanimlari = allDepolar
            .OrderBy(d => d.DepoKodu, StringComparer.Ordinal)
            .Select(d => new DepoTanimOutput(d.DepoKodu, d.Bolge, d.DepoAdi, d.Sehir))
            .ToList();

        // 1. Ürün bazında matris satırlarını oluştur
        List<UrunDepoMatrisItemOutput> matrisItems = new();

        foreach (var urun in allUrunler)
        {
            decimal toplamMevcut = 0m;
            decimal toplamSatis = 0m;
            int kritikDepoSayisi = 0;

            List<UrunDepoDetayItemOutput> depoDetaylari = new();

            foreach (var depo in allDepolar)
            {
                stokMap.TryGetValue((urun.UrunKodu, depo.DepoKodu), out var stock);
                decimal satisAdedi = satisGruplari.TryGetValue((urun.UrunKodu, depo.DepoKodu), out decimal s) ? s : 0m;

                StockMetricsResult? metrics = stock is not null
                    ? _stockMetricsCalculator.Calculate(stock, satisAdedi)
                    : null;

                decimal mevcutStok = metrics?.MevcutStok ?? 0m;
                decimal? minStok = metrics?.MinStokSeviyesi;
                decimal? tahminiGun = metrics?.TahminiStokGunu;
                bool kritikMi = metrics?.KritikMi ?? false;

                toplamMevcut += mevcutStok;
                toplamSatis += satisAdedi;
                if (kritikMi) kritikDepoSayisi++;

                depoDetaylari.Add(new UrunDepoDetayItemOutput(
                    DepoKodu: depo.DepoKodu,
                    DepoAdi: depo.DepoAdi,
                    Sehir: depo.Sehir,
                    Bolge: depo.Bolge,
                    MevcutStok: mevcutStok,
                    MinStokSeviyesi: minStok,
                    SonUcAySatisAdedi: satisAdedi,
                    TahminiStokGunu: tahminiGun,
                    KritikMi: kritikMi));
            }

            matrisItems.Add(new UrunDepoMatrisItemOutput(
                UrunKodu: urun.UrunKodu,
                UrunAdi: urun.UrunAdi,
                Kategori: urun.Kategori,
                AltKategori: urun.AltKategori,
                ToplamMevcutStok: toplamMevcut,
                ToplamSonUcAySatisAdedi: toplamSatis,
                KritikDepoSayisi: kritikDepoSayisi,
                DepoStoklari: depoDetaylari.OrderBy(d => d.DepoKodu, StringComparer.Ordinal).ToList()));
        }

        // 2. Filtreleme
        IEnumerable<UrunDepoMatrisItemOutput> filtered = matrisItems;

        if (!string.IsNullOrWhiteSpace(query.UrunKodlari))
        {
            var codeSet = query.UrunKodlari
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (codeSet.Count > 0)
            {
                filtered = filtered.Where(item => codeSet.Contains(item.UrunKodu.ToUpperInvariant()));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Kategori))
        {
            var targetCat = query.Kategori.Trim();
            filtered = filtered.Where(item => item.Kategori != null && string.Equals(item.Kategori, targetCat, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.AltKategori))
        {
            var targetSub = query.AltKategori.Trim();
            filtered = filtered.Where(item => item.AltKategori != null && string.Equals(item.AltKategori, targetSub, StringComparison.OrdinalIgnoreCase));
        }

        if (query.KritikMi.HasValue)
        {
            filtered = query.KritikMi.Value
                ? filtered.Where(item => item.KritikDepoSayisi > 0)
                : filtered.Where(item => item.KritikDepoSayisi == 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Arama))
        {
            var term = query.Arama.Trim();
            filtered = filtered.Where(item =>
                item.UrunKodu.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(item.UrunAdi) && item.UrunAdi.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(item.Kategori) && item.Kategori.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(item.AltKategori) && item.AltKategori.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = filtered.OrderBy(i => i.UrunKodu, StringComparer.Ordinal).ToList();
        int totalCount = filteredList.Count;

        // 3. Sayfalama
        var resultList = filteredList;
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

        return ServiceResult<UrunDepoStokMatrisiOutput>.Success(
            new UrunDepoStokMatrisiOutput(
                HesaplamaTarihi: hesaplamaTarihi,
                ToplamUrunSayisi: totalCount,
                Urunler: resultList,
                Depolar: depoTanimlari,
                Sayfa: query.Sayfa,
                SayfaBoyutu: query.SayfaBoyutu));
    }
}
