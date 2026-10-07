using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Spendly.Web.Domain;

namespace Spendly.Web.Data.Configurations;

public class CategoryBudgetConfiguration : IEntityTypeConfiguration<CategoryBudget>
{
    public void Configure(EntityTypeBuilder<CategoryBudget> builder)
    {
        builder.ToTable("CategoryBudgets", t => t.HasCheckConstraint("CK_CategoryBudgets_Amount_Positive", "[Amount] > 0"));

        builder.Property(b => b.Period).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(b => b.Amount).IsRequired();

        // Mỗi người chỉ có một hạn mức cho mỗi (danh mục, loại kỳ).
        builder.HasIndex(b => new { b.UserId, b.CategoryId, b.Period }).IsUnique();

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (không cascade) để tránh nhiều đường xóa dây chuyền trong SQL Server;
        // CategoryService tự xóa các hạn mức của danh mục trước khi xóa danh mục.
        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
