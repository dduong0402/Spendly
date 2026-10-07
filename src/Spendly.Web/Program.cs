using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Spendly.Web.Auth;
using Spendly.Web.Common;
using Spendly.Web.Data;
using Spendly.Web.Data.Seed;
using Spendly.Web.Services;
using Spendly.Web.Services.Email;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection.");

// Production trong container: khóa DataProtection phải được lưu ở nơi bền vững (volume / thư mục /home trên Azure),
// nếu không mỗi lần container khởi động lại sẽ làm hỏng cookie đăng nhập và liên kết đặt lại mật khẩu.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Spendly");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddSingleton<TimeProvider, VietnamTimeProvider>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSpendlyIdentity(builder.Environment.IsDevelopment());
builder.Services.AddHttpContextAccessor();

// Email: có Email:Host thì gửi qua SMTP; chưa cấu hình thì in ra console (Development) để vẫn thử được tính năng quên mật khẩu.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.IsConfigured)
{
    builder.Services.AddSingleton<IAppEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IAppEmailSender, LoggingEmailSender>();
}

// Giới hạn số lần yêu cầu "quên mật khẩu" theo IP: 5 lần mỗi 15 phút.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
});
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IBudgetSuggestionService, BudgetSuggestionService>();
builder.Services.AddScoped<IRecurringExpenseService, RecurringExpenseService>();
builder.Services.AddScoped<IAccountDataService, AccountDataService>();
builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();
builder.Services.AddScoped<RecurringGenerationFilter>();
builder.Services.AddControllersWithViews(options => options.Filters.AddService<RecurringGenerationFilter>());

var app = builder.Build();

// Tự áp dụng migration + seed danh mục mặc định khi khởi động.
// Development: luôn bật. Production: chỉ bật khi đặt Database:MigrateOnStartup=true (biến môi trường Database__MigrateOnStartup).
var migrateOnStartup = builder.Configuration.GetValue<bool?>("Database:MigrateOnStartup") ?? app.Environment.IsDevelopment();
if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// Mặc định vi-VN: định dạng số, ngày theo kiểu Việt Nam.
var viVn = new CultureInfo("vi-VN");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(viVn),
    SupportedCultures = new[] { viVn },
    SupportedUICultures = new[] { viVn }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    // Các endpoint health check được gọi qua HTTP thường bởi nền tảng hosting nên không chuyển hướng sang HTTPS.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/healthz"),
        branch => branch.UseHttpsRedirection());
}

// Trang 404/403... thân thiện cho người dùng; bỏ qua /api/* để JS nhận đúng mã trạng thái.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/Home/HttpStatus/{0}"));

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Liveness: tiến trình còn sống. Readiness: kết nối được SQL Server.
app.MapGet("/healthz", () => Results.Text("Healthy"));
app.MapGet("/healthz/ready", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Text("Ready")
        : Results.Text("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Cần để project test có thể dùng WebApplicationFactory<Program> ở các bước sau.
public partial class Program { }
