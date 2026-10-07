using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Spendly.Web.Data;
using Spendly.Web.Domain;

namespace Spendly.Web.Auth;

public static class IdentityServiceCollectionExtensions
{
    /// <summary>Thời gian hiệu lực của liên kết đặt lại mật khẩu.</summary>
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);

    /// <summary>Cấu hình ASP.NET Core Identity + cookie đăng nhập cho Spendly.</summary>
    public static IServiceCollection AddSpendlyIdentity(this IServiceCollection services, bool isDevelopment)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddErrorDescriber<VietnameseIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        // Liên kết đặt lại mật khẩu chỉ có hiệu lực 1 giờ (mặc định của Identity là 1 ngày).
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = PasswordResetLifetime);

        // Kiểm tra security stamp mỗi phút: đổi hoặc đặt lại mật khẩu sẽ đăng xuất các thiết bị khác trong vòng 1 phút (mặc định 30 phút).
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Cookie.Name = "Spendly.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // Development chạy http nên cho phép SameAsRequest; môi trường thật bắt buộc HTTPS.
            options.Cookie.SecurePolicy = isDevelopment
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            // Yêu cầu tới /api/* (fetch từ JS) cần mã 401/403 thay vì bị chuyển hướng sang trang đăng nhập.
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        return services;
    }
}
