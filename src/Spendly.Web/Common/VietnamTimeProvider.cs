namespace Spendly.Web.Common;

/// <summary>
/// TimeProvider có múi giờ địa phương cố định UTC+7 (Việt Nam không dùng giờ mùa hè),
/// nên không phụ thuộc dữ liệu múi giờ của hệ điều hành.
/// </summary>
public class VietnamTimeProvider : TimeProvider
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.CreateCustomTimeZone(
        id: "Asia/Ho_Chi_Minh",
        baseUtcOffset: TimeSpan.FromHours(7),
        displayName: "(UTC+07:00) Việt Nam",
        standardDisplayName: "Giờ Việt Nam");

    public override TimeZoneInfo LocalTimeZone => TimeZone;
}
