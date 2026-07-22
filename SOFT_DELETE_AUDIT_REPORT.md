# BÁO CÁO RÀ SOÁT SOFT DELETE

Ngày rà soát: 21/07/2026

## Phạm vi

- Domain entities và `ApplicationDbContext`.
- EF Core configurations, foreign keys, migration và nâng cấp schema khi khởi động.
- Infrastructure services.
- Login/authorization và các trang Owner/Admin/Driver.
- Luồng hợp đồng, hành khách, snapshot, PDF và Excel.
- Thao tác file trên ổ đĩa.

## Kết quả

### Bao phủ entity

Toàn bộ DbSet nghiệp vụ tùy chỉnh đều triển khai `ISoftDeletable`, trực tiếp hoặc thông qua `BaseEntity`:

1. CompanyProfile
2. Customer
3. Vehicle
4. ContractType
5. ContractTemplate
6. Contract
7. ContractPassenger
8. ContractSignature
9. ContractAttachment
10. ContractAuditLog
11. DriverNotification

`ApplicationUser` cũng triển khai `ISoftDeletable`.

### Quét thao tác phá hủy dữ liệu

Trong source chạy ứng dụng:

- `UserManager.DeleteAsync`: 0.
- `ExecuteDelete` / `ExecuteDeleteAsync`: 0.
- SQL `DELETE FROM`: 0.
- SQL `TRUNCATE TABLE`: 0.
- Endpoint hard-delete/purge: 0.

Các lệnh `Remove/RemoveRange` còn lại thuộc hai nhóm:

- Xóa phần tử khỏi model/collection trong bộ nhớ trước khi lưu form.
- `ContractPassengers.RemoveRange`, được `ApplicationDbContext` chuyển thành soft delete trước khi phát SQL.

### File vật lý

Hai vị trí `File.Delete` chỉ xử lý:

- File render tạm khi tạo PDF thất bại.
- File chữ ký/PDF mới tạo nhưng chưa được transaction/database ghi nhận thành công.

Không có luồng xóa PDF hoặc chữ ký chính thức đã được lưu thành công.

### Bypass query filter

`IgnoreQueryFilters` chỉ còn trong `DatabaseSeeder` để:

- Nhận biết bản ghi demo đã bị ẩn và không tạo trùng.
- Backfill dữ liệu lịch sử.

Không có page, API hoặc service dành cho người dùng sử dụng `IgnoreQueryFilters` để xem/khôi phục dữ liệu đã ẩn.

### Tài khoản và Công ty/Văn phòng

Hai entity này không dùng global query filter vì Identity và truy vấn lịch sử cần đọc theo ID. Thay vào đó, toàn bộ truy vấn vận hành đã được kiểm tra rõ:

- `IsDeleted = 0`.
- `IsActive = 1` khi nghiệp vụ yêu cầu.
- Công ty/Văn phòng cha phải hoạt động và chưa bị ẩn.

Truy vấn snapshot/PDF và tên người tạo lịch sử được phép đọc bản ghi đã ẩn để hợp đồng cũ không mất thông tin.

### Foreign key

Các quan hệ nghiệp vụ tùy chỉnh dùng `DeleteBehavior.Restrict`. Database cũ được nâng cấp có điều kiện để đổi các foreign key quan trọng từ CASCADE/SET NULL về NO ACTION.

## Kiểm tra tĩnh

- 88 file C#: cân bằng ngoặc và chuỗi/comment hợp lệ theo kiểm tra tĩnh.
- 59 file Razor: các khối `@code` cân bằng.
- Toàn bộ `.csproj/.props/.targets`: XML hợp lệ.
- Toàn bộ JSON: parse thành công.

Môi trường đóng gói không có .NET 9 SDK, vì vậy báo cáo này không thay thế `dotnet build` và kiểm thử tích hợp trên SQL Server thật.

## Kết luận

Trong phạm vi code của ứng dụng, thao tác xóa dữ liệu nghiệp vụ đã được chuyển sang ẩn mềm. Ứng dụng không cung cấp cách xóa vật lý hoặc phục hồi dữ liệu đã ẩn. Người có quyền truy cập SQL Server trực tiếp vẫn có toàn quyền hạ tầng và cần được kiểm soát bằng phân quyền DBA, backup và audit của SQL Server.
