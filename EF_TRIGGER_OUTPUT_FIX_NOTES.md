# Sửa lỗi EF Core SaveChanges với SQL Server trigger

## Triệu chứng

Ứng dụng lỗi tại `UserManager.AddToRoleAsync` / `UserStore.UpdateAsync` sau khi tạo các trigger:

- `TR_AspNetUsers_ReleaseAssignedVehicle`
- `TR_Vehicles_ValidateAssignedDriver`
- `TR_AspNetUserRoles_ReleaseAssignedVehicle`

EF Core SQL Server mặc định dùng mệnh đề `OUTPUT` khi lưu. SQL Server không cho phép
`OUTPUT` trực tiếp trên bảng đang có trigger, nên việc seed Owner hoặc cập nhật tài xế/xe có thể thất bại.

## Điều chỉnh

Đã cấu hình `UseSqlOutputClause(false)` cho đúng ba bảng có trigger:

- `AspNetUsers` trong `ApplicationUserConfiguration.cs`
- `Vehicles` trong `VehicleConfiguration.cs`
- `AspNetUserRoles` trong `ApplicationDbContext.cs`

Trigger và toàn bộ logic giải phóng xe vẫn được giữ nguyên. Không cần xóa trigger và không cần migration dữ liệu.

## Kiểm tra

1. Khởi động lại ứng dụng để chạy seed Owner.
2. Khóa một tài xế đang có xe, xác nhận xe được giải phóng.
3. Mở khóa tài xế, xác nhận xe không tự gán lại.
4. Gán xe cho tài xế hợp lệ.
5. Thử gán xe cho tài xế bị khóa, database phải từ chối.
