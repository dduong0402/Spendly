using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Common;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Models.Expenses;
using Spendly.Web.Models.Recurring;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

/// <summary>Đồng hồ test: Thứ Ba 29/09/2026 (giờ Việt Nam).</summary>
public class RecurringExpenseServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private sealed record Setup(TestDb Db, RecurringExpenseService Service, ApplicationUser User, Category Food);

    private static async Task<Setup> ArrangeAsync()
    {
        var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var user = await t.AddUserAsync("a@example.com");
        var food = await t.Db.Categories.Where(c => c.UserId == null).OrderBy(c => c.Id).FirstAsync();
        var service = new RecurringExpenseService(t.Db, new FakeCurrentUser { UserId = user.Id }, t.Clock);
        return new Setup(t, service, user, food);
    }

    private static SaveRecurringRequest Rule(Setup s, string name, DateOnly start, RecurrenceFrequency frequency = RecurrenceFrequency.Monthly,
        long amount = 3_500_000, DateOnly? end = null) =>
        new(name, amount, s.Food.Id, frequency, start, end);

    private static async Task<int> CreateAsync(Setup s, SaveRecurringRequest request)
    {
        var result = await s.Service.CreateAsync(request);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value;
    }

    private static Task<List<Expense>> ExpensesAsync(Setup s) =>
        s.Db.Db.Expenses.AsNoTracking().OrderBy(e => e.SpentAt).ToListAsync();

    // ---------- Tạo ----------

    [Fact]
    public async Task Create_TrimsName_AndStartsDueOnTheStartDate()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        var id = await CreateAsync(s, Rule(s, "  Tiền nhà  ", new DateOnly(2026, 10, 5)));

        var rule = (await s.Service.GetAsync(id))!;
        rule.Name.Should().Be("Tiền nhà");
        rule.NextDueDate.Should().Be(new DateOnly(2026, 10, 5));
        rule.IsActive.Should().BeTrue();
        rule.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task Create_InvalidOrForeignCategory_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var foreign = TestDb.NewCategory(other.Id, "Của người khác");
        s.Db.Db.Categories.Add(foreign);
        await s.Db.Db.SaveChangesAsync();

        (await s.Service.CreateAsync(Rule(s, "A", Today) with { Amount = 0 })).Status.Should().Be(ServiceStatus.Invalid);
        (await s.Service.CreateAsync(Rule(s, "A", Today) with { CategoryId = 99_999 })).Status.Should().Be(ServiceStatus.Invalid);
        (await s.Service.CreateAsync(Rule(s, "A", Today) with { CategoryId = foreign.Id })).Status.Should().Be(ServiceStatus.Invalid);
        (await s.Db.Db.RecurringExpenses.CountAsync()).Should().Be(0);
    }

    // ---------- Ghi khoản chi đến hạn ----------

    [Fact]
    public async Task GenerateDue_Monthly_BackfillsEveryMissedDueDate_WithRuleDetails()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 7, 5)));

        var created = await s.Service.GenerateDueAsync();

        created.Should().Be(3);
        var expenses = await ExpensesAsync(s);
        expenses.Select(e => e.SpentAt).Should().Equal(new DateOnly(2026, 7, 5), new DateOnly(2026, 8, 5), new DateOnly(2026, 9, 5));
        expenses.Should().OnlyContain(e => e.Amount == 3_500_000 && e.Note == "Tiền nhà" && e.CategoryId == s.Food.Id
                                           && e.UserId == s.User.Id && e.RecurringExpenseId == id);
        (await s.Service.GetAsync(id))!.NextDueDate.Should().Be(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public async Task GenerateDue_IsIdempotent()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 7, 5)));

        (await s.Service.GenerateDueAsync()).Should().Be(3);
        (await s.Service.GenerateDueAsync()).Should().Be(0);
        (await s.Db.Db.Expenses.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task GenerateDue_FutureStart_CreatesNothing()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 10, 5)));

        (await s.Service.GenerateDueAsync()).Should().Be(0);
        (await s.Db.Db.Expenses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GenerateDue_Weekly_IncludesToday_AndSchedulesNextWeek()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Gửi xe", new DateOnly(2026, 9, 8), RecurrenceFrequency.Weekly, 50_000));

        (await s.Service.GenerateDueAsync()).Should().Be(4);

        (await ExpensesAsync(s)).Select(e => e.SpentAt).Should().Equal(
            new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 29));
        (await s.Service.GetAsync(id))!.NextDueDate.Should().Be(new DateOnly(2026, 10, 6));
    }

    [Fact]
    public async Task GenerateDue_MonthlyOnThe31st_UsesMonthEnd_WithoutDrifting()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Điện", new DateOnly(2026, 5, 31)));

        (await s.Service.GenerateDueAsync()).Should().Be(4);

        (await ExpensesAsync(s)).Select(e => e.SpentAt).Should().Equal(
            new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 31), new DateOnly(2026, 8, 31));
        (await s.Service.GetAsync(id))!.NextDueDate.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public async Task GenerateDue_StopsAtEndDate_AndMarksTheRuleFinished()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Học phí", new DateOnly(2026, 6, 5), end: new DateOnly(2026, 8, 5)));

        (await s.Service.GenerateDueAsync()).Should().Be(3);
        (await s.Service.GenerateDueAsync()).Should().Be(0);

        var item = (await s.Service.ListAsync()).Single();
        item.IsFinished.Should().BeTrue();
        (await s.Service.GetAsync(id))!.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateDue_SkipsPausedRules()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 7, 5)));
        (await s.Service.SetActiveAsync(id, false)).IsSuccess.Should().BeTrue();

        (await s.Service.GenerateDueAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Resume_SkipsOccurrencesMissedWhilePaused()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 7, 5)));
        await s.Service.SetActiveAsync(id, false);

        await s.Service.SetActiveAsync(id, true);

        (await s.Service.GetAsync(id))!.NextDueDate.Should().Be(new DateOnly(2026, 10, 5));
        (await s.Service.GenerateDueAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GenerateDue_OnlyForTheCurrentUser()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new RecurringExpenseService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        await CreateAsync(s, Rule(s, "Của A", new DateOnly(2026, 8, 5)));

        (await otherService.GenerateDueAsync()).Should().Be(0);
        (await s.Db.Db.Expenses.CountAsync()).Should().Be(0);
        (await s.Service.GenerateDueAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DeletedGeneratedExpense_IsNotGeneratedAgain()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 8, 5)));
        await s.Service.GenerateDueAsync();

        var first = await s.Db.Db.Expenses.OrderBy(e => e.SpentAt).FirstAsync();
        s.Db.Db.Expenses.Remove(first);
        await s.Db.Db.SaveChangesAsync();

        (await s.Service.GenerateDueAsync()).Should().Be(0);
        (await s.Db.Db.Expenses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UniqueIndex_RejectsTwoExpensesOfTheSameRuleOnTheSameDay()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 10, 5)));

        Expense Make() => new()
        {
            UserId = s.User.Id, CategoryId = s.Food.Id, Amount = 1000, SpentAt = new DateOnly(2026, 9, 5), RecurringExpenseId = id
        };

        s.Db.Db.Expenses.Add(Make());
        await s.Db.Db.SaveChangesAsync();
        s.Db.Db.Expenses.Add(Make());

        var act = () => s.Db.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UniqueIndex_DoesNotAffectOrdinaryExpenses()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;

        for (var i = 0; i < 2; i++)
        {
            s.Db.Db.Expenses.Add(new Expense { UserId = s.User.Id, CategoryId = s.Food.Id, Amount = 1000, SpentAt = Today });
        }

        await s.Db.Db.SaveChangesAsync();
        (await s.Db.Db.Expenses.CountAsync()).Should().Be(2);
    }

    // ---------- Sửa / tạm dừng / xóa ----------

    [Fact]
    public async Task Update_ChangesFields_AndNewAmountOnlyAffectsFutureOccurrences()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Internet", new DateOnly(2026, 8, 5), amount: 250_000));
        await s.Service.GenerateDueAsync(); // 5/8 và 5/9 với 250.000

        var result = await s.Service.UpdateAsync(id, new UpdateRecurringRequest("Internet FPT", 300_000, s.Food.Id, null));

        result.IsSuccess.Should().BeTrue();
        (await ExpensesAsync(s)).Should().OnlyContain(e => e.Amount == 250_000);
        var rule = (await s.Service.GetAsync(id))!;
        rule.Name.Should().Be("Internet FPT");
        rule.Amount.Should().Be(300_000);
        rule.StartDate.Should().Be(new DateOnly(2026, 8, 5));
        rule.Frequency.Should().Be(RecurrenceFrequency.Monthly);
    }

    [Fact]
    public async Task Update_EndDateBeforeStart_IsRejected()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Internet", new DateOnly(2026, 8, 5)));

        var result = await s.Service.UpdateAsync(id, new UpdateRecurringRequest("Internet", 250_000, s.Food.Id, new DateOnly(2026, 8, 4)));

        result.Status.Should().Be(ServiceStatus.Invalid);
    }

    [Fact]
    public async Task Update_ExtendingAFinishedRule_ResumesFromNextOccurrence_WithoutBackfillingTheGap()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Học phí", new DateOnly(2026, 1, 5), end: new DateOnly(2026, 3, 5)));
        await s.Service.GenerateDueAsync();
        (await s.Db.Db.Expenses.CountAsync()).Should().Be(3);

        await s.Service.UpdateAsync(id, new UpdateRecurringRequest("Học phí", 3_500_000, s.Food.Id, null));

        (await s.Service.GetAsync(id))!.NextDueDate.Should().Be(new DateOnly(2026, 10, 5));
        (await s.Service.GenerateDueAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_KeepsGeneratedExpenses_ButUnlinksThem()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 8, 5)));
        await s.Service.GenerateDueAsync();

        (await s.Service.DeleteAsync(id)).IsSuccess.Should().BeTrue();

        (await s.Db.Db.RecurringExpenses.CountAsync()).Should().Be(0);
        var expenses = await ExpensesAsync(s);
        expenses.Should().HaveCount(2);
        expenses.Should().OnlyContain(e => e.RecurringExpenseId == null);
    }

    [Fact]
    public async Task GetUpdateToggleDelete_OnAnotherUsersRule_ReturnNotFound()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var other = await s.Db.AddUserAsync("b@example.com");
        var otherService = new RecurringExpenseService(s.Db.Db, new FakeCurrentUser { UserId = other.Id }, s.Db.Clock);
        var id = await CreateAsync(s, Rule(s, "Của A", new DateOnly(2026, 10, 5)));

        (await otherService.GetAsync(id)).Should().BeNull();
        (await otherService.UpdateAsync(id, new UpdateRecurringRequest("X", 1000, s.Food.Id, null))).Status.Should().Be(ServiceStatus.NotFound);
        (await otherService.SetActiveAsync(id, false)).Status.Should().Be(ServiceStatus.NotFound);
        (await otherService.DeleteAsync(id)).Status.Should().Be(ServiceStatus.NotFound);
        (await s.Db.Db.RecurringExpenses.CountAsync()).Should().Be(1);
    }

    // ---------- Danh sách ----------

    [Fact]
    public async Task List_PutsRunningRulesFirstByNextDue_AndComputesCountsAndMonthlyEquivalent()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var rent = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 8, 5)));
        var park = await CreateAsync(s, Rule(s, "Gửi xe", new DateOnly(2026, 10, 1), RecurrenceFrequency.Weekly, 100_000));
        var paused = await CreateAsync(s, Rule(s, "Tạm dừng", new DateOnly(2026, 10, 2)));
        await s.Service.SetActiveAsync(paused, false);
        await s.Service.GenerateDueAsync();

        var list = await s.Service.ListAsync();

        // Đang chạy xếp theo lần tới (park 01/10 trước rent 05/10), tạm dừng xuống cuối.
        list.Select(i => i.Id).Should().Equal(park, rent, paused);
        list.Single(i => i.Id == rent).GeneratedCount.Should().Be(2);
        list.Single(i => i.Id == park).MonthlyEquivalent.Should().Be(433_333);
    }

    // ---------- Liên quan tới các service khác ----------

    [Fact]
    public async Task ExpenseList_FlagsGeneratedExpensesAsRecurring()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 9, 5)));
        await s.Service.GenerateDueAsync();
        s.Db.Db.Expenses.Add(new Expense { UserId = s.User.Id, CategoryId = s.Food.Id, Amount = 1000, SpentAt = Today });
        await s.Db.Db.SaveChangesAsync();
        var expenses = new ExpenseService(s.Db.Db, new FakeCurrentUser { UserId = s.User.Id }, s.Db.Clock);

        var result = await expenses.ListAsync(new ExpenseFilter());

        result.Items.Single(i => i.SpentAt == new DateOnly(2026, 9, 5)).IsRecurring.Should().BeTrue();
        result.Items.Single(i => i.SpentAt == Today).IsRecurring.Should().BeFalse();
    }

    [Fact]
    public async Task EditingAGeneratedExpense_ToAnotherGeneratedDate_ReturnsConflictInsteadOfCrashing()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 8, 5)));
        await s.Service.GenerateDueAsync(); // 5/8 và 5/9
        var expenses = new ExpenseService(s.Db.Db, new FakeCurrentUser { UserId = s.User.Id }, s.Db.Clock);
        var september = await s.Db.Db.Expenses.SingleAsync(e => e.SpentAt == new DateOnly(2026, 9, 5));

        var result = await expenses.UpdateAsync(september.Id, new SaveExpenseRequest(3_500_000, s.Food.Id, new DateOnly(2026, 8, 5), "Tiền nhà"));

        result.Status.Should().Be(ServiceStatus.Conflict);
    }

    [Fact]
    public async Task EditingAGeneratedExpense_ToAFreeDate_IsAllowed_AndKeepsTheLink()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var id = await CreateAsync(s, Rule(s, "Tiền nhà", new DateOnly(2026, 9, 5)));
        await s.Service.GenerateDueAsync();
        var expenses = new ExpenseService(s.Db.Db, new FakeCurrentUser { UserId = s.User.Id }, s.Db.Clock);
        var expense = await s.Db.Db.Expenses.SingleAsync();

        var result = await expenses.UpdateAsync(expense.Id, new SaveExpenseRequest(3_600_000, s.Food.Id, new DateOnly(2026, 9, 7), "Tiền nhà"));

        result.IsSuccess.Should().BeTrue();
        var saved = await s.Db.Db.Expenses.AsNoTracking().SingleAsync();
        saved.RecurringExpenseId.Should().Be(id);
        saved.Amount.Should().Be(3_600_000);
    }

    [Fact]
    public async Task DeleteCategory_UsedByARecurringRule_IsBlocked()
    {
        var s = await ArrangeAsync();
        using var _ = s.Db;
        var own = TestDb.NewCategory(s.User.Id, "Đăng ký");
        s.Db.Db.Categories.Add(own);
        await s.Db.Db.SaveChangesAsync();
        await CreateAsync(s, Rule(s, "Netflix", new DateOnly(2026, 10, 5)) with { CategoryId = own.Id });
        var categories = new CategoryService(s.Db.Db, new FakeCurrentUser { UserId = s.User.Id });

        var result = await categories.DeleteAsync(own.Id);

        result.Status.Should().Be(ServiceStatus.Conflict);
        (await s.Db.Db.Categories.AnyAsync(c => c.Id == own.Id)).Should().BeTrue();
    }
}
