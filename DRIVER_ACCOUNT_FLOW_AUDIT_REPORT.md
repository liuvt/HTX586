# Báo cáo rà soát luồng khóa/nghỉ việc tài xế

## Kết quả nghiệp vụ

### Khóa tạm thời
- Tài khoản chuyển `IsActive = false`, không bị xóa vật lý.
- `SecurityStamp` và `ConcurrencyStamp` được đổi để phiên đăng nhập cũ mất hiệu lực.
- Xe đang gán được cập nhật `AssignedDriverId = null` trong cùng transaction.
- Không xóa chữ ký, hồ sơ, hợp đồng, thông báo hoặc lịch sử.
- Mở khóa không tự khôi phục xe cũ.

### Nghỉ việc
- Tài khoản chuyển `IsActive = false`, `IsDeleted = true` theo soft delete.
- Bản ghi vẫn còn trong `AspNetUsers` và chỉ bị ẩn trên giao diện.
- Xe đang gán được giải phóng.
- Hợp đồng lịch sử tiếp tục sử dụng snapshot đã lưu.

### Chặn đăng nhập và phiên cũ
- Endpoint đăng nhập từ chối tài khoản không hoạt động, đã ẩn hoặc Driver chưa được duyệt.
- `ActiveAccountGuard` kiểm tra lại trạng thái định kỳ trong phiên đang mở và đưa tài khoản bị khóa về trang đăng nhập.
- Security stamp vẫn được Identity kiểm tra theo cấu hình của ứng dụng.

### Loại khỏi lựa chọn Owner/Admin
Tất cả danh sách chọn tài xế để:
- tạo/cập nhật xe;
- phát/cập nhật hợp đồng;

đều yêu cầu đồng thời:
- `IsActive = true`;
- `IsDeleted = false`;
- `RegistrationStatus = Approved`;
- Công ty/Văn phòng đang hoạt động và chưa bị ẩn.

`ContractService` kiểm tra lại các điều kiện này ở tầng service để chặn trường hợp giao diện cũ hoặc dữ liệu bị thay đổi trong lúc người dùng đang thao tác.

### Bảo vệ database
- Startup tự giải phóng các quan hệ gán xe không hợp lệ còn sót từ dữ liệu cũ.
- Trigger trên `AspNetUsers` tự giải phóng xe khi tài xế bị khóa, nghỉ việc, chưa duyệt hoặc chuyển đơn vị.
- Trigger trên `Vehicles` từ chối gán xe cho tài xế không hợp lệ.
- Unique filtered index bảo đảm một tài xế chỉ được gán tối đa một xe đang tồn tại.

### Tìm kiếm Xe & Chủ sở hữu
Trang `/admin/vehicles` tìm theo:
- biển số;
- tên chủ sở hữu;
- họ tên tài xế đang được gán;
- mã nhân viên tài xế đang được gán.

## Kiểm tra DBA
Chạy file `database/DRIVER_ACCOUNT_FLOW_AUDIT.sql`. Các mục 2, 3 và 5 phải trả về tập rỗng.
