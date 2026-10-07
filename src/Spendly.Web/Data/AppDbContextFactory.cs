using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Spendly.Web.Data;

/// <summary>
/// Chỉ dùng lúc chạy lệnh <c>dotnet ef</c> (design-time). Đọc chuỗi kết nối giống ứng dụng:
/// biến môi trường ConnectionStrings__DefaultConnection nếu có, không thì appsettings.json + appsettings.Development.json.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(FindProjectDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Không tìm thấy ConnectionStrings:DefaultConnection trong appsettings.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options, TimeProvider.System);
    }

    private static string FindProjectDirectory()
    {
        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "appsettings.json")))
        {
            return current;
        }

        var nested = Path.Combine(current, "src", "Spendly.Web");
        return File.Exists(Path.Combine(nested, "appsettings.json")) ? nested : current;
    }
}

