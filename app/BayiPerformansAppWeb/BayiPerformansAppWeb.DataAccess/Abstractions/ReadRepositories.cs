using BayiPerformansAppWeb.Entities.ReadModels;

namespace BayiPerformansAppWeb.DataAccess.Abstractions;

public interface IBayiReadRepository
{
    Task<BayiReadModel?> GetByCodeAsync(
        string bayiKodu,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BayiReadModel>> ListAsync(
        string? segment,
        string? bolge,
        CancellationToken cancellationToken);
}

public interface IDepoReadRepository
{
    Task<DepoReadModel?> GetByCodeAsync(
        string depoKodu,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DepoReadModel>> ListAsync(
        CancellationToken cancellationToken);
}

public interface ISatisReadRepository
{
    Task<DateOnly?> GetLatestSaleDateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DateOnly>> ListDistinctPeriodsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SatisReadModel>> ListByPeriodAsync(
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SatisReadModel>> ListByDealerAndPeriodAsync(
        string bayiKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SatisReadModel>> ListByProductWarehouseAndPeriodAsync(
        string urunKodu,
        string depoKodu,
        DateOnly periodStart,
        DateOnly periodEndExclusive,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesByPeriodAsync(
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<(string UrunKodu, string DepoKodu), decimal>>
        GetTotalSalesQuantitiesForDealerAsync(
            string bayiKodu,
            DateOnly periodStart,
            DateOnly periodEndExclusive,
            CancellationToken cancellationToken);
}

public interface IUrunReadRepository
{
    Task<UrunReadModel?> GetByCodeAsync(
        string urunKodu,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UrunReadModel>> ListByCodesAsync(
        IReadOnlyCollection<string> urunKodlari,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UrunReadModel>> ListAllAsync(
        CancellationToken cancellationToken);
}

public interface IStokReadRepository
{
    Task<IReadOnlyList<StokReadModel>> ListAsync(
        string? depoKodu,
        string? urunKodu,
        CancellationToken cancellationToken);

    Task<StokReadModel?> GetAsync(
        string urunKodu,
        string depoKodu,
        CancellationToken cancellationToken);
}

public interface IAylikHedefReadRepository
{
    Task<AylikHedefReadModel?> GetAsync(
        string bayiKodu,
        int yil,
        int ay,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AylikHedefReadModel>> ListByPeriodAsync(
        int yil,
        int ay,
        CancellationToken cancellationToken);
}

/// <summary>
/// Data/Automation tarafından hazırlanmış bayi_donem_performans snapshot
/// tablosunu okur. Metrik veya karar hesaplaması yapmaz.
/// </summary>
public interface IBayiDonemPerformansReadRepository
{
    Task<BayiDonemPerformansPage> ListAsync(
        BayiDonemPerformansQuery query,
        CancellationToken cancellationToken);

    Task<BayiDonemPerformansReadModel?> GetAsync(
        string bayiKodu,
        DateOnly? donem,
        CancellationToken cancellationToken);

    Task<PerformansDashboardStatsReadModel> GetDashboardStatsAsync(
        DateOnly? donem,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BayiDonemPerformansReadModel>> GetHistoryAsync(
        string bayiKodu,
        int limit,
        CancellationToken cancellationToken);
}
