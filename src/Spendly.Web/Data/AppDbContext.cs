using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Spendly.Web.Domain;

namespace Spendly.Web.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly TimeProvider _timeProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
        : base(options)
    {
        _timeProvider = timeProvider;
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Budget> Budgets => Set<Budget>();

    public DbSet<CategoryBudget> CategoryBudgets => Set<CategoryBudget>();

    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Tự gán CreatedAt/UpdatedAt để service không phải nhớ.</summary>
    private void ApplyTimestamps()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<Expense>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Budget>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(b => b.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<CategoryBudget>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(b => b.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<RecurringExpense>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(r => r.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
        }
    }
}
