# HTX586CONTRACT

Ứng dụng web quản lý và ký hợp đồng vận chuyển hành khách cho HTX 586, viết bằng ASP.NET Core Blazor Interactive Server trên .NET 9.

## Điểm chính

- Solution và namespace độc lập: `HTX586CONTRACT`.
- Quản lý hồ sơ HTX, tài xế, khách hàng, phương tiện và hợp đồng.
- Ký 4 bên: văn phòng HTX, chủ xe, khách hàng, tài xế.
- Khóa cập nhật/hủy hợp đồng theo trạng thái chữ ký.
- Sinh PDF 2 trang từ **PDF nền + layout JSON**.
- Runtime không cần Microsoft Word hoặc LibreOffice.
- Triển khai thuần ASP.NET Core Blazor trên IIS hoặc VPS chạy Kestrel/systemd.
- Không còn file triển khai kiểu container trong source.
- File upload/chữ ký/PDF được tách khỏi `wwwroot` qua cấu hình `FileStorage:UploadRootPath`.

## Chạy local

```powershell
dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
dotnet run --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj --launch-profile https

## User secrets

dotnet user-secrets init --project src/HTX586CONTRACT.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/HTX586CONTRACT.Web

dotnet clean
dotnet restore
dotnet build
dotnet ef migrations add AddCompanyProfile --project src/HTX586CONTRACT.Infrastructure --startup-project src/HTX586CONTRACT.Web
dotnet ef database update --project src/HTX586CONTRACT.Infrastructure --startup-project src/HTX586CONTRACT.Web
dotnet watch run /a --project src/HTX586CONTRACT.Web
```

Mặc định môi trường Development dùng:

```text
ConnectionStrings:Default = Server=localhost;Database=HTX586CONTRACT_DEV;Trusted_Connection=True;TrustServerCertificate=True;
Owner bootstrap = owner / Owner@123456
```

Nếu máy bạn dùng SQL Server khác, cấu hình lại bằng user-secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:Default" "Server=YOUR_SQL_SERVER;Database=HTX586CONTRACT;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;" --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj
```

## Cấu hình production

Ứng dụng đọc cấu hình chính từ `ConnectionStrings:Default`, `Seed:OwnerPassword` và `FileStorage:UploadRootPath`.

Ví dụ biến môi trường Windows/IIS:

```powershell
setx ConnectionStrings__Default "Server=YOUR_SQL_SERVER,1433;Database=HTX586CONTRACT;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;"
setx Seed__OwnerUserName "owner"
setx Seed__OwnerPassword "CHANGE_ME_WITH_A_STRONG_PASSWORD"
setx FileStorage__UploadRootPath "D:\HTX586CONTRACT_Data\uploads"
```

Có thể tham khảo file mẫu:

```text
src/HTX586CONTRACT.Web/appsettings.Production.example.json
```

Không lưu connection string/mật khẩu thật vào Git.

## Publish IIS hoặc VPS

Publish:

```powershell
dotnet publish .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj -c Release -o .\publish
```

Với IIS: cài ASP.NET Core Hosting Bundle đúng phiên bản .NET đang dùng, tạo Application Pool `No Managed Code`, trỏ website tới thư mục `publish`, và cấp quyền ghi cho thư mục upload tách riêng, ví dụ:

```text
D:\HTX586CONTRACT_Data\uploads
```

Với VPS chạy Kestrel/systemd: copy thư mục `publish` lên server, cấu hình biến môi trường, tạo thư mục upload riêng như `/var/lib/htx586contract/uploads`, rồi chạy `dotnet HTX586CONTRACT.Web.dll` sau reverse proxy Nginx/IIS ARR. Nếu chạy sau reverse proxy, đặt:

```text
ForwardedHeaders__Enabled=true
```

## Phân quyền

- `Owner`: quản lý tổng, được tạo từ seeding ban đầu. Owner tạo tài khoản `Admin`.
- `Admin`: được tạo kèm `CompanyProfile` và chữ ký cố định người đại diện.
- `Driver`: tài xế được gán vào `CompanyProfile` và có chữ ký cố định tài xế.
- Database mới ở Development tự tạo tài khoản bootstrap `owner / Owner@123456`.
- Production/Staging cần cấu hình `Seed:OwnerPassword` riêng để bootstrap an toàn.

## Template runtime

```text
src/HTX586CONTRACT.Web/Templates/Contracts/
├── HopDongVanChuyenHanhKhach.template.pdf
└── HopDongVanChuyenHanhKhach.layout.json
```

File thiết kế gốc nằm trong thư mục `design` và không tham gia vào runtime.

## Tài liệu triển khai

Xem [docs/DEPLOY_IIS_VPS.md](docs/DEPLOY_IIS_VPS.md), [docs/UPLOAD_STORAGE.md](docs/UPLOAD_STORAGE.md) và [HTX586CONTRACT_LATEST.md](HTX586CONTRACT_LATEST.md).
