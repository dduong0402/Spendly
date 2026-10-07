# CLAUDE.md — Spendly (Website quản lý chi tiêu cá nhân)

> Tên dự án `Spendly` chỉ là tên tạm, có thể đổi.
> File này là nguồn sự thật duy nhất về mục tiêu, kiến trúc, quy ước code và phong cách giao diện. Đọc kỹ trước khi viết bất kỳ dòng code nào.

## 1. Mục tiêu sản phẩm

Website giúp người dùng **ghi lại các khoản chi (dòng tiền ra) mỗi ngày** và **xem thống kê theo ngày / tuần / tháng**.

Phạm vi MVP (chỉ làm những thứ này):
- Đăng ký / đăng nhập, quên mật khẩu qua email, đổi mật khẩu, đổi tên hiển thị, xuất dữ liệu (CSV/JSON), xóa tài khoản.
- Thêm, sửa, xóa khoản chi (số tiền, danh mục, ngày, ghi chú).
- Quản lý danh mục chi tiêu (có sẵn mặc định + tự tạo).
- Dashboard thống kê: tổng chi theo ngày / tuần / tháng, biểu đồ xu hướng, tỷ trọng theo danh mục, top khoản chi lớn.
- Lọc và tìm kiếm danh sách chi tiêu (theo khoảng ngày, danh mục, từ khóa).
- Hạn mức chi tiêu theo tuần hoặc tháng: theo dõi mức đã dùng, cảnh báo kèm lời khuyên ở các mốc 50% - 75% - 90% - 100%; vượt hạn mức vẫn ghi chi tiêu bình thường nhưng có thông báo vượt hạn mức.
- Hạn mức theo từng danh mục (tuần hoặc tháng): dùng cùng các mốc cảnh báo; danh mục mặc định hoặc danh mục riêng đều đặt được.
- Khoản chi định kỳ (tiền nhà, internet, đăng ký dịch vụ): khai báo một lần (hằng tuần/tháng/năm), Spendly tự ghi khoản chi mỗi khi đến hạn, ghi bù đúng ngày nếu vắng mặt; tạm dừng/tiếp tục, ngày kết thúc tùy chọn.
- Gợi ý hạn mức từ lịch sử chi tiêu: khi đặt hạn mức tổng hoặc theo danh mục, hiện mức gợi ý "sát thực tế" và "tiết kiệm ~10%", bấm để điền vào ô nhập.

**Ngoài phạm vi MVP (không làm trừ khi được yêu cầu):** thu nhập, chia hóa đơn, đa tiền tệ, đồng bộ ngân hàng, app mobile, export báo cáo.

## 2. Tech stack

| Tầng | Công nghệ |
|---|---|
| Nền tảng | .NET 8 (LTS), C# 12 |
| Web | ASP.NET Core MVC (Razor Views) + một số endpoint JSON cho biểu đồ |
| ORM | Entity Framework Core 8 (Code First + Migrations) |
| Database | SQL Server (LocalDB hoặc SQL Server Express cài trực tiếp trên Windows, không dùng Docker) |
| Auth | ASP.NET Core Identity (cookie authentication) |
| Validation | DataAnnotations + FluentValidation (tùy chọn) |
| Mapping | Viết tay hoặc Mapperly, không dùng AutoMapper |
| Styling | Tailwind CSS (Tailwind standalone CLI, không cần Node cho build CSS) |
| Biểu đồ | Chart.js (tải qua libman hoặc npm, không dùng CDN ở production) |
| Tương tác nhẹ | Vanilla JS hoặc Alpine.js + `fetch` (không dùng SPA framework) |
| Test | xUnit + Moq + FluentAssertions; EF Core InMemory/SQLite cho test repository |
| IDE | Visual Studio Code |

Không thêm package mới nếu chưa cần. Nếu cần, nêu lý do trước khi cài.

## 3. Cấu trúc solution

```
Spendly/
├── CLAUDE.md
├── Spendly.sln
├── .editorconfig
├── README.md
├── Dockerfile
├── .dockerignore
├── .github/workflows/             # ci.yml (build + test + docker build thử), deploy.yml (dotnet publish → Azure)
├── .vscode/
│   ├── launch.json
│   ├── tasks.json
│   └── extensions.json
├── src/
│   └── Spendly.Web/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Domain/                  # Entities, enums (Expense, Category, ApplicationUser)
│       ├── Data/                    # AppDbContext, Configurations/, Migrations/, Seed/
│       ├── Services/                # IExpenseService, ExpenseService, IStatsService, StatsService, ...
│       ├── Models/                  # ViewModels, DTOs (record), Requests
│       ├── Controllers/             # AccountController, DashboardController, ExpensesController, CategoriesController, StatsController (JSON)
│       ├── Views/                   # Razor: Shared/_Layout, Dashboard, Expenses, Categories, Account
│       ├── Common/                  # Extensions, helpers (DateRangeCalculator, MoneyFormatter)
│       ├── Styles/input.css         # nguồn Tailwind
│       ├── tailwind.config.js
│       └── wwwroot/                 # css (output), js, lib
└── tests/
    └── Spendly.Tests/
```

Quy tắc phân lớp: `Controller → Service → DbContext`. Controller mỏng, logic nghiệp vụ nằm trong Service. Không viết truy vấn EF trực tiếp trong Controller hoặc View.

## 4. Domain & quy tắc nghiệp vụ

### Mô hình dữ liệu

- `ApplicationUser : IdentityUser`: DisplayName, CreatedAt
- `Category`: Id, UserId (null = danh mục mặc định hệ thống), Name, Color, Icon (đúng MỘT emoji bất kỳ do người dùng chọn)
- `Expense`: Id, UserId, CategoryId, Amount, SpentAt (`DateOnly`), Note, RecurringExpenseId (null nếu nhập tay), CreatedAt, UpdatedAt. Duy nhất theo (RecurringExpenseId, SpentAt) khi RecurringExpenseId khác null.
- `Budget`: Id, UserId, Period (Week | Month, lưu dạng chữ), Amount, CreatedAt, UpdatedAt. Duy nhất theo (UserId, Period): mỗi người tối đa 1 hạn mức tuần và 1 hạn mức tháng; Amount > 0.
- `RecurringExpense`: Id, UserId, CategoryId, Name, Amount, Frequency (Weekly | Monthly | Yearly, lưu dạng chữ), StartDate, EndDate (null = không kết thúc), NextDueDate, IsActive, CreatedAt, UpdatedAt. Amount > 0; FK tới Category là Restrict (CategoryService chặn xóa danh mục đang dùng); FK từ Expense là NoAction (RecurringExpenseService gỡ liên kết rồi mới xóa quy tắc, các khoản chi đã ghi được giữ lại).
- `CategoryBudget`: Id, UserId, CategoryId, Period (Week | Month), Amount, CreatedAt, UpdatedAt. Duy nhất theo (UserId, CategoryId, Period); Amount > 0; FK tới Category là Restrict (CategoryService xóa hạn mức của danh mục trước khi xóa danh mục).

### Quy tắc

- Tiền tệ duy nhất: **VND**. `Amount` kiểu `long` (đơn vị đồng), cột SQL `bigint`. **Không dùng float/double.** Nếu sau này cần thập phân thì dùng `decimal`.
- `Amount` phải > 0.
- Múi giờ: `Asia/Ho_Chi_Minh`. Không gọi `DateTime.Now` / `DateOnly.FromDateTime(DateTime.Now)` trực tiếp trong logic, hãy inject `TimeProvider` để dễ test.
- **Tuần bắt đầu từ Thứ Hai** (ISO week). Viết một helper duy nhất `DateRangeCalculator` trả về `(DateOnly From, DateOnly To)` cho `day | week | month`, và dùng nó ở mọi nơi.
- Mọi truy vấn **phải lọc theo UserId lấy từ `User` claims** (`UserManager`/`ClaimTypes.NameIdentifier`). Không bao giờ tin UserId từ form hay query string. Cân nhắc dùng EF Global Query Filter hoặc một `ICurrentUser` service.
- Sửa/xóa expense hoặc category: kiểm tra quyền sở hữu, trả 404 nếu không phải của user (không tiết lộ tồn tại).
- Xóa danh mục đang có khoản chi: chặn lại và báo lỗi, hoặc yêu cầu chuyển sang danh mục khác. Không xóa cascade.
- Icon danh mục: lưu thẳng ký tự emoji (tối đa 30 ký tự UTF-16), hợp lệ khi là đúng 1 grapheme có ký hiệu (`CategoryIcons.IsValid`). Khóa icon cũ ("utensils"...) vẫn đọc được qua `CategoryIcons.Resolve` và được `DbSeeder` đổi sang emoji.
- Hạn mức: luôn tính cho kỳ HIỆN TẠI (tuần Thứ Hai–Chủ Nhật hoặc tháng dương lịch, giờ Việt Nam). Mốc cảnh báo dùng số nguyên: <50% an toàn, ≥50%, ≥75%, ≥90%, =100% (chạm), >100% (vượt). Lời khuyên gồm: số tiền nên chi mỗi ngày còn lại, cảnh báo chi nhanh hơn tiến độ, dự báo cả kỳ (khi đã qua ≥3 ngày) và danh mục chi nhiều nhất. Sau khi thêm/sửa khoản chi: báo khi chạm mốc cao hơn (một thông báo cho mốc cao nhất), và báo vượt hạn mức mỗi lần khoản chi làm tổng chi tăng khi đang vượt. Không bao giờ chặn việc ghi chi tiêu.
- Khoản chi định kỳ (`RecurringExpenseService`, `RecurrenceCalculator`): ngày đến hạn luôn tính từ `StartDate` (neo) nên 31/01 → 28/02 → 31/03 (không trôi); năm nhuận 29/02 → 28/02 rồi lại 29/02. `RecurringGenerationFilter` (filter toàn cục, chỉ GET của người dùng đã đăng nhập, lỗi không làm hỏng trang) gọi `GenerateDueAsync` trước mỗi lần xem trang: tạo `Expense` (SpentAt = ngày đến hạn, Note = Name) cho mọi lần đến hạn đã qua, tối đa 500 khoản mỗi lần, rồi dời `NextDueDate`. Chống ghi trùng bằng chỉ mục duy nhất (RecurringExpenseId, SpentAt). Ngày bắt đầu cho phép lùi tối đa 366 ngày (ghi bù) và tiến tối đa 366 ngày. Sửa chỉ đổi được tên, số tiền (áp dụng cho các lần sau), danh mục, ngày kết thúc; không đổi được chu kỳ và ngày bắt đầu. Tiếp tục sau khi tạm dừng (hoặc kéo dài quy tắc đã kết thúc) bỏ qua các lần đã lỡ. Xóa khoản chi đã ghi không làm nó được ghi lại; xóa quy tắc không xóa khoản chi. Khoản chi tự tạo không sinh cảnh báo hạn mức nhưng được tính vào hạn mức và thống kê. Ước tính mỗi tháng: tuần ×52/12, năm ÷12.
- Tài khoản: quên mật khẩu KHÔNG tiết lộ email nào đã đăng ký (cùng phản hồi dù có hay không; lỗi gửi mail chỉ ghi log). Token đặt lại mật khẩu sống 1 giờ, dùng một lần (đổi security stamp). Link trong email dựng từ `App:PublicBaseUrl` (chống giả mạo header Host; môi trường thật BẮT BUỘC đặt). Đổi/đặt lại mật khẩu đổi security stamp, `SecurityStampValidatorOptions.ValidationInterval` = 1 phút nên thiết bị khác bị đăng xuất trong vòng 1 phút. Đổi mật khẩu và xóa tài khoản đều kiểm tra mật khẩu hiện tại qua `CheckPasswordSignInAsync(lockoutOnFailure: true)`. Xóa tài khoản chạy trong một giao dịch theo thứ tự khoản chi → khoản định kỳ → hạn mức danh mục → hạn mức tổng → danh mục riêng → người dùng. Xuất CSV chặn CSV injection (ô bắt đầu bằng = + - @ được thêm dấu nháy đơn), số tiền xuất dạng số nguyên, ngày dạng yyyy-MM-dd.
- Email: `IAppEmailSender`; có `Email:Host` thì `SmtpEmailSender`, chưa có thì `LoggingEmailSender` (Development in nội dung email ra console, môi trường khác chỉ ghi lỗi, không bao giờ ghi nội dung). Mật khẩu SMTP đặt bằng User Secrets (`Email:Password`) hoặc biến môi trường `Email__Password`, KHÔNG đưa vào appsettings.
- Gợi ý hạn mức (`BudgetSuggestionCalculator`, `BudgetSuggestionService`): lấy tối đa 3 tuần/tháng đã TRỌN VẸN gần nhất (không tính kỳ hiện tại), bỏ kỳ bắt đầu trước khoản chi đầu tiên của người dùng; kỳ không chi vẫn tính là 0. Gợi ý = trung bình làm tròn LÊN theo bước (<100k: 10k, <1tr: 50k, <10tr: 100k, còn lại 500k); mức tiết kiệm = 90% trung bình làm tròn gần nhất, chỉ hiện khi > 0 và thấp hơn gợi ý; không vượt `ExpenseRules.MaxAmount`. Không có bảng mới, tính trực tiếp từ `Expenses`. Gợi ý chỉ điền số vào ô nhập, người dùng vẫn phải bấm lưu.
- Hạn mức theo danh mục: tính trên các khoản chi thuộc đúng danh mục đó trong tuần/tháng hiện tại, dùng chung `BudgetEvaluator` (cùng mốc, cùng lời khuyên; nhãn dạng "Hạn mức tuần · Ăn uống"). Một lần lưu khoản chi có thể chạm nhiều hạn mức: chỉ hiện tối đa 3 thông báo, mốc cao hơn trước (`BudgetAlertPolicy.MaxAlerts`). Chỉ đặt được cho danh mục mặc định hoặc của chính người dùng.
- Danh mục mặc định (seed): Ăn uống, Di chuyển, Nhà ở, Hóa đơn, Mua sắm, Giải trí, Sức khỏe, Giáo dục, Khác.

## 5. Phong cách giao diện (bám theo ảnh tham chiếu Billow)

Cảm giác chung: **sáng, sạch, hiện đại, nhiều khoảng trắng**, nền trắng với các mảng gradient xanh da trời mềm như đám mây phía sau các khối dashboard. Tiêu đề lớn, đậm, chữ Title Case. Card bo góc lớn, viền mảnh, đổ bóng rất nhẹ. Nút CTA màu xanh dương đậm, dạng pill hoặc bo tròn.

> Mã màu bên dưới là ước lượng từ ảnh, có thể tinh chỉnh khi dựng thực tế.

### Design tokens (đưa vào `tailwind.config.js` và CSS variables trong `Styles/input.css`)

```
Màu
--bg:            #FFFFFF
--bg-soft:       #F5F8FF      /* nền section xen kẽ */
--surface:       #FFFFFF      /* card */
--border:        #E6EBF3
--text:          #0B1B33      /* xanh navy rất đậm, dùng cho heading */
--text-muted:    #5B6B84
--primary:       #1F6BFF      /* nút chính, link, điểm nhấn biểu đồ */
--primary-hover: #1657D6
--primary-soft:  #E8F0FF      /* badge, nền chip */
--sky-1:         #CFE3FF      /* gradient mây */
--sky-2:         #8EBBFF
--sky-3:         #EAF3FF
--success:       #16A34A
--danger:        #E5484D
--warning:       #F59E0B

Gradient "mây" (nền hero và sau dashboard preview)
radial-gradient(60% 50% at 50% 40%, #8EBBFF 0%, #CFE3FF 45%, #FFFFFF 100%)

Bo góc:  card 16px · nút 12px (CTA chính có thể pill 999px) · input 10px
Bóng:    0 1px 2px rgba(11,27,51,.04), 0 8px 24px rgba(31,107,255,.08)
```

### Typography

- Font chính: **Inter** (fallback: `ui-sans-serif, system-ui, sans-serif`). Có thể thử **Plus Jakarta Sans** cho heading. Chọn một và dùng nhất quán. Tự host font trong `wwwroot/fonts` hoặc Google Fonts, bắt buộc hỗ trợ subset `vietnamese`.
- Heading: đậm (700–800), `letter-spacing: -0.02em`, line-height chặt (1.1–1.15).
  - H1 hero: 48–64px (mobile 36–40px)
  - H2 section: 32–40px
  - H3 card: 18–20px
- Body: 16px, line-height 1.6, màu `--text-muted`.
- Số tiền và số liệu: `font-variant-numeric: tabular-nums`, đậm 600–700.

### Thành phần UI (làm bằng Razor Partial View / View Component + class Tailwind)

- **Button**: primary (nền `--primary`, chữ trắng), secondary (viền `--border`, nền trắng), ghost. Có hover/focus/disabled/loading.
- **Card**: nền trắng, viền `--border`, bo 16px, padding 20–24px, bóng nhẹ.
- **Badge/Chip**: pill nhỏ, nền `--primary-soft`, chữ `--primary`, dùng cho danh mục và nhãn "Hôm nay", "Tuần này".
- **Tab Ngày / Tuần / Tháng**: segmented control bo tròn, tab đang chọn nền trắng + bóng nhẹ trên nền `--bg-soft`.
- **Biểu đồ (Chart.js)**: đường/cột chính màu `--primary`, lưới mờ, tooltip dạng card trắng bo góc. Donut theo danh mục dùng dải xanh dương + vài màu phụ hài hòa, không chói.
- **Layout**: container tối đa 1200px, khoảng cách section lớn (80–120px trên landing, 24–32px trong app), header dính trên cùng nền trắng mờ (`backdrop-blur`).
- **Motion**: nhẹ, 150–250ms ease-out.

### Nguyên tắc giao diện

- Responsive mobile-first (≥ 360px). Bảng chi tiêu trên mobile chuyển thành danh sách thẻ.
- Tương phản tối thiểu WCAG AA. Focus ring rõ (`--primary`).
- Nội dung hiển thị bằng **tiếng Việt**. Format tiền: `1.250.000 ₫` (`CultureInfo("vi-VN")`), ngày: `dd/MM/yyyy`. Cấu hình `RequestLocalization` mặc định `vi-VN`.
- Đủ trạng thái: loading (skeleton), empty ("Thêm khoản chi đầu tiên"), error.

## 6. Màn hình

1. **Landing** (đã làm): hero + dashboard minh họa, tính năng, ba bước bắt đầu, câu hỏi thường gặp, kêu gọi hành động. Ý tưởng ban đầu: hero "Theo dõi chi tiêu. Rõ ràng từng đồng." + nền mây + ảnh preview dashboard + CTA.
2. **Đăng nhập / Đăng ký / Quên mật khẩu / Đặt lại mật khẩu**.
3. **Dashboard**:
   - Tab Ngày / Tuần / Tháng + bộ chọn kỳ (mũi tên trước/sau) và chọn nhanh: tháng chọn qua lưới 12 tháng + chọn năm, ngày/tuần chọn qua ô ngày.
   - Thẻ tổng: tổng chi kỳ này, so với kỳ trước (% tăng/giảm), trung bình mỗi ngày, số giao dịch.
   - Biểu đồ xu hướng, donut theo danh mục, danh sách khoản chi gần nhất.
4. **Chi tiêu**: danh sách có lọc, tìm kiếm, phân trang; nút "Thêm khoản chi" mở modal/trang form.
5. **Danh mục**: quản lý danh mục (tên, màu, icon).
6. **Hạn mức**: đặt/cập nhật/xóa hạn mức tuần và tháng, xem tiến độ, cảnh báo và lời khuyên. Dashboard có khối Hạn mức (tuần/tháng hiện tại) phía trên phần thống kê. Có thêm phần Hạn mức theo danh mục (thêm/sửa/xóa) và khối tóm tắt trên Dashboard. Mỗi ô nhập hạn mức có khung "Gợi ý theo lịch sử" (trung bình các kỳ đã qua + hai nút điền nhanh).
7. **Định kỳ**: danh sách khoản chi định kỳ (ước tính mỗi tháng, lần tới, số khoản đã ghi), thêm/sửa/tạm dừng-tiếp tục/xóa. Trang Chi tiêu gắn nhãn "Định kỳ" cho khoản chi tự tạo.
8. **Tài khoản** (`/Manage`): thông tin và đổi tên hiển thị, đổi mật khẩu, tải dữ liệu (CSV cho Excel, JSON đầy đủ), xóa tài khoản (có xác nhận bằng mật khẩu).

## 7. Routes & endpoint

MVC (trả View):
```
GET/POST /Account/Register
GET/POST /Account/Login
POST     /Account/Logout

GET      /                         → Dashboard (?period=day|week|month&date=yyyy-MM-dd)
GET      /Expenses                 → danh sách (?from=&to=&categoryId=&q=&page=)
GET/POST /Expenses/Create
GET/POST /Expenses/Edit/{id}
POST     /Expenses/Delete/{id}
GET/POST /Categories/...           → CRUD tương tự
GET      /Budgets                  → xem và quản lý hạn mức
POST     /Budgets/Set              → period=Week|Month, amountInput
POST     /Budgets/Delete           → period=Week|Month
POST     /Budgets/SetCategory      → categoryId, period=Week|Month, amountInput
POST     /Budgets/DeleteCategory   → categoryId, period=Week|Month
GET/POST /Account/ForgotPassword  → nhập email; luôn trả cùng một kết quả; giới hạn 5 lần / 15 phút / IP
GET      /Account/ForgotPasswordConfirmation
GET/POST /Account/ResetPassword    → ?email=&token= (liên kết trong email, hiệu lực 1 giờ, dùng một lần)
GET      /Account/ResetPasswordConfirmation
GET      /Manage                   → trang Tài khoản
POST     /Manage/UpdateProfile     → DisplayName
GET/POST /Manage/ChangePassword
GET      /Manage/ExportCsv         → tải CSV khoản chi (UTF-8 BOM + dòng sep=,)
GET      /Manage/ExportJson        → tải toàn bộ dữ liệu
GET/POST /Manage/Delete            → xóa tài khoản, cần nhập mật khẩu
GET      /Recurring                → danh sách khoản chi định kỳ
GET/POST /Recurring/Create
GET/POST /Recurring/Edit/{id}
POST     /Recurring/Toggle/{id}    → active=true|false (tạm dừng / tiếp tục)
POST     /Recurring/Delete/{id}
```

JSON cho biểu đồ (cần đăng nhập, `[Authorize]`):
```
GET /api/stats/summary?period=&date=
GET /api/stats/trend?period=&date=
GET /api/stats/by-category?period=&date=
GET /api/stats/top-expenses?period=&date=&take=5
GET /api/stats/recent-expenses?period=&date=&take=5
```

Quy ước:
- `period` + `date` xác định kỳ: `day` = đúng ngày đó; `week` = Thứ Hai → Chủ Nhật chứa ngày đó; `month` = cả tháng chứa ngày đó. Mặc định: `week`, hôm nay.
- `trend`: `week` → 7 điểm; `month` → từng ngày trong tháng; `day` → 7 ngày kết thúc tại ngày đó (một điểm duy nhất thì biểu đồ vô nghĩa). Ngày không có chi tiêu vẫn trả về `amount = 0` để biểu đồ liền mạch; ngày chưa tới có `isFuture = true` và biểu đồ để trống.
- `summary`: trung bình mỗi ngày tính trên số ngày đã trôi qua của kỳ (kỳ đang diễn ra) hoặc cả kỳ (kỳ đã kết thúc). Kỳ đang diễn ra được so sánh với cùng số ngày đầu của kỳ trước; kỳ đã kết thúc so với cả kỳ trước.
- Nền giao diện: một lớp gradient "mây" cố định (`body::before`) dùng chung cho mọi trang, không đặt `cloud-bg` riêng trên từng section.
- Thống kê tính bằng **truy vấn EF `GroupBy` + `SumAsync` dịch xuống SQL**, không `ToList()` rồi cộng trong bộ nhớ.
- Form POST bắt buộc có `[ValidateAntiForgeryToken]`. Kiểm tra `ModelState.IsValid`. Dùng Post-Redirect-Get.
- Dùng ViewModel/DTO riêng cho View, **không truyền Entity thẳng ra View** hoặc JSON.
- Lỗi JSON dùng `ProblemDetails`. Lỗi trang dùng trang lỗi thân thiện + `UseExceptionHandler`.

## 8. Quy ước code

### C# / ASP.NET Core
- `Nullable` bật (`<Nullable>enable</Nullable>`), `ImplicitUsings` bật, `TreatWarningsAsErrors` cho project chính nếu khả thi.
- Dùng `record` cho DTO, file-scoped namespace, primary constructor khi hợp lý.
- Dependency Injection qua constructor. Đăng ký service trong `Program.cs` (có thể gom vào extension method `AddSpendlyServices`).
- Mọi phương thức truy cập DB dùng `async/await` + `CancellationToken`, hậu tố `Async`.
- Truy vấn chỉ đọc dùng `AsNoTracking()`. Tránh N+1 (dùng `Include`/projection `Select`).
- Cấu hình entity bằng Fluent API trong `Data/Configurations/` (`IEntityTypeConfiguration<T>`), không nhồi attribute vào entity.
- Đặt tên: `ExpensesController`, `IExpenseService`, `ExpenseService`, `CreateExpenseRequest`, `ExpenseListItemViewModel`.
- Secret (connection string thật, key) dùng **User Secrets** khi dev và biến môi trường khi deploy. **Không commit** vào `appsettings.json`.
- Migration: mỗi thay đổi schema tạo một migration có tên rõ nghĩa (`dotnet ef migrations add AddNoteToExpense`). Không sửa migration đã áp dụng.
- Viết unit test cho `DateRangeCalculator` và `StatsService` (các trường hợp biên: đầu/cuối tuần, đầu/cuối tháng, năm nhuận, tháng không có dữ liệu). Viết integration test cho luồng thêm/sửa/xóa expense.

### Razor & Frontend
- Dùng Tag Helpers (`asp-for`, `asp-action`, `asp-validation-for`), không hard-code URL.
- Tách phần lặp thành Partial View / View Component (`_ExpenseCard`, `_PeriodTabs`, `_StatCard`).
- Style bằng Tailwind theo tokens ở mục 5; **không hardcode mã màu** trong View, dùng class/token.
- JS đặt trong `wwwroot/js`, tách theo trang (`dashboard.js`, `expenses.js`), không viết script dài inline trong View.
- Xác nhận hành động nguy hiểm (xóa): đặt `data-confirm="..."` trên `<form>`; `site.js` hiện hộp thoại `<dialog class="confirm-dialog">` (tùy chọn `data-confirm-title`, `data-confirm-ok`, `data-confirm-note`), KHÔNG dùng `window.confirm`. Chỉ ghi nội dung bằng `textContent`.
- Nút điền nhanh: `data-fill-target="#id" data-fill-value="123"` điền giá trị vào ô nhập.

### Chung
- Commit theo Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `test:`).
- Tên biến, hàm, bảng, cột, route bằng tiếng Anh. Chỉ nội dung hiển thị cho người dùng bằng tiếng Việt.
- Không commit `.env`, secret, dữ liệu thật. Cung cấp `appsettings.Development.json.example` nếu cần.

## 9. Thiết lập môi trường với VS Code

Yêu cầu: .NET 8 SDK, SQL Server LocalDB hoặc Express (+ SSMS để xem dữ liệu), Git, `dotnet-ef` tool.

Extension khuyến nghị (`.vscode/extensions.json`):
- `ms-dotnettools.csdevkit` (C# Dev Kit, kéo theo C#)
- `ms-dotnettools.vscode-dotnet-runtime`
- `bradlc.vscode-tailwindcss` (Tailwind CSS IntelliSense)
- `humao.rest-client` (thử endpoint JSON bằng file `.http`)
- `editorconfig.editorconfig`

Cấu hình gợi ý:
- `.vscode/launch.json`: cấu hình `.NET Launch` trỏ tới `src/Spendly.Web`, `ASPNETCORE_ENVIRONMENT=Development`, tự mở trình duyệt.
- `.vscode/tasks.json`: task `build`, `watch` (`dotnet watch run`), `tailwind` (chạy Tailwind ở chế độ `--watch`), và task tổng chạy song song `watch` + `tailwind`.
- `.editorconfig`: indent 4 spaces cho C#, 2 spaces cho cshtml/js/css, `end_of_line = lf`, `charset = utf-8`.

## 10. Lệnh thường dùng

```bash
# Tạo solution & project (chỉ lần đầu)
dotnet new sln -n Spendly
dotnet new mvc -n Spendly.Web -o src/Spendly.Web --auth Individual
dotnet new xunit -n Spendly.Tests -o tests/Spendly.Tests
dotnet sln add src/Spendly.Web tests/Spendly.Tests
dotnet add tests/Spendly.Tests reference src/Spendly.Web

# Database
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> -p src/Spendly.Web
dotnet ef database update -p src/Spendly.Web

# Chạy & test
dotnet watch run --project src/Spendly.Web
dotnet test
dotnet format --verify-no-changes

# Tailwind (chạy trong src/Spendly.Web)
tailwindcss -i ./Styles/input.css -o ./wwwroot/css/site.css --watch
tailwindcss -i ./Styles/input.css -o ./wwwroot/css/site.css --minify
```

## 11. Cách làm việc với Claude

- Làm **từng bước nhỏ**, mỗi bước build và chạy được. Thứ tự đề xuất:
  1. Khởi tạo solution, cấu hình SQL Server cục bộ, cấu hình VS Code, Tailwind, layout `_Layout` theo design tokens.
  2. Entities + `AppDbContext` + migration đầu tiên + seed danh mục mặc định.
  3. Auth (Identity): đăng ký, đăng nhập, đăng xuất.
  4. Category + Expense CRUD (Controller, Service, View).
  5. `DateRangeCalculator` + `StatsService` (kèm unit test) + API JSON thống kê.
  6. Dashboard với Chart.js.
  7. Trau chuốt UI theo mục 5, responsive, empty/loading/error states.
  8. Landing page.
- Trước khi thay đổi lớn (đổi schema, thêm package, đổi cấu trúc), nêu kế hoạch ngắn và chờ xác nhận.
- Khi sửa code có sẵn, sửa trực tiếp và chỉ ra file/đoạn đã đổi, không giải thích lan man.
- Nếu yêu cầu mâu thuẫn với file này, hỏi lại thay vì tự đoán.
- Sau mỗi thay đổi: chạy `dotnet build` và `dotnet test`, báo kết quả.

## 12. Triển khai & chất lượng (bước 13)

- **CI** (`.github/workflows/ci.yml`): mỗi push/PR vào `main` chạy restore → build Release → `dotnet test` (TRX + coverage upload artifact) → build thử Docker image. Test dùng SQLite in-memory nên CI không cần SQL Server.
- **Dockerfile** (chỉ build thử trong CI; máy người dùng không chạy được Docker) nhiều giai đoạn (`sdk:8.0` → `aspnet:8.0`), chạy user `app`, cổng 8080, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. Tailwind CSS đã build sẵn trong repo nên image không cần Node.
- **CD** (`.github/workflows/deploy.yml`): sau khi CI xanh trên `main` → `dotnet publish` → đẩy code lên Azure App Service (Linux, runtime .NET 8, KHÔNG dùng container); chỉ chạy khi có Variable `AZURE_WEBAPP_NAME` + Secret `AZURE_WEBAPP_PUBLISH_PROFILE`.
- **Hosting mặc định**: Azure App Service (Linux, deploy code) + Azure SQL Database. Provider DB vẫn là SQL Server.
- **Cấu hình production qua biến môi trường** (không bao giờ commit secret): `ConnectionStrings__DefaultConnection`, `Database__MigrateOnStartup` (true = tự migrate + seed khi khởi động; mặc định chỉ bật ở Development), `DataProtection__KeysPath` (thư mục bền vững, Azure: `/home/dp-keys`), `App__PublicBaseUrl`, `Email__*`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.
- **Health check**: `/healthz` (liveness), `/healthz/ready` (kiểm tra kết nối DB, 503 nếu lỗi). Không bị chuyển hướng HTTPS.
- Không được commit `appsettings.Development.json` có mật khẩu thật; `.dockerignore` loại file này khỏi image.
