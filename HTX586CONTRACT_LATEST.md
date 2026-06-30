# HTX586CONTRACT - Tài liệu triển khai mới nhất

**Phiên bản kiến trúc:** PDF template cố định + layout JSON  
**Tên solution:** `HTX586CONTRACT`  
**Nền tảng:** ASP.NET Core / Blazor Web App Interactive Server / .NET 9 / SQL Server

---

## Cập nhật phân quyền Owner/Admin/Driver

- Thêm role `Owner` cho tài khoản quản lý tổng.
- Seeding tạo role `Owner`, `Admin`, `Driver`; Development có tài khoản bootstrap mặc định `owner / Owner@123456`, Production/Staging nên dùng `Seed:OwnerPassword`.
- Không seed CompanyProfile mặc định. Owner tạo Admin thì hệ thống tạo CompanyProfile và chữ ký cố định người đại diện cho Admin đó.
- Admin và Driver được gán CompanyProfile để Contract lấy đúng chữ ký/đơn vị.

## 1. Mục tiêu của bản thiết kế lại

Project này được tách thành một solution riêng, đổi toàn bộ tên project và namespace từ hệ thống cũ sang `HTX586CONTRACT`.

Luồng xuất hợp đồng không còn phụ thuộc vào Microsoft Word hoặc LibreOffice trên máy chủ. Web chỉ cần:

1. Đọc PDF nền 2 trang.
2. Đọc file JSON chứa tọa độ các trường.
3. Lấy dữ liệu hợp đồng từ SQL Server.
4. Vẽ chữ tiếng Việt và chèn ảnh chữ ký lên PDF nền.
5. Lưu PDF hoàn chỉnh vào `FileStorage:UploadRootPath/contracts`.
6. Ghi URL, SHA-256 và thời gian sinh PDF trở lại bảng `Contracts`.

Luồng runtime:

```text
SQL Server
   ↓
Contract + CompanyProfile + Customer + Vehicle + Driver
   ↓
ContractPassengers + ContractSignatures
   ↓
HopDongVanChuyenHanhKhach.template.pdf
   + HopDongVanChuyenHanhKhach.layout.json
   ↓
PdfContractTemplateRenderer
   ↓
PDF hợp đồng hoàn chỉnh 2 trang
```

**Không có bước DOCX → PDF khi website đang chạy.**

---

## 2. Các file template

### File dùng trong production

```text
src/HTX586CONTRACT.Web/Templates/Contracts/
├── HopDongVanChuyenHanhKhach.template.pdf
└── HopDongVanChuyenHanhKhach.layout.json
```

- `template.pdf`: PDF nền cố định gồm 2 trang A4.
- `layout.json`: tọa độ, kích thước, font, căn lề và khóa dữ liệu cần điền.

Hai file này được tự động sao chép vào thư mục publish.

### File chỉ dùng để thiết kế

```text
design/
└── Template.xlsx
```

Các file trong `design` không được website đọc khi chạy. Chúng chỉ là nguồn thiết kế mẫu. Khi thay đổi nội dung tĩnh, người phát triển xuất lại một PDF nền và cập nhật layout JSON nếu vị trí trường thay đổi. Khi thay đổi nội dung tĩnh, người phát triển xuất lại một PDF nền và cập nhật layout JSON nếu vị trí trường thay đổi.

---

## 3. Cấu trúc solution

```text
HTX586CONTRACT/
├── HTX586CONTRACT.slnx
├── global.json
├── README.md
├── HTX586CONTRACT_LATEST.md
├── database/
│   └── 20260626_ver6.sql
├── design/
│   └── Template.xlsx
├── docs/
│   └── DEPLOY_IIS_VPS.md
├── scripts/
│   ├── publish.ps1
│   └── publish.sh
└── src/
    ├── HTX586CONTRACT.Domain/
    ├── HTX586CONTRACT.Application/
    ├── HTX586CONTRACT.Infrastructure/
    └── HTX586CONTRACT.Web/
```

### `HTX586CONTRACT.Domain`

Chứa entity và enum:

- `ApplicationUser`
- `CompanyProfile`
- `Customer`
- `Vehicle`
- `Contract`
- `ContractPassenger`
- `ContractSignature`
- `ContractAttachment`
- `ContractAuditLog`
- `ContractStatus`
- `SignatureParty`

### `HTX586CONTRACT.Application`

Chứa DTO và interface nghiệp vụ:

- Quản lý tài khoản.
- Quản lý hồ sơ tài xế.
- Quản lý hồ sơ HTX.
- Quản lý hợp đồng.
- Lưu chữ ký và sinh PDF.

### `HTX586CONTRACT.Infrastructure`

Chứa:

- `ApplicationDbContext`.
- Entity Framework Core SQL Server.
- ASP.NET Core Identity.
- Các service nghiệp vụ.
- Seeder và script nâng cấp database idempotent.

### `HTX586CONTRACT.Web`

Chứa:

- Blazor Web App Interactive Server.
- Giao diện Admin và Driver.
- Quy trình bắt buộc đổi mật khẩu.
- Quản lý khách hàng, phương tiện và hợp đồng.
- Canvas ký điện tử.
- `PdfContractTemplateRenderer`.
- `ContractDocumentService`.

---

## 4. Công nghệ và package chính

```text
.NET SDK                         9.0
Target Framework                 net9.0
MudBlazor                        9.5.0
Entity Framework Core           9.0.6
PDFsharp                         6.2.4
SkiaSharp                        2.88.9
SkiaSharp.NativeAssets.Linux     2.88.9
SkiaSharp.NativeAssets.Win32     2.88.9
SQL Server
```

- PDFsharp dùng để mở PDF nền và chèn ảnh vào từng trang.
- SkiaSharp dùng để render chữ tiếng Việt thành ảnh PNG trong suốt trước khi đặt lên PDF. Cách này tránh lỗi font tiếng Việt khi chạy khác hệ điều hành.
- Không dùng `DocumentFormat.OpenXml` trong luồng xuất PDF.
- Không dùng `Process.Start`, `soffice.exe`, Microsoft Office hoặc LibreOffice trong runtime.

---

## 5. Cấu hình ứng dụng

File:

```text
src/HTX586CONTRACT.Web/appsettings.json
```

Cấu hình template:

```json
{
  "DocumentGeneration": {
    "ContractTemplatePath": "Templates/Contracts/HopDongVanChuyenHanhKhach.template.pdf",
    "ContractLayoutPath": "Templates/Contracts/HopDongVanChuyenHanhKhach.layout.json"
  }
}
```

Không còn các cấu hình:

```text
LibreOfficePath
ConvertTimeoutSeconds
```

### Connection string

Không lưu mật khẩu database trong source code. Dùng một trong các phương án sau.

#### User Secrets khi phát triển

```powershell
dotnet user-secrets set `
  "ConnectionStrings:Default" `
  "Server=localhost;Database=HTX586CONTRACT;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True" `
  --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj

dotnet user-secrets set `
  "Seed:OwnerUserName" `
  "owner" `
  --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj

dotnet user-secrets set `
  "Seed:OwnerPassword" `
  "YOUR_INITIAL_OWNER_PASSWORD" `
  --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj
```

`Seed:OwnerPassword` chỉ được dùng khi database chưa có tài khoản Owner. Ở Development có fallback `owner / Owner@123456`. Tài khoản mới được đặt `MustChangePassword = true` và phải đổi mật khẩu ở lần đăng nhập đầu tiên.

#### Biến môi trường khi deploy

```text
ConnectionStrings__Default=Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True
Seed__OwnerUserName=owner
Seed__OwnerPassword=YOUR_INITIAL_OWNER_PASSWORD
```

---

## 6. Build và chạy local

Yêu cầu:

- .NET SDK 9.0; `global.json` cho phép tự động dùng feature band mới nhất của .NET 9.
- SQL Server.

Từ thư mục gốc solution:

```powershell
dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
dotnet run --project .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj
```

Build sạch:

```powershell
dotnet clean .\HTX586CONTRACT.slnx

Get-ChildItem -Path . -Include bin,obj -Recurse -Directory |
    Remove-Item -Recurse -Force

dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
```

---

## 7. Deploy thuần ASP.NET Core trên IIS hoặc VPS

Bản này chạy trực tiếp bằng Blazor Server/Kestrel hoặc IIS, không cần lớp đóng gói trung gian. Cấu hình production dùng key chuẩn:

```text
ConnectionStrings__Default=Server=YOUR_SQL_SERVER,1433;Database=HTX586CONTRACT;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;
Seed__OwnerUserName=owner
Seed__OwnerPassword=YOUR_INITIAL_OWNER_PASSWORD
```

File mẫu cấu hình production:

```text
src/HTX586CONTRACT.Web/appsettings.Production.example.json
```

Publish:

```powershell
dotnet publish `
  .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj `
  -c Release `
  -o .\publish
```

Khi chạy sau reverse proxy Nginx/IIS ARR/Cloudflare, bật forwarded headers:

```text
ForwardedHeaders__Enabled=true
```

Thư mục `FileStorage:UploadRootPath` phải được giữ lại và backup cùng database vì chứa chữ ký/PDF hợp đồng đã phát sinh.

---

## 8. Deploy IIS trên Windows

1. Publish:

```powershell
dotnet publish `
  .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj `
  -c Release `
  -o .\publish
```

2. Cài ASP.NET Core Hosting Bundle tương ứng với .NET 9 trên máy IIS.
3. Tạo Application Pool dạng `No Managed Code`.
4. Cấp quyền ghi cho tài khoản Application Pool tại:

```text
publish\wwwroot\uploads\contracts
```

5. Cấu hình connection string bằng biến môi trường hoặc IIS Configuration Editor.
6. Không cần cài Microsoft Word hoặc LibreOffice.

---

## 9. Cách hoạt động của layout JSON

Mỗi trường chữ có cấu trúc:

```json
{
  "key": "CUSTOMER_NAME",
  "page": 1,
  "x": 175.0,
  "y": 188.0,
  "width": 260.0,
  "height": 14.0,
  "fontSize": 8.5,
  "minFontSize": 5.5,
  "maxLines": 1,
  "alignment": "Left",
  "verticalAlignment": "Center",
  "bold": false,
  "italic": false,
  "uppercase": false,
  "clearBackground": false
}
```

Tọa độ dùng đơn vị PDF point:

- Gốc tọa độ của cấu hình là góc trên bên trái.
- `x`: khoảng cách từ mép trái.
- `y`: khoảng cách từ mép trên.
- `width`, `height`: vùng tối đa của nội dung.
- `fontSize`: cỡ chữ ưu tiên.
- `minFontSize`: cỡ nhỏ nhất khi nội dung dài.
- `maxLines`: số dòng tối đa.
- `clearBackground`: xóa vùng nền bằng màu trắng trước khi ghi nội dung mới.

Trường ảnh chữ ký:

```json
{
  "key": "SIG_DRIVER",
  "page": 2,
  "x": 355.0,
  "y": 620.0,
  "width": 155.0,
  "height": 45.0,
  "fit": "Contain"
}
```

Sau khi sửa JSON, không cần compile lại nếu file nằm bên ngoài DLL và đã được publish đúng vị trí. Khi source được đóng gói trong project, nên publish lại để bảo đảm file mới được sao chép.

---

## 10. Các trường dữ liệu đang hỗ trợ

### Hợp đồng

```text
CONTRACT_NUMBER
CONTRACT_TIME
CONTRACT_DAY
CONTRACT_MONTH
CONTRACT_YEAR
CONTRACT_DATE
PASSENGER_LIST_SUBTITLE
CONTRACT_VALUE
CONTRACT_VALUE_WORDS
PAYMENT_METHOD
PAYMENT_TIME
CONTRACT_NOTE
VERIFY_CODE
```

### Hồ sơ HTX

```text
COMPANY_NAME
COMPANY_OFFICE_NAME
COMPANY_TAX_CODE
COMPANY_LICENSE
COMPANY_ADDRESS
COMPANY_PHONE
COMPANY_REPRESENTATIVE
COMPANY_REP_CITIZEN_ID
COMPANY_REP_ISSUED_DATE
COMPANY_REP_ISSUED_PLACE
COMPANY_REP_POSITION
```

### Chủ phương tiện và xe

```text
OWNER_NAME
OWNER_CITIZEN_ID
OWNER_ISSUED_DATE
OWNER_ISSUED_PLACE
VEHICLE_PLATE
VEHICLE_BRAND_MODEL
SEAT_COUNT
```

### Khách hàng

```text
CUSTOMER_NAME
CUSTOMER_TAX_CODE
CUSTOMER_PHONE
CUSTOMER_ADDRESS
CUSTOMER_CITIZEN_ID
CUSTOMER_ISSUED_DATE
CUSTOMER_ISSUED_PLACE
CUSTOMER_REPRESENTATIVE
```

### Tài xế và hành trình

```text
DRIVER_NAME
DRIVER_LICENSE_CLASS
SECOND_DRIVER_NAME
SECOND_DRIVER_LICENSE_CLASS
PICKUP_DATETIME_LOCATION
DROPOFF_DATETIME_LOCATION
ROUTE_DESCRIPTION
TOTAL_KILOMETERS
```

### Hành khách

```text
PASSENGER_COUNT
PASSENGER_COUNT_2D
P01_NAME ... P20_NAME
P01_BIRTH_YEAR ... P20_BIRTH_YEAR
P01_NOTE ... P20_NOTE
```

Mẫu cố định hỗ trợ tối đa 20 hành khách. Service sẽ từ chối sinh PDF khi danh sách vượt quá 20 người để tránh mất dữ liệu.

### Chữ ký

```text
SIG_OFFICE
SIG_OWNER
SIG_CUSTOMER
SIG_CUSTOMER_2
SIG_DRIVER
SIG_OFFICE_NAME
SIG_OWNER_NAME
SIG_CUSTOMER_NAME
SIG_DRIVER_NAME
```

---

## 11. Quy trình ký và bảo vệ dữ liệu

Các chân ký:

1. Văn phòng đại diện HTX.
2. Chủ sở hữu xe.
3. Khách hàng.
4. Tài xế xác nhận cuối cùng.

Luồng lưu chữ ký đã được thiết kế để tránh lỗi `RowVersion`:

- Mở transaction `Serializable` trước khi đọc hợp đồng.
- Khóa bản ghi hợp đồng bằng `UPDLOCK`, `HOLDLOCK`, `ROWLOCK`.
- Không dùng `SaveChangesAsync()` trong luồng ký.
- Chèn `ContractSignatures` bằng lệnh SQL trực tiếp.
- Cập nhật trạng thái hợp đồng bằng `ExecuteUpdateAsync()`.
- Không cập nhật theo `RowVersion` cũ.
- Chữ ký đã tồn tại không được ghi đè.
- File PNG được ghi tạm và chỉ trở thành file chính thức khi giao dịch SQL thành công.
- Nếu SQL hoặc thao tác file thất bại, transaction rollback và file tạm bị xóa.

Tài xế không được sửa hợp đồng khi đã có chữ ký. Hợp đồng chỉ được hủy khi chưa có bất kỳ chân ký nào.

---

## 12. Sinh PDF cuối cùng

`ContractDocumentService.GeneratePdfAsync()` thực hiện:

1. Tải hợp đồng và toàn bộ quan hệ cần thiết bằng `AsNoTracking()`.
2. Kiểm tra đủ 4 chân ký.
3. Kiểm tra số hành khách không vượt quá 20.
4. Gọi `PdfContractTemplateRenderer.RenderPdfAsync()`.
5. Tính SHA-256 của PDF.
6. Cập nhật các cột:

```text
PdfFileUrl
PdfSha256
PdfGeneratedAt
UpdatedAt
```

Việc cập nhật dùng `ExecuteUpdateAsync()`, không dùng `SaveChangesAsync()` nên không phát sinh lỗi optimistic concurrency do `RowVersion` cũ.

---

## 13. Database

### Database mới

Khi database chưa tồn tại, ứng dụng gọi:

```csharp
Database.EnsureCreatedAsync();
```

### Database đã có từ bản cũ

Ứng dụng chạy script idempotent:

```text
database/20260626_ver6.sql
```

Script bổ sung hoặc bảo đảm tồn tại:

- Các cột thông tin PDF trong `Contracts`.
- `ContractPassengers`.
- `ContractSignatures`.
- Index phục vụ truy vấn và chống ký trùng.

Không xóa dữ liệu hiện có.

---

## 14. Thư mục upload

Cấu trúc file runtime:

```text
FileStorage:UploadRootPath/contracts/{contractId}/
├── signatures/
│   ├── representativeoffice-*.png
│   ├── vehicleowner-*.png
│   ├── customer-*.png
│   └── driver-*.png
└── pdf/
    └── hop-dong-*.pdf
```

Khi triển khai nhiều instance, cần thay local filesystem bằng shared volume hoặc object storage. Database chỉ lưu URL và hash, không lưu toàn bộ file binary.

---

## 15. Chỉnh sửa template trong tương lai

### Chỉ sửa nội dung tĩnh, không đổi vị trí

1. Mở file trong `design`.
2. Chỉnh nội dung.
3. Xuất đúng 2 trang A4 thành PDF.
4. Thay file:

```text
src/HTX586CONTRACT.Web/Templates/Contracts/HopDongVanChuyenHanhKhach.template.pdf
```

5. Giữ nguyên kích thước trang và vị trí vùng trống.
6. Render thử PDF mẫu để kiểm tra.

### Có thay đổi vị trí trường

Ngoài việc thay PDF nền, cần sửa `layout.json` cho các trường bị di chuyển.

Nên điều chỉnh từng trường theo thứ tự:

1. `page`.
2. `x`, `y`.
3. `width`, `height`.
4. `fontSize`, `minFontSize`.
5. Căn lề.
6. Vị trí chữ ký.

---

## 16. Kiểm tra sau triển khai

### Kiểm tra template đã publish

```powershell
Test-Path ".\publish\Templates\Contracts\HopDongVanChuyenHanhKhach.template.pdf"
Test-Path ".\publish\Templates\Contracts\HopDongVanChuyenHanhKhach.layout.json"
```

Cả hai phải trả về `True`.

### Kiểm tra quyền ghi

Tài khoản chạy web phải có quyền ghi tại:

```text
FileStorage:UploadRootPath/contracts
```

### Kiểm tra PDF

- Đúng 2 trang A4.
- Không mất dấu tiếng Việt.
- Không vỡ dòng tại phần thông tin khách hàng và hành trình.
- Đủ 4 chữ ký.
- Trang 2 có đúng danh sách hành khách và tổng số.

---

## 17. Lỗi thường gặp

### Không tìm thấy PDF nền

```text
Không tìm thấy PDF nền tại ...
```

Kiểm tra `ContractTemplatePath` và thuộc tính copy trong `.csproj`.

### Không tìm thấy layout JSON

Kiểm tra `ContractLayoutPath` và file đã được publish.

### Không ghi được chữ ký/PDF

Kiểm tra quyền ghi thư mục `FileStorage:UploadRootPath`.

### Chữ tiếng Việt bị khác font

- Trên Windows, hệ thống sẽ ưu tiên Times New Roman hoặc font tương thích.
- Trên Linux VPS, cần cài `fontconfig` và font tương thích như `fonts-liberation` hoặc `fonts-dejavu-core`.
- Có thể đổi danh sách `fallbackFontFamilies` trong layout JSON.

### PDF không đúng vị trí sau khi đổi mẫu

PDF nền đã thay đổi kích thước hoặc bố cục nhưng layout JSON vẫn dùng tọa độ cũ. Cần hiệu chỉnh lại tọa độ.

### Không thể sinh PDF cuối cùng

Service chỉ sinh PDF khi đủ bốn chân ký và tối đa 20 hành khách.

---

## 18. Lưu ý bảo mật trước production

- Cấu hình mật khẩu admin khởi tạo bằng secret hoặc biến môi trường; không lưu mật khẩu thật trong source.
- Đổi mật khẩu tài khoản admin ngay khi đăng nhập lần đầu.
- Không commit connection string thật vào Git.
- Bật HTTPS.
- Sao lưu SQL Server và thư mục upload cùng thời điểm.
- Không công khai trực tiếp thư mục chữ ký nếu hệ thống cần mức riêng tư cao hơn; có thể chuyển sang endpoint có kiểm tra quyền.
- Khi chạy nhiều máy chủ, dùng shared storage hoặc object storage thay vì ổ đĩa cục bộ.

---

## 19. Trạng thái kiểm tra của gói bàn giao

- Solution, project, folder và namespace đã đổi sang `HTX586CONTRACT`.
- Không còn tham chiếu runtime đến Word, Open XML hoặc LibreOffice.
- PDF nền đã được kiểm tra là 2 trang A4.
- Layout JSON hợp lệ và có trường chữ, danh sách 20 hành khách cùng 5 vị trí ảnh chữ ký.
- File mẫu đã được render để kiểm tra bố cục.
- Connection string thật đã được loại khỏi `appsettings.json`.
- Các file chữ ký/PDF phát sinh từ project cũ không được đưa vào gói bàn giao.

Môi trường tạo gói không có .NET SDK nên chưa chạy được `dotnet build`. Sau khi tải project, cần chạy restore/build bằng .NET SDK 9 trên máy phát triển hoặc server để xác nhận package restore và API tương thích với môi trường của bạn.

## Cập nhật Owner seeding

- Seed role `Owner`, `Admin`, `Driver`.
- Không seed `CompanyProfile` mặc định.
- Database mới ở Development tự tạo Owner mặc định `owner / Owner@123456`; khi Production/Staging cần cấu hình `Seed:OwnerPassword` để tạo tài khoản Owner ban đầu.
- Database cũ chưa có Owner sẽ được nâng cấp mềm: nếu tìm thấy tài khoản theo `Seed:OwnerUserName`/`Seed:AdminUserName` thì gán Owner; nếu không, tự gán Owner cho Admin hiện hữu đầu tiên.
- `ContractAuditLog` có query filter khớp với `Contract.IsDeleted` để loại warning EF Core required navigation.

## Cập nhật bản no-docker + external upload storage

Bản source hiện tại đã loại bỏ các file Docker và tách upload khỏi `wwwroot`. Các URL trong database vẫn giữ dạng `/uploads/...` để tương thích dữ liệu cũ, nhưng file vật lý được lưu theo cấu hình `FileStorage:UploadRootPath`.

Cấu hình chính:

```json
"FileStorage": {
  "Provider": "LocalFileSystem",
  "UploadRootPath": "D:\\HTX586CONTRACT_Data\\uploads",
  "PublicRequestPath": "/uploads",
  "ServeUploadsAsStaticFiles": true
}
```

Khi nâng cấp từ bản cũ, copy toàn bộ nội dung `wwwroot/uploads` cũ sang thư mục mới trong `FileStorage:UploadRootPath`; không cần sửa URL trong SQL nếu vẫn giữ `PublicRequestPath` là `/uploads`.
