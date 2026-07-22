# Luồng khóa/nghỉ việc tài xế và tìm kiếm xe

## Khóa tạm thời
- `ApplicationUser.IsActive = false`, không soft delete.
- Đổi `SecurityStamp` để cookie đăng nhập cũ mất hiệu lực.
- Tự động đặt `Vehicles.AssignedDriverId = NULL`.
- Không xóa hồ sơ, chữ ký, thông báo, khách hàng hoặc hợp đồng lịch sử.
- Tài xế bị loại khỏi mọi select tạo hợp đồng/gán xe do các query chỉ lấy `IsActive && !IsDeleted`.
- Khi mở khóa, xe cũ không tự động được gán lại.

## Nghỉ việc
- Dùng soft delete: `IsDeleted = true`, `IsActive = false`.
- Tài khoản bị ẩn khỏi giao diện nhưng còn nguyên trong `AspNetUsers`.
- Xe đang gán được giải phóng.
- Hợp đồng hoàn tất tiếp tục đọc snapshot lịch sử, không đổi theo hồ sơ hiện tại.

## Bảo vệ tầng database
- Startup tự sửa các xe đang gán cho tài xế khóa/nghỉ việc/chuyển đơn vị.
- Trigger tài khoản tự giải phóng xe khi trạng thái tài xế thay đổi.
- Trigger xe từ chối gán cho tài xế không hoạt động, bị ẩn, chưa duyệt hoặc khác Công ty/Văn phòng.

## Tìm kiếm Xe & Chủ sở hữu
Thanh tìm kiếm `/admin/vehicles` hỗ trợ:
- Biển số xe.
- Họ tên chủ sở hữu.
- Họ tên tài xế được gán.
- Mã nhân viên tài xế được gán.
