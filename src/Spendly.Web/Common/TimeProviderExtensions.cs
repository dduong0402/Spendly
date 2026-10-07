namespace Spendly.Web.Common;

public static class TimeProviderExtensions
{
    /// <summary>Ngày hiện tại theo múi giờ của TimeProvider (Việt Nam khi dùng VietnamTimeProvider).</summary>
    public static DateOnly GetToday(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
