# Spendly — Quản lý chi tiêu cá nhân

[![CI](https://github.com/dduong0402/Spendly/actions/workflows/ci.yml/badge.svg)](https://github.com/dduong0402/Spendly/actions/workflows/ci.yml)
[![Deploy](https://github.com/dduong0402/Spendly/actions/workflows/deploy.yml/badge.svg)](https://github.com/dduong0402/Spendly/actions/workflows/deploy.yml)

Website ghi lại dòng tiền ra mỗi ngày và thống kê theo ngày / tuần / tháng. Giao diện tiếng Việt.

> Đổi `dduong0402/Spendly` trong hai badge trên cho đúng tên repository của bạn.

**Demo:** _(điền địa chỉ sau khi deploy)_

## Tính năng
- Đăng ký, đăng nhập (ASP.NET Core Identity), quên/đổi mật khẩu qua email, xuất dữ liệu CSV/JSON, xóa tài khoản
- Danh mục chi tiêu (chọn emoji bất kỳ), khoản chi CRUD, lọc theo kỳ
- Dashboard thống kê ngày/tuần/tháng (Chart.js), chọn nhanh kỳ
- Hạn mức tuần/tháng (tổng và theo danh mục), cảnh báo 50/75/90/100%, gợi ý hạn mức từ lịch sử
- Khoản chi định kỳ tự sinh theo chu kỳ (sinh bù, không trùng lặp)

## Công nghệ
ASP.NET Core 8 MVC/Razor · EF Core 8 (Code First) · SQL Server · Tailwind CSS · Chart.js · xUnit + FluentAssertions · GitHub Actions · Azure App Service · Docker

## Chạy trên máy
```bash
dotnet restore
dotnet test
dotnet run --project src/Spendly.Web      # Development: tự migrate + seed
```
Cần SQL Server LocalDB/Express; chuỗi kết nối nằm trong `src/Spendly.Web/appsettings.Development.json`.

## Kiến trúc
`Controller → Service → DbContext`. Logic nghiệp vụ (kỳ thống kê, hạn mức, định kỳ, gợi ý) là các lớp thuần có unit test. Chi tiết trong [CLAUDE.md](CLAUDE.md).

## CI/CD
- `ci.yml`: build + test + build Docker image cho mỗi push/PR.
- `deploy.yml`: `dotnet publish` rồi deploy lên Azure App Service.
