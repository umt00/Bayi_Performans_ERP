using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BayiPerformansAppWeb.Business.Models;
using BayiPerformansAppWeb.Entities.ReadModels;
using BayiPerformansAppWeb.Entities.ServiceOutputs;

namespace BayiPerformansAppWeb.Business.Abstractions;

public sealed record RiskListesiQuery(
    DateOnly? Donem = null,
    string? Segment = null,
    string? Bolge = null,
    string? Sehir = null,
    string? RiskSeviyesi = null,
    int? MinRiskSkoru = null,
    int? MaxRiskSkoru = null,
    string? GerekceKodu = null,
    decimal? MinHedefeUlasmaPct = null,
    decimal? MaxHedefeUlasmaPct = null,
    decimal? MinMarjPct = null,
    decimal? MaxMarjPct = null,
    decimal? MinIskontoPct = null,
    decimal? MaxIskontoPct = null,
    decimal? MinCiro = null,
    decimal? MaxCiro = null,
    string? AktiviteDurumu = null,
    bool? KritikStokVarMi = null,
    string? Arama = null,
    string? SiralaAlan = null,
    string? SiralaYon = null,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record BayilerOzetQuery(
    DateOnly? Donem = null,
    string? Segment = null,
    string? Bolge = null);

public sealed record BayiGecmisQuery(
    string BayiKodu,
    int AySayisi = 6);

public sealed record BayiSehirDagilimiQuery(
    DateOnly? Donem = null,
    string? Bolge = null,
    string? Segment = null);

public sealed record BayiBolgeDagilimiQuery(
    DateOnly? Donem = null);

public sealed record KritikStokQuery(
    string? DepoKodu = null,
    string? UrunKodu = null,
    string? Kategori = null,
    string? AltKategori = null,
    string? GerekceKodu = null,
    decimal? MaxStokGunu = null,
    string? SiralaAlan = null,
    string? SiralaYon = null,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record StokEnvanterQuery(
    string? DepoKodu = null,
    string? UrunKodu = null,
    string? Bolge = null,
    string? Kategori = null,
    string? AltKategori = null,
    string? StokDurumu = null,
    bool? KritikMi = null,
    decimal? MinMevcutStok = null,
    decimal? MaxMevcutStok = null,
    decimal? MaxStokGunu = null,
    string? Arama = null,
    string? SiralaAlan = null,
    string? SiralaYon = null,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record UrunStokMatrisiQuery(
    string? UrunKodlari = null,
    string? Kategori = null,
    string? AltKategori = null,
    bool? KritikMi = null,
    string? Arama = null,
    int? Sayfa = null,
    int? SayfaBoyutu = null);

public sealed record StokOzetQuery(
    string? DepoKodu = null);

public sealed record TeklifDegerlendirmeCommand(
    string BayiKodu,
    string UrunKodu,
    string DepoKodu,
    decimal Miktar,
    decimal BirimFiyat,
    decimal IskontoOrani);

public interface IBayiPerformansService
{
    Task<ServiceResult<BayiPerformansOutput>> GetirAsync(
        string bayiKodu,
        DateOnly? donem,
        CancellationToken cancellationToken);
}

public interface IRiskListesiService
{
    Task<ServiceResult<RiskListesiOutput>> ListeleAsync(
        RiskListesiQuery query,
        CancellationToken cancellationToken);
}

public interface IBayiOzetService
{
    Task<ServiceResult<BayilerOzetOutput>> GetirOzetAsync(
        BayilerOzetQuery query,
        CancellationToken cancellationToken);
}

public interface IBayiGecmisService
{
    Task<ServiceResult<BayiGecmisPerformansOutput>> GetirGecmisAsync(
        BayiGecmisQuery query,
        CancellationToken cancellationToken);
}

public interface IBayiSehirDagilimService
{
    Task<ServiceResult<BayiSehirDagilimiOutput>> GetirSehirDagilimiAsync(
        BayiSehirDagilimiQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<BayiBolgeDagilimiOutput>> GetirBolgeDagilimiAsync(
        BayiBolgeDagilimiQuery query,
        CancellationToken cancellationToken);
}

public interface IKritikStokService
{
    Task<ServiceResult<KritikStokOutput>> ListeleAsync(
        KritikStokQuery query,
        CancellationToken cancellationToken);
}

public interface IStokEnvanterService
{
    Task<ServiceResult<StokEnvanterOutput>> ListeleAsync(
        StokEnvanterQuery query,
        CancellationToken cancellationToken);

    Task<ServiceResult<StokOzetOutput>> GetirOzetAsync(
        StokOzetQuery query,
        CancellationToken cancellationToken);
}

public interface IUrunStokMatrisiService
{
    Task<ServiceResult<UrunDepoStokMatrisiOutput>> GetirMatrisAsync(
        UrunStokMatrisiQuery query,
        CancellationToken cancellationToken);
}

public interface ITeklifDegerlendirmeService
{
    Task<ServiceResult<TeklifDegerlendirmeOutput>> DegerlendirAsync(
        TeklifDegerlendirmeCommand command,
        CancellationToken cancellationToken);
}

public interface ITanimlarService
{
    Task<ServiceResult<IReadOnlyList<BayiReadModel>>> ListeleBayilerAsync(
        string? segment,
        string? bolge,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<DepoReadModel>>> ListeleDepolarAsync(
        CancellationToken cancellationToken);

    Task<ServiceResult<FiltreSecenekleriOutput>> GetirFiltreSecenekleriAsync(
        CancellationToken cancellationToken);
}
