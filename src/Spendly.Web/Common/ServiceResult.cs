namespace Spendly.Web.Common;

public enum ServiceStatus
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

/// <summary>Kết quả của một thao tác service, không dùng exception cho lỗi nghiệp vụ.</summary>
public sealed record ServiceResult(ServiceStatus Status, string? Error = null)
{
    public bool IsSuccess => Status == ServiceStatus.Ok;

    public static ServiceResult Ok() => new(ServiceStatus.Ok);

    public static ServiceResult NotFound() => new(ServiceStatus.NotFound);

    public static ServiceResult Invalid(string error) => new(ServiceStatus.Invalid, error);

    public static ServiceResult Conflict(string error) => new(ServiceStatus.Conflict, error);
}

public sealed record ServiceResult<T>(ServiceStatus Status, T? Value = default, string? Error = null)
{
    public bool IsSuccess => Status == ServiceStatus.Ok;

    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value);

    public static ServiceResult<T> NotFound() => new(ServiceStatus.NotFound);

    public static ServiceResult<T> Invalid(string error) => new(ServiceStatus.Invalid, default, error);

    public static ServiceResult<T> Conflict(string error) => new(ServiceStatus.Conflict, default, error);
}
