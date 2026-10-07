using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Spendly.Web.Auth;
using Spendly.Web.Common;
using Spendly.Web.Data;

namespace Spendly.Tests.TestSupport;

/// <summary>Dựng ServiceProvider nhỏ với Identity thật (cùng cấu hình như app) chạy trên SQLite in-memory.</summary>
public sealed class IdentityTestHost : IDisposable
{
    private readonly SqliteConnection _connection;

    public ServiceProvider Provider { get; }

    private IdentityTestHost()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<TimeProvider, VietnamTimeProvider>();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        services.AddSpendlyIdentity(isDevelopment: true);
        Provider = services.BuildServiceProvider();

        using var scope = Provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public static IdentityTestHost Create() => new();

    public IServiceScope CreateScope() => Provider.CreateScope();

    public void Dispose()
    {
        Provider.Dispose();
        _connection.Dispose();
    }
}
