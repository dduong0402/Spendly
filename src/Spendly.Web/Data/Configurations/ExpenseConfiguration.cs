using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Spendly.Web.Domain;

namespace Spendly.Web.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", t => t.HasCheckConstraint("CK_Expenses_Amount_Positive", "[Amount] > 0"));

        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.SpentAt).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);

        // Phục vụ truy vấn thống kê theo người dùng + khoảng ngày.
        builder.HasIndex(e => new { e.UserId, e.SpentAt });

        // Mỗi quy tắc định kỳ chỉ tạo tối đa một khoản chi cho mỗi ngày đến hạn (chặn ghi trùng khi hai yêu cầu chạy cùng lúc).
        builder.HasIndex(e => new { e.RecurringExpenseId, e.SpentAt })
            .IsUnique()
            .HasFilter("[RecurringExpenseId] IS NOT NULL");

        // NoAction (không SetNull ở database) để tránh nhiều đường xóa dây chuyền trong SQL Server;
        // RecurringExpenseService tự gỡ liên kết trước khi xóa quy tắc, các khoản chi đã ghi vẫn được giữ lại.
        builder.HasOne(e => e.RecurringExpense)
            .WithMany(r => r.Expenses)
            .HasForeignKey(e => e.RecurringExpenseId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.User)
            .WithMany(u => u.Expenses)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Không cho xóa danh mục đang có khoản chi (không xóa cascade).
        builder.HasOne(e => e.Category)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
