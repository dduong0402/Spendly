using Microsoft.AspNetCore.Identity;

namespace Spendly.Web.Domain;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
