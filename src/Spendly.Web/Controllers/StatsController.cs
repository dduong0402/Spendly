using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spendly.Web.Common;
using Spendly.Web.Models.Stats;
using Spendly.Web.Services;

namespace Spendly.Web.Controllers;

/// <summary>
/// API JSON thống kê cho Dashboard. period = day | week | month, date = yyyy-MM-dd (mặc định hôm nay theo giờ Việt Nam).
/// </summary>
[ApiController]
[Authorize]
[Route("api/stats")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class StatsController : ControllerBase
{
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    private readonly IStatsService _stats;
    private readonly TimeProvider _clock;

    public StatsController(IStatsService stats, TimeProvider clock)
    {
        _stats = stats;
        _clock = clock;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] StatsPeriod period = StatsPeriod.Week,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        return TryResolveDate(date, out var resolved, out var problem)
            ? Ok(await _stats.GetSummaryAsync(period, resolved, cancellationToken))
            : problem!;
    }

    [HttpGet("trend")]
    public async Task<IActionResult> Trend(
        [FromQuery] StatsPeriod period = StatsPeriod.Week,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        return TryResolveDate(date, out var resolved, out var problem)
            ? Ok(await _stats.GetTrendAsync(period, resolved, cancellationToken))
            : problem!;
    }

    [HttpGet("by-category")]
    public async Task<IActionResult> ByCategory(
        [FromQuery] StatsPeriod period = StatsPeriod.Week,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        return TryResolveDate(date, out var resolved, out var problem)
            ? Ok(await _stats.GetByCategoryAsync(period, resolved, cancellationToken))
            : problem!;
    }

    [HttpGet("top-expenses")]
    public async Task<IActionResult> TopExpenses(
        [FromQuery] StatsPeriod period = StatsPeriod.Week,
        [FromQuery] DateOnly? date = null,
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default)
    {
        return TryResolveDate(date, out var resolved, out var problem)
            ? Ok(await _stats.GetTopExpensesAsync(period, resolved, take, cancellationToken))
            : problem!;
    }

    [HttpGet("recent-expenses")]
    public async Task<IActionResult> RecentExpenses(
        [FromQuery] StatsPeriod period = StatsPeriod.Week,
        [FromQuery] DateOnly? date = null,
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default)
    {
        return TryResolveDate(date, out var resolved, out var problem)
            ? Ok(await _stats.GetRecentExpensesAsync(period, resolved, take, cancellationToken))
            : problem!;
    }

    private bool TryResolveDate(DateOnly? date, out DateOnly resolved, out IActionResult? problem)
    {
        resolved = date ?? _clock.GetToday();
        if (resolved < ExpenseRules.MinDate || resolved > MaxDate)
        {
            problem = Problem(
                title: "Ngày không hợp lệ",
                detail: $"Ngày phải nằm trong khoảng {ExpenseRules.MinDate:yyyy-MM-dd} đến {MaxDate:yyyy-MM-dd}.",
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }

        problem = null;
        return true;
    }
}
