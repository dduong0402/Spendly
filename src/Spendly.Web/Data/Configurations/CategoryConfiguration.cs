using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Spendly.Web.Domain;

namespace Spendly.Web.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.Property(c => c.Name).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Color).HasMaxLength(7).IsRequired();
        builder.Property(c => c.Icon).HasMaxLength(30).IsRequired();
        builder.Ignore(c => c.IsSystemDefault);

        // Tên danh mục không trùng trong cùng một người dùng.
        // HasFilter(null): bỏ bộ lọc "IS NOT NULL" mặc định của SQL Server
        // để các danh mục hệ thống (UserId = NULL) cũng không bị trùng tên.
        builder.HasIndex(c => new { c.UserId, c.Name }).IsUnique().HasFilter(null);

        builder.HasOne(c => c.User)
            .WithMany(u => u.Categories)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.NoAction);;
    }
}
