namespace BayiPerformansAppWeb.Business.Models;

public enum ServiceErrorKind
{
    None,
    Validation,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record ServiceResult<T>(
    T? Value,
    ServiceErrorKind ErrorKind,
    string? ErrorCode,
    string? ErrorDetail)
{
    public bool IsSuccess => ErrorKind == ServiceErrorKind.None && Value is not null;

    public static ServiceResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ServiceResult<T>(value, ServiceErrorKind.None, null, null);
    }

    public static ServiceResult<T> Fail(
        ServiceErrorKind errorKind,
        string errorCode,
        string errorDetail)
    {
        if (errorKind == ServiceErrorKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorKind),
                "Basarisiz sonuc icin bir hata turu secilmelidir.");
        }

        return new ServiceResult<T>(default, errorKind, errorCode, errorDetail);
    }

    public static ServiceResult<T> Fail(
        string errorCode,
        string errorDetail,
        ServiceErrorKind errorKind = ServiceErrorKind.Validation)
    {
        return Fail(errorKind, errorCode, errorDetail);
    }
}
