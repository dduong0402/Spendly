using Microsoft.AspNetCore.Mvc.Filters;

namespace Spendly.Web.Services;

/// <summary>
/// Trước mỗi lần xem trang (GET) của người dùng đã đăng nhập, ghi các khoản chi định kỳ đã đến hạn.
/// Không cần dịch vụ chạy nền: lần nào bạn mở app, các khoản đến hạn (kể cả bị lỡ vài ngày) đều được ghi đúng ngày.
/// Lỗi ở đây không bao giờ được làm hỏng trang đang mở.
/// </summary>
public sealed class RecurringGenerationFilter : IAsyncActionFilter
{
    private readonly IRecurringExpenseService _recurring;
    private readonly ILogger<RecurringGenerationFilter> _logger;

    public RecurringGenerationFilter(IRecurringExpenseService recurring, ILogger<RecurringGenerationFilter> logger)
    {
        _recurring = recurring;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var isPageView = HttpMethods.IsGet(request.Method);
        var isAuthenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
        var isApiCall = request.Path.StartsWithSegments("/api");

        // Các lệnh gọi /api/* (JS của Dashboard) bỏ qua: trang đã được ghi ở lần xem trang ngay trước đó.
        if (isPageView && isAuthenticated && !isApiCall)
        {
            try
            {
                await _recurring.GenerateDueAsync(context.HttpContext.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Không ghi được các khoản chi định kỳ đến hạn.");
            }
        }

        await next();
    }
}
