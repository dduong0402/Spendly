using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class AccountDataServiceTests
{
    private sealed record Setup(TestDb Db, AccountDataService Service, ApplicationUser User, ApplicationUser Other, Category Food, Category Own);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var other = await t.AddUserAsync("b@example.com");
        var food = await t.Db.Categories.Where(c => c.UserId == null).OrderBy(c => c.Id).FirstAsync();
        var own = TestDb.NewCategory(user.Id, "Cà phê");
        t.Db.Categories.Add(own);
        await t.Db.SaveChangesAsync();
        var service = new AccountDataService(t.Db, new FakeCurrentUser { UserId = user.Id }, t.Clock);
        return new Setup(t, service, user, other, food, own);
    }

    private static async Task AddExpenseAsync(Setup s, ApplicationUser user, DateOnly date, long amount, Category category, string? note, int? ruleId = null)
    {
        s.Db.Db.Expenses.Add(new Expense { UserId = user.Id, CategoryId = category.Id, Amount = amount, SpentAt = date, Note = note, RecurringExpenseId = ruleId });
        await s.Db.Db.SaveChangesAsync();
    }

    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

    [Fact]
    public async Task ExportCsv_ContainsOnlyOwnExpenses_OrderedByDate_WithEscaping()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddExpenseAsync(s, s.User, new DateOnly(2026, 9, 2), 50_000, s.Food, "Phở, bò");
        await AddExpenseAsync(s, s.User, new DateOnly(2026, 9, 1), 30_000, s.Own, "=SUM(A1)");
        await AddExpenseAsync(s, s.Other, new DateOnly(2026, 9, 3), 999_999, s.Food, "Của người khác");

        var bytes = await s.Service.ExportCsvAsync();

        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var lines = Decode(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("sep=,");
        lines[1].Should().Be("Ngày,Số tiền (VND),Danh mục,Ghi chú,Định kỳ");
        lines.Should().HaveCount(4);
        lines[2].Should().Be("2026-09-01,30000,Cà phê,'=SUM(A1),Không");
        lines[3].Should().Be($"2026-09-02,50000,{s.Food.Name},\"Phở, bò\",Không");
        Decode(bytes).Should().NotContain("Của người khác");
    }

    [Fact]
    public async Task ExportCsv_MarksRecurringExpenses()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var rule = new RecurringExpense
        {
            UserId = s.User.Id, Name = "Tiền nhà", CategoryId = s.Food.Id, Amount = 3_500_000, Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 9, 5), NextDueDate = new DateOnly(2026, 10, 5)
        };
        s.Db.Db.RecurringExpenses.Add(rule);
        await s.Db.Db.SaveChangesAsync();
        await AddExpenseAsync(s, s.User, new DateOnly(2026, 9, 5), 3_500_000, s.Food, "Tiền nhà", rule.Id);

        var text = Decode(await s.Service.ExportCsvAsync());

        text.Should().Contain("Tiền nhà,Có");
    }

    [Fact]
    public async Task ExportCsv_WithNoExpenses_StillHasHeader()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var lines = Decode(await s.Service.ExportCsvAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExportJson_ContainsAllOwnData_AndNothingFromOtherUsers()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddExpenseAsync(s, s.User, new DateOnly(2026, 9, 1), 30_000, s.Own, "Cà phê sáng");
        await AddExpenseAsync(s, s.Other, new DateOnly(2026, 9, 3), 999_999, s.Food, "Của người khác");
        s.Db.Db.Budgets.Add(new Budget { UserId = s.User.Id, Period = BudgetPeriod.Month, Amount = 4_000_000 });
        s.Db.Db.CategoryBudgets.Add(new CategoryBudget { UserId = s.User.Id, CategoryId = s.Own.Id, Period = BudgetPeriod.Week, Amount = 200_000 });
        s.Db.Db.RecurringExpenses.Add(new RecurringExpense
        {
            UserId = s.User.Id, Name = "Netflix", CategoryId = s.Food.Id, Amount = 260_000, Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 9, 5), NextDueDate = new DateOnly(2026, 10, 5)
        });
        await s.Db.Db.SaveChangesAsync();

        var bytes = await s.Service.ExportJsonAsync();
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;

        root.GetProperty("app").GetString().Should().Be("Spendly");
        root.GetProperty("account").GetProperty("email").GetString().Should().Be("a@example.com");
        root.GetProperty("categories").GetArrayLength().Should().Be(1);
        root.GetProperty("expenses").GetArrayLength().Should().Be(1);
        root.GetProperty("expenses")[0].GetProperty("date").GetString().Should().Be("2026-09-01");
        root.GetProperty("expenses")[0].GetProperty("category").GetString().Should().Be("Cà phê");
        root.GetProperty("budgets")[0].GetProperty("period").GetString().Should().Be("Month");
        root.GetProperty("categoryBudgets")[0].GetProperty("period").GetString().Should().Be("Week");
        root.GetProperty("recurringExpenses")[0].GetProperty("frequency").GetString().Should().Be("Monthly");
        json.Should().Contain("Cà phê sáng"); // không bị escape thành \uXXXX
        json.Should().NotContain("Của người khác").And.NotContain("b@example.com");
    }

    [Fact]
    public async Task GetSummary_CountsOnlyOwnData()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await AddExpenseAsync(s, s.User, new DateOnly(2026, 9, 1), 30_000, s.Own, null);
        await AddExpenseAsync(s, s.User, new DateOnly(2026, 9, 2), 30_000, s.Own, null);
        await AddExpenseAsync(s, s.Other, new DateOnly(2026, 9, 3), 1_000, s.Food, null);
        s.Db.Db.Budgets.Add(new Budget { UserId = s.User.Id, Period = BudgetPeriod.Month, Amount = 1_000_000 });
        s.Db.Db.CategoryBudgets.Add(new CategoryBudget { UserId = s.User.Id, CategoryId = s.Own.Id, Period = BudgetPeriod.Month, Amount = 500_000 });
        await s.Db.Db.SaveChangesAsync();

        var summary = await s.Service.GetSummaryAsync();

        summary.Expenses.Should().Be(2);
        summary.Categories.Should().Be(1);
        summary.RecurringExpenses.Should().Be(0);
        summary.Budgets.Should().Be(2);
    }
}
