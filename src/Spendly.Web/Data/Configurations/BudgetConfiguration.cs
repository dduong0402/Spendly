using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Spendly.Web.Domain;

namespace Spendly.Web.Data.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets", t => t.HasCheckConstraint("CK_Budgets_Amount_Positive", "[Amount] > 0"));

        // Lưu "Week"/"Month" dạng chữ cho dễ đọc khi xem trực tiếp trong database.
        builder.Property(b => b.Period).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(b => b.Amount).IsRequired();

        // Mỗi người chỉ có một hạn mức cho mỗi loại kỳ.
        builder.HasIndex(b => new { b.UserId, b.Period }).IsUnique();

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
