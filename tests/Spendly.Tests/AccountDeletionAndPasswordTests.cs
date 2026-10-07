using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spendly.Tests.TestSupport;
using Spendly.Web.Auth;
using Spendly.Web.Data;
using Spendly.Web.Data.Seed;
using Spendly.Web.Domain;
using Spendly.Web.Services;
using Xunit;

namespace Spendly.Tests;

public class AccountDeletionServiceTests
{
    private const string Password = "matkhau123";

    private static async Task<ApplicationUser> NewUserAsync(UserManager<ApplicationUser> users, string email)
    {
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = email };
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        return user;
    }

    private static async Task SeedDataAsync(AppDbContext db, ApplicationUser user, Category shared)
    {
        var own = new Category { UserId = user.Id, Name = "Riêng", Color = "#1F6BFF", Icon = "☕" };
        db.Categories.Add(own);
        await db.SaveChangesAsync();

        var rule = new RecurringExpense
        {
            UserId = user.Id, Name = "Tiền nhà", CategoryId = own.Id, Amount = 1_000_000, Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateOnly(2026, 9, 1), NextDueDate = new DateOnly(2026, 10, 1)
        };
        db.RecurringExpenses.Add(rule);
        await db.SaveChangesAsync();

        db.Expenses.AddRange(
            new Expense { UserId = user.Id, CategoryId = shared.Id, Amount = 10_000, SpentAt = new DateOnly(2026, 9, 1) },
            new Expense { UserId = user.Id, CategoryId = own.Id, Amount = 1_000_000, SpentAt = new DateOnly(2026, 9, 1), RecurringExpenseId = rule.Id });
        db.Budgets.Add(new Budget { UserId = user.Id, Period = BudgetPeriod.Month, Amount = 5_000_000 });
        db.CategoryBudgets.Add(new CategoryBudget { UserId = user.Id, CategoryId = own.Id, Period = BudgetPeriod.Month, Amount = 2_000_000 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteAccount_RemovesEverythingOfTheUser_AndKeepsOthersAndSharedCategories()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await DbSeeder.SeedAsync(db);
        var shared = await db.Categories.Where(c => c.UserId == null).OrderBy(c => c.Id).FirstAsync();
        var sharedCount = await db.Categories.CountAsync(c => c.UserId == null);
        var a = await NewUserAsync(users, "a@example.com");
        var b = await NewUserAsync(users, "b@example.com");
        await SeedDataAsync(db, a, shared);
        await SeedDataAsync(db, b, shared);
        var service = new AccountDeletionService(db, users);

        var result = await service.DeleteAccountAsync(a);

        result.IsSuccess.Should().BeTrue();
        (await users.FindByIdAsync(a.Id)).Should().BeNull();
        (await db.Expenses.CountAsync(e => e.UserId == a.Id)).Should().Be(0);
        (await db.RecurringExpenses.CountAsync(r => r.UserId == a.Id)).Should().Be(0);
        (await db.Budgets.CountAsync(x => x.UserId == a.Id)).Should().Be(0);
        (await db.CategoryBudgets.CountAsync(x => x.UserId == a.Id)).Should().Be(0);
        (await db.Categories.CountAsync(c => c.UserId == a.Id)).Should().Be(0);

        // Người dùng khác và danh mục mặc định còn nguyên.
        (await users.FindByIdAsync(b.Id)).Should().NotBeNull();
        (await db.Expenses.CountAsync(e => e.UserId == b.Id)).Should().Be(2);
        (await db.RecurringExpenses.CountAsync(r => r.UserId == b.Id)).Should().Be(1);
        (await db.Budgets.CountAsync(x => x.UserId == b.Id)).Should().Be(1);
        (await db.CategoryBudgets.CountAsync(x => x.UserId == b.Id)).Should().Be(1);
        (await db.Categories.CountAsync(c => c.UserId == b.Id)).Should().Be(1);
        (await db.Categories.CountAsync(c => c.UserId == null)).Should().Be(sharedCount);
    }

    [Fact]
    public async Task DeleteAccount_ForUserWithoutData_Succeeds()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var a = await NewUserAsync(users, "a@example.com");

        var result = await new AccountDeletionService(db, users).DeleteAccountAsync(a);

        result.IsSuccess.Should().BeTrue();
        (await users.FindByEmailAsync("a@example.com")).Should().BeNull();
    }
}

public class PasswordFlowTests
{
    private const string Password = "matkhau123";

    private static async Task<(UserManager<ApplicationUser> Users, ApplicationUser User)> ArrangeAsync(IServiceScope scope)
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "a@example.com", Email = "a@example.com", DisplayName = "A" };
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        return (users, user);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPassword()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var (users, user) = await ArrangeAsync(scope);
        var token = await users.GeneratePasswordResetTokenAsync(user);

        var result = await users.ResetPasswordAsync(user, token, "matkhaumoi456");

        result.Succeeded.Should().BeTrue();
        (await users.CheckPasswordAsync(user, "matkhaumoi456")).Should().BeTrue();
        (await users.CheckPasswordAsync(user, Password)).Should().BeFalse();
    }

    [Fact]
    public async Task ResetPassword_TokenIsSingleUse()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var (users, user) = await ArrangeAsync(scope);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        (await users.ResetPasswordAsync(user, token, "matkhaumoi456")).Succeeded.Should().BeTrue();

        var again = await users.ResetPasswordAsync(user, token, "khac789matkhau");

        again.Succeeded.Should().BeFalse();
        again.Errors.Should().ContainSingle(e => e.Code == "InvalidToken");
        again.Errors.Single().Description.Should().Contain("hết hạn");
    }

    [Fact]
    public async Task ResetPassword_WithGarbageToken_Fails()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var (users, user) = await ArrangeAsync(scope);

        var result = await users.ResetPasswordAsync(user, "khong-phai-token", "matkhaumoi456");

        result.Succeeded.Should().BeFalse();
        (await users.CheckPasswordAsync(user, Password)).Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_WithWeakNewPassword_IsRejected()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var (users, user) = await ArrangeAsync(scope);
        var token = await users.GeneratePasswordResetTokenAsync(user);

        var result = await users.ResetPasswordAsync(user, token, "ngan1");

        result.Succeeded.Should().BeFalse();
        result.Errors.Select(e => e.Code).Should().Contain("PasswordTooShort");
    }

    [Fact]
    public async Task ResetToken_IsInvalidatedWhenPasswordIsChangedAfterwards()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var (users, user) = await ArrangeAsync(scope);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        (await users.ChangePasswordAsync(user, Password, "matkhaumoi456")).Succeeded.Should().BeTrue();

        var result = await users.ResetPasswordAsync(user, token, "khac789matkhau");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Fails_AndCorrectOneRotatesSecurityStamp()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var (users, user) = await ArrangeAsync(scope);
        var stampBefore = await users.GetSecurityStampAsync(user);

        var wrong = await users.ChangePasswordAsync(user, "sai-mat-khau-1", "matkhaumoi456");
        wrong.Succeeded.Should().BeFalse();
        wrong.Errors.Should().ContainSingle(e => e.Code == "PasswordMismatch");

        (await users.ChangePasswordAsync(user, Password, "matkhaumoi456")).Succeeded.Should().BeTrue();
        (await users.GetSecurityStampAsync(user)).Should().NotBe(stampBefore);
    }

    [Fact]
    public void IdentityOptions_ResetLinkLivesOneHour_AndStampIsCheckedEveryMinute()
    {
        using var host = IdentityTestHost.Create();

        host.Provider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan
            .Should().Be(TimeSpan.FromHours(1));
        Spendly.Web.Auth.IdentityServiceCollectionExtensions.PasswordResetLifetime
             .Should().Be(TimeSpan.FromHours(1));
        host.Provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval
            .Should().Be(TimeSpan.FromMinutes(1));
    }
}
