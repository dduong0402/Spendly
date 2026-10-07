namespace Spendly.Web.Models.Stats;

public enum StatsPeriod
{
    Day,
    Week,
    Month
}

/// <summary>Khoảng ngày [From, To], gồm cả hai đầu.</summary>
public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    public int DayCount => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= From && date <= To;
}

public sealed record StatsSummary(
    string Period,
    DateOnly From,
    DateOnly To,
    string RangeLabel,
    string ComparisonLabel,
    bool IsCurrent,
    long TotalAmount,
    int TransactionCount,
    long AveragePerDay,
    long PreviousTotal,
    double? ChangePercent,
    DateOnly PreviousDate,
    DateOnly NextDate,
    bool HasNext);

public sealed record TrendPoint(DateOnly Date, string Label, long Amount, bool IsFuture);

public sealed record CategoryShare(
    int CategoryId,
    string Name,
    string Color,
    string Emoji,
    long Amount,
    int Count,
    double Percent);

public sealed record StatsExpenseItem(
    int Id,
    DateOnly SpentAt,
    long Amount,
    string? Note,
    string CategoryName,
    string CategoryColor,
    string CategoryEmoji);
