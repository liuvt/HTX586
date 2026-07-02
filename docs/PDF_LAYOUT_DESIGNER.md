# PDF Layout Designer

Màn hình này dùng để kéo thả và pin vị trí dữ liệu trên mẫu hợp đồng PDF mà không cần sửa tay `x`, `y`, `width`, `height` trong JSON.

## Đường dẫn

```text
/admin/pdf-layout-designer
```

Quyền truy cập:

```text
Owner, Admin
```

## Cách dùng

1. Vào **Quản trị → Thiết kế PDF**.
2. Chọn field cần chỉnh, ví dụ `CUSTOMER_NAME`, `VEHICLE_PLATE`, `CONTRACT_VALUE_WORDS`.
3. Kéo khung trên PDF để đổi vị trí.
4. Kéo nút tròn ở góc phải dưới để resize vùng dữ liệu.
5. Chỉnh thêm thông số ở panel bên trái:
   - X/Y
   - Rộng/Cao
   - Size/Min size
   - Canh trái/giữa/phải
   - Bold/Italic/Uppercase
   - Clear background
6. Bấm **Lưu JSON**.

Khi lưu, hệ thống sẽ backup file cũ dạng:

```text
HopDongVanChuyenHanhKhach.layout.json.bak-yyyyMMddHHmmss
```

## Lưu ý

Màn hình preview dùng PDF.js ở phía trình duyệt để render PDF thành canvas.
Nếu VPS hoặc máy client không có Internet, hãy tải PDF.js về `wwwroot/lib/pdfjs` và chỉnh lại đường dẫn trong:

```text
wwwroot/js/pdf-layout-designer.js
```

## File liên quan

```text
src/HTX586CONTRACT.Web/Components/Pages/Admin/PdfLayoutDesigner.razor
src/HTX586CONTRACT.Web/Services/PdfLayoutDesignerService.cs
src/HTX586CONTRACT.Web/wwwroot/js/pdf-layout-designer.js
src/HTX586CONTRACT.Web/wwwroot/css/pdf-layout-designer.css
```
