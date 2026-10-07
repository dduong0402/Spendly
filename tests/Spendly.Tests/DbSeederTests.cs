using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Spendly.Tests.TestSupport;
using Spendly.Web.Data.Seed;
using Xunit;

namespace Spendly.Tests;

public class DbSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesAllDefaultCategories()
    {
        using var t = TestDb.Create();

        await DbSeeder.SeedAsync(t.Db);

        var categories = await t.Db.Categories.ToListAsync();
        categories.Should().HaveCount(DefaultCategories.All.Count);
        categories.Should().OnlyContain(c => c.UserId == null);
        categories.Select(c => c.Name).Should().Contain(new[] { "Ăn uống", "Khác" });
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotCreateDuplicates()
    {
        using var t = TestDb.Create();

        await DbSeeder.SeedAsync(t.Db);
        await DbSeeder.SeedAsync(t.Db);

        (await t.Db.Categories.CountAsync()).Should().Be(DefaultCategories.All.Count);
    }

    [Fact]
    public async Task SeedAsync_OnlyAddsMissingCategories()
    {
        using var t = TestDb.Create();
        await DbSeeder.SeedAsync(t.Db);
        var removed = await t.Db.Categories.FirstAsync(c => c.Name == "Giải trí");
        t.Db.Categories.Remove(removed);
        await t.Db.SaveChangesAsync();

        await DbSeeder.SeedAsync(t.Db);

        (await t.Db.Categories.CountAsync()).Should().Be(DefaultCategories.All.Count);
        (await t.Db.Categories.AnyAsync(c => c.Name == "Giải trí")).Should().BeTrue();
    }
}
