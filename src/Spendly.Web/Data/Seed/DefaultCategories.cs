namespace Spendly.Web.Data.Seed;

public static class DefaultCategories
{
    public sealed record Item(string Name, string Color, string Icon);

    public static readonly IReadOnlyList<Item> All = new List<Item>
    {
        new("Ăn uống",   "#1F6BFF", "🍽️"),
        new("Di chuyển", "#8EBBFF", "🚗"),
        new("Nhà ở",     "#5B8DEF", "🏠"),
        new("Hóa đơn",   "#F59E0B", "🧾"),
        new("Mua sắm",   "#E5484D", "🛍️"),
        new("Giải trí",  "#A78BFA", "🎬"),
        new("Sức khỏe",  "#16A34A", "🩺"),
        new("Giáo dục",  "#06B6D4", "🎓"),
        new("Khác",      "#94A3B8", "📌")
    };
}
