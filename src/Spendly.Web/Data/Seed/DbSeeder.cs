using Microsoft.EntityFrameworkCore;
using Spendly.Web.Domain;
using Spendly.Web.Models.Categories;

namespace Spendly.Web.Data.Seed;

public static class DbSeeder
{
    /// <summary>
    /// Thêm các danh mục mặc định còn thiếu và đổi icon dạng khóa cũ ("utensils"...) sang emoji.
    /// Chạy nhiều lần vẫn an toàn (idempotent).
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Categories
            .Where(c => c.UserId == null)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var missing = DefaultCategories.All
            .Where(d => !existing.Contains(d.Name))
            .Select(d => new Category { UserId = null, Name = d.Name, Color = d.Color, Icon = d.Icon })
            .ToList();

        if (missing.Count > 0)
        {
            db.Categories.AddRange(missing);
        }

        var legacyKeys = CategoryIcons.LegacyKeys.ToArray();
        var stale = await db.Categories
            .Where(c => legacyKeys.Contains(c.Icon))
            .ToListAsync(cancellationToken);

        foreach (var category in stale)
        {
            category.Icon = CategoryIcons.Resolve(category.Icon);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
