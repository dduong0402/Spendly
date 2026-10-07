using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Spendly.Tests.TestSupport;
using Spendly.Web.Common;
using Spendly.Web.Domain;
using Xunit;

namespace Spendly.Tests;

public class IdentityRulesTests
{
    private const string ValidPassword = "matkhau123";

    private static ApplicationUser NewUser(string email = "user@example.com") => new()
    {
        UserName = email,
        Email = email,
        DisplayName = "Duong"
    };

    [Fact]
    public async Task CreateAsync_WithValidPassword_Succeeds()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await users.CreateAsync(NewUser(), ValidPassword);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("short1", "PasswordTooShort")]
    [InlineData("khongcochuso", "PasswordRequiresDigit")]
    [InlineData("12345678", "PasswordRequiresLower")]
    public async Task CreateAsync_WithWeakPassword_IsRejected(string password, string expectedCode)
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await users.CreateAsync(NewUser(), password);

        result.Succeeded.Should().BeFalse();
        result.Errors.Select(e => e.Code).Should().Contain(expectedCode);
    }

    [Fact]
    public async Task CreateAsync_TooShortPassword_HasVietnameseMessage()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await users.CreateAsync(NewUser(), "short1");

        result.Errors.Should().Contain(e => e.Description.Contains("ít nhất 8 ký tự"));
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateEmail_IsRejectedCaseInsensitively()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CreateAsync(NewUser("user@example.com"), ValidPassword)).Succeeded.Should().BeTrue();

        var result = await users.CreateAsync(NewUser("USER@example.com"), ValidPassword);

        result.Succeeded.Should().BeFalse();
        result.Errors.Select(e => e.Code).Should().Contain("DuplicateUserName");
    }

    [Fact]
    public async Task CheckPasswordSignIn_After5WrongAttempts_LocksAccountEvenWithCorrectPassword()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = NewUser();
        (await users.CreateAsync(user, ValidPassword)).Succeeded.Should().BeTrue();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await signIn.CheckPasswordSignInAsync(user, "sai-mat-khau-1", lockoutOnFailure: true);
            wrong.Succeeded.Should().BeFalse();
        }

        var result = await signIn.CheckPasswordSignInAsync(user, ValidPassword, lockoutOnFailure: true);

        result.IsLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimsPrincipal_ContainsUserIdAndDisplayName()
    {
        using var host = IdentityTestHost.Create();
        using var scope = host.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var user = NewUser();
        (await users.CreateAsync(user, ValidPassword)).Succeeded.Should().BeTrue();

        var principal = await factory.CreateAsync(user);

        principal.GetUserId().Should().Be(user.Id);
        principal.GetDisplayName().Should().Be("Duong");
    }
}
