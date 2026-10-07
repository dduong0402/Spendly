using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Spendly.Web.Domain;

namespace Spendly.Web.Data.Configurations;

public class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("RecurringExpenses", t => t.HasCheckConstraint("CK_RecurringExpenses_Amount_Positive", "[Amount] > 0"));

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Amount).IsRequired();
        builder.Property(r => r.Frequency).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(r => r.StartDate).IsRequired();
        builder.Property(r => r.NextDueDate).IsRequired();
        builder.Ignore(r => r.IsFinished);

        // Truy vấn "quy tắc nào đến hạn" của một người dùng chạy ở mỗi lần mở trang.
        builder.HasIndex(r => new { r.UserId, r.IsActive, r.NextDueDate });

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Không cho xóa danh mục đang được dùng bởi một quy tắc định kỳ.
        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
