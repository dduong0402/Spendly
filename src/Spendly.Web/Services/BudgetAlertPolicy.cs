using Spendly.Web.Models.Budgets;

namespace Spendly.Web.Services;

/// <summary>Quyết định khi nào hiện thông báo sau khi người dùng lưu một khoản chi.</summary>
public static class BudgetAlertPolicy
{
    /// <summary>Một lần lưu có thể chạm nhiều hạn mức cùng lúc; chỉ hiện tối đa từng này thông báo (nghiêm trọng nhất trước).</summary>
    public const int MaxAlerts = 3;

    /// <summary>
    /// - Chạm mốc cao hơn (50/75/90/100%) so với trước khi lưu: báo mốc cao nhất vừa chạm (một thông báo, dù nhảy qua nhiều mốc).
    /// - Đang vượt hạn mức mà khoản chi mới làm tổng chi tăng thêm: báo vượt hạn mức mỗi lần (vẫn cho ghi bình thường).
    /// - Các trường hợp còn lại (cùng mốc, chi giảm, khoản chi ngoài kỳ hiện tại): không báo, tránh làm phiền.
    /// </summary>
    public static IReadOnlyList<BudgetAlertMessage> Detect(
        IReadOnlyList<BudgetStatus> before,
        IReadOnlyList<BudgetStatus> after)
    {
        return after
            .Where(current => ShouldAlert(before.FirstOrDefault(b => b.Period == current.Period), current))
            .Select(ToAlert)
            .ToList();
    }

    /// <summary>
    /// Áp dụng cùng quy tắc cho cả hạn mức tổng và hạn mức theo danh mục.
    /// Nhiều thông báo cùng lúc thì giữ tối đa <see cref="MaxAlerts"/>, mốc cao hơn lên trước (hạn mức tổng trước khi bằng mốc).
    /// </summary>
    public static IReadOnlyList<BudgetAlertMessage> Detect(BudgetSnapshot before, BudgetSnapshot after)
    {
        var candidates = new List<BudgetStatus>();

        foreach (var current in after.Overall)
        {
            if (ShouldAlert(before.Overall.FirstOrDefault(b => b.Period == current.Period), current))
            {
                candidates.Add(current);
            }
        }

        foreach (var current in after.Categories)
        {
            var previous = before.Categories.FirstOrDefault(c =>
                c.CategoryId == current.CategoryId && c.Status.Period == current.Status.Period);

            if (ShouldAlert(previous?.Status, current.Status))
            {
                candidates.Add(current.Status);
            }
        }

        // OrderByDescending ổn định: cùng mốc thì giữ thứ tự (tổng trước, danh mục sau).
        return candidates
            .OrderByDescending(status => status.Level)
            .Take(MaxAlerts)
            .Select(ToAlert)
            .ToList();
    }

    private static bool ShouldAlert(BudgetStatus? previous, BudgetStatus current)
    {
        if (previous is null)
        {
            return false;
        }

        var crossedHigherLevel = current.Level > previous.Level && current.Level >= BudgetLevel.Half;
        var exceededAndSpentMore = current.Level == BudgetLevel.Exceeded && current.Spent > previous.Spent;

        return crossedHigherLevel || exceededAndSpentMore;
    }

    public static BudgetAlertMessage ToAlert(BudgetStatus status) => new(
        status.Period.ToString(),
        status.Severity,
        $"{status.PeriodLabel}: {status.Title}",
        status.Message,
        status.Advice);
}
