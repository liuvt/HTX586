# Upload storage

Bản này đã tách toàn bộ file phát sinh khỏi `wwwroot/uploads`.

Ứng dụng vẫn lưu URL trong database dạng `/uploads/...` để tương thích dữ liệu cũ, nhưng file vật lý được đọc/ghi tại thư mục cấu hình:

```json
"FileStorage": {
  "Provider": "LocalFileSystem",
  "UploadRootPath": "D:\\HTX586CONTRACT_Data\\uploads",
  "PublicRequestPath": "/uploads",
  "ServeUploadsAsStaticFiles": true
}
```

## Ý nghĩa cấu hình

- `Provider`: hiện tại dùng `LocalFileSystem`; giữ sẵn để mở rộng sang NAS, S3, MinIO hoặc Azure Blob sau này.
- `UploadRootPath`: thư mục vật lý chứa chữ ký, PDF và các file upload phát sinh.
- `PublicRequestPath`: đường dẫn public. Nên giữ `/uploads` để không cần migrate dữ liệu cũ.
- `ServeUploadsAsStaticFiles`: bật/tắt middleware phục vụ file upload từ `UploadRootPath`.

## Cấu trúc thư mục upload

```text
HTX586CONTRACT_Data/uploads/
├── contracts/
│   └── {contractId}/
│       ├── signatures/
│       └── pdf/
└── master-signatures/
    ├── companies/
    ├── drivers/
    └── vehicles/
```

## Migration từ bản cũ

Nếu server cũ đang có dữ liệu ở:

```text
publish/wwwroot/uploads
```

hãy copy toàn bộ nội dung sang thư mục mới, ví dụ Windows:

```powershell
robocopy "D:\Sites\HTX586CONTRACT\wwwroot\uploads" "D:\HTX586CONTRACT_Data\uploads" /E
```

hoặc Linux:

```bash
sudo rsync -av /var/www/htx586contract/wwwroot/uploads/ /var/lib/htx586contract/uploads/
```

Không cần đổi dữ liệu trong SQL nếu vẫn giữ `PublicRequestPath` là `/uploads`.

## Lưu ý deploy

- Cấp quyền ghi cho tài khoản chạy app trên `UploadRootPath`.
- Không xóa `UploadRootPath` khi deploy bản mới.
- Backup đồng bộ SQL Server và `UploadRootPath`.
