using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Spendly.Web.Data;
using Spendly.Web.Domain;

namespace Spendly.Tests.TestSupport;

/// <summary>SQLite in-memory dùng chung một connection để nhiều DbContext thấy cùng dữ liệu.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public MutableTimeProvider Clock { get; }

    public AppDbContext Db { get; }

    private TestDb(MutableTimeProvider clock)
    {
        Clock = clock;
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        Db = NewContext();
        Db.Database.EnsureCreated();
    }

    public static TestDb Create(MutableTimeProvider? clock = null) => new(clock ?? new MutableTimeProvider());

    public AppDbContext NewContext() => new(_options, Clock);

    public static ApplicationUser NewUser(string email = "duong@example.com") => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = email,
        Email = email,
        DisplayName = "Duong"
    };

    public static Category NewCategory(string userId, string name = "Ăn uống") => new()
    {
        UserId = userId,
        Name = name,
        Color = "#1F6BFF",
        Icon = "utensils"
    };

    public async Task<ApplicationUser> AddUserAsync(string email)
    {
        var user = NewUser(email);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
