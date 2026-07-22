# CHÍNH SÁCH BẢO TOÀN DỮ LIỆU – SOFT DELETE

## 1. Nguyên tắc áp dụng

Trong ứng dụng HTX586CONTRACT, thao tác người dùng gọi là **Xóa** được hiểu là **Ẩn khỏi giao diện**:

- Bản ghi vẫn còn nguyên trong SQL Server.
- Khóa chính và các quan hệ lịch sử không bị cắt.
- Hợp đồng, snapshot, hành khách, chữ ký, tệp đính kèm, xe, khách hàng và tài khoản liên quan vẫn giữ nguồn dữ liệu gốc.
- Ứng dụng không cung cấp màn hình, API hoặc endpoint để xóa vật lý hay phục hồi bản ghi đã ẩn.
- Chỉ người quản trị SQL Server có quyền truy cập database trực tiếp mới có thể kiểm tra hoặc xử lý dữ liệu nguồn.

## 2. Cơ chế bảo vệ

### Soft delete ở tầng entity

Các entity nghiệp vụ kế thừa `BaseEntity` có sẵn:

- `IsDeleted`
- `DeletedAt`
- `DeletedBy`

`ApplicationUser`, `CompanyProfile`, `ContractAuditLog` và `DriverNotification` cũng triển khai `ISoftDeletable` để áp dụng cùng chính sách. Vì vậy toàn bộ entity nghiệp vụ tùy chỉnh trong `ApplicationDbContext` đều được lớp bảo vệ soft delete bao phủ.

### Chặn xóa vật lý ở tầng DbContext

`ApplicationDbContext.SaveChanges` và `SaveChangesAsync` kiểm tra mọi entity đang ở trạng thái `Deleted`.
Nếu entity triển khai `ISoftDeletable`, thao tác DELETE được đổi thành UPDATE:

```text
IsDeleted = 1
DeletedAt = thời điểm ẩn
DeletedBy = nguồn thao tác
```

Nhờ đó, kể cả code gọi nhầm `Remove` hoặc `RemoveRange`, dữ liệu nghiệp vụ vẫn không bị DELETE khỏi database.

### Tài khoản người dùng

Khi tài khoản bị ẩn:

- Giữ nguyên dòng `AspNetUsers`.
- Giữ nguyên role, claim, login, token và liên kết lịch sử.
- Đặt `IsDeleted = 1`, `IsActive = 0`.
- Đổi `SecurityStamp` để phiên đăng nhập cũ hết hiệu lực nhanh.
- Không cho đăng nhập hoặc xuất hiện trong danh sách/ô chọn.

### Công ty/Văn phòng

Khi đơn vị bị ẩn:

- Giữ nguyên đơn vị trong `CompanyProfiles`.
- Không xóa hoặc chuyển quan hệ của tài xế, xe và hợp đồng.
- Tài khoản thuộc đơn vị không còn được sử dụng trong luồng vận hành.
- Dữ liệu lịch sử và snapshot hợp đồng vẫn đọc được khi cần xuất tài liệu.

### Hợp đồng và dữ liệu con

- Hợp đồng chưa khóa chỉ được **ẩn**, không xóa vật lý.
- Hợp đồng hoàn tất/hủy/hết hạn/vô hiệu hóa không cho thao tác ẩn hoặc sửa.
- Hành khách bị bỏ khỏi biểu mẫu được soft delete qua interceptor.
- Unique index hành khách chỉ áp dụng với `IsDeleted = 0`, nên có thể dùng lại thứ tự hiển thị mà vẫn giữ bản ghi cũ.
- Snapshot và PDF chính thức của hợp đồng hoàn tất tiếp tục là dữ liệu lịch sử bất biến.

## 3. Dữ liệu nào được ẩn khỏi người dùng

Mặc định danh sách và ô chọn không trả về bản ghi có `IsDeleted = 1`.
Đối với `ApplicationUser` và `CompanyProfile`, các truy vấn vận hành còn kiểm tra:

- Người dùng đang hoạt động.
- Công ty/Văn phòng đang hoạt động.
- Công ty/Văn phòng chưa bị ẩn.

Các truy vấn lịch sử phục vụ snapshot/PDF có thể đọc bản ghi đã ẩn theo ID để không làm mất thông tin hợp đồng cũ.

## 4. Không có luồng xóa vật lý trong ứng dụng

Đã rà soát source và không còn:

- `UserManager.DeleteAsync`.
- `ExecuteDelete` / `ExecuteDeleteAsync`.
- SQL `DELETE FROM` hoặc `TRUNCATE TABLE`.
- Endpoint hard-delete hoặc purge dành cho giao diện.

Các lệnh `File.Delete` còn lại chỉ dọn file tạm hoặc file mới sinh nhưng transaction lưu database thất bại. Chúng không xóa file PDF/chữ ký chính thức đã được ghi nhận thành công.

## 5. Nâng cấp database hiện hữu

Khi ứng dụng khởi động, `DatabaseSeeder.EnsureSoftDeleteColumnsAsync` bổ sung có điều kiện:

- Các cột soft-delete còn thiếu cho `AspNetUsers`, `CompanyProfiles`, `ContractAuditLogs` và `DriverNotifications`.
- Index hỗ trợ lọc dữ liệu ẩn.
- Foreign key quan trọng về `NO ACTION/RESTRICT`.
- Filtered unique index cho `ContractPassengers`.

Migration `20260721093000_AddApplicationSoftDelete` cũng được kèm theo cho môi trường quản lý database bằng EF migration.

Luôn sao lưu database trước khi publish bản mới.

## 6. Quyền của quản trị database

Soft delete bảo vệ dữ liệu trước mọi luồng của ứng dụng. Người có quyền SQL Server trực tiếp vẫn có thể sửa hoặc xóa dữ liệu bằng SQL; đây là quyền quản trị hạ tầng và không thể bị ứng dụng vô hiệu hóa hoàn toàn.

Khuyến nghị:

- Tài khoản chạy ứng dụng chỉ cấp quyền cần thiết, không cấp `db_owner` nếu không bắt buộc.
- Chỉ DBA giữ quyền hard-delete.
- Bật backup định kỳ và lưu bản backup ngoài máy chủ ứng dụng.
- Không cấp SSMS/SQL login cho tài khoản Owner/Admin của website.

Xem `database/SOFT_DELETE_AUDIT.sql` để kiểm tra dữ liệu đã ẩn bằng các câu lệnh chỉ đọc.
