using BayiPerformansAppWeb.Entities.Search;

namespace BayiPerformansAppWeb.DataAccess.Abstractions;

public interface IElasticsearchSalesSearch
{
    Task<SatisSearchPage> SearchAsync(
        SatisSearchQuery query,
        CancellationToken cancellationToken);
}

public sealed class ElasticsearchSearchException(
    string errorCode,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ErrorCode { get; } = errorCode;
}
