# Triển khai HTX586CONTRACT trên IIS hoặc VPS

Tài liệu này dành cho bản source đã gỡ toàn bộ cấu hình đóng gói trung gian. Ứng dụng chạy trực tiếp bằng ASP.NET Core Blazor Server.

## 1. Cấu hình bắt buộc

Ứng dụng cần tối thiểu ba nhóm cấu hình sau ở môi trường production:

```text
ConnectionStrings__Default=Server=YOUR_SQL_SERVER,1433;Database=HTX586CONTRACT;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;
Seed__OwnerUserName=owner
Seed__OwnerPassword=CHANGE_ME_WITH_A_STRONG_PASSWORD
FileStorage__UploadRootPath=D:\HTX586CONTRACT_Data\uploads
```

`Seed__OwnerPassword` chỉ dùng để tạo tài khoản Owner lần đầu khi database chưa có Owner/Admin cũ. Sau khi đăng nhập, nên đổi mật khẩu ngay.

## 2. Publish

Chạy từ thư mục gốc solution:

```powershell
dotnet restore .\HTX586CONTRACT.slnx
dotnet build .\HTX586CONTRACT.slnx
dotnet publish .\src\HTX586CONTRACT.Web\HTX586CONTRACT.Web.csproj -c Release -o .\publish
```

Thư mục cần đưa lên server là `publish`.

## 3. Deploy IIS Windows

1. Cài ASP.NET Core Hosting Bundle đúng phiên bản .NET của project.
2. Tạo website hoặc application trong IIS, trỏ Physical Path tới thư mục `publish`.
3. Application Pool đặt `.NET CLR version` = `No Managed Code`.
4. Cấu hình biến môi trường cho website/application pool:

```powershell
setx ConnectionStrings__Default "Server=YOUR_SQL_SERVER,1433;Database=HTX586CONTRACT;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;"
setx Seed__OwnerUserName "owner"
setx Seed__OwnerPassword "CHANGE_ME_WITH_A_STRONG_PASSWORD"
setx FileStorage__UploadRootPath "D:\HTX586CONTRACT_Data\uploads"
```

5. Tạo thư mục upload tách riêng và cấp quyền Modify cho tài khoản Application Pool tại:

```text
D:\HTX586CONTRACT_Data\uploads
```

6. Nếu nâng cấp từ bản cũ, copy toàn bộ `publish\wwwroot\uploads` cũ sang `D:\HTX586CONTRACT_Data\uploads`.

7. Restart Application Pool sau khi đổi biến môi trường.

## 4. Deploy VPS chạy Kestrel/systemd

Ví dụ Linux VPS:

```bash
sudo mkdir -p /var/www/htx586contract
sudo rsync -av ./publish/ /var/www/htx586contract/
sudo mkdir -p /var/lib/htx586contract/uploads
sudo chown -R www-data:www-data /var/www/htx586contract /var/lib/htx586contract
```

Tạo service `/etc/systemd/system/htx586contract.service`:

```ini
[Unit]
Description=HTX586CONTRACT Blazor Web
After=network.target

[Service]
WorkingDirectory=/var/www/htx586contract
ExecStart=/usr/bin/dotnet /var/www/htx586contract/HTX586CONTRACT.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=htx586contract
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
Environment=ConnectionStrings__Default=Server=YOUR_SQL_SERVER,1433;Database=HTX586CONTRACT;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;
Environment=Seed__OwnerUserName=owner
Environment=Seed__OwnerPassword=CHANGE_ME_WITH_A_STRONG_PASSWORD
Environment=ForwardedHeaders__Enabled=true
Environment=FileStorage__UploadRootPath=/var/lib/htx586contract/uploads

[Install]
WantedBy=multi-user.target
```

Khởi động service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable htx586contract
sudo systemctl start htx586contract
sudo systemctl status htx586contract
```

Nếu dùng Linux để sinh PDF có tiếng Việt, cài font/fontconfig tương ứng cho hệ điều hành, ví dụ:

```bash
sudo apt-get update
sudo apt-get install -y fontconfig fonts-liberation fonts-dejavu-core
```

## 5. Reverse proxy mẫu Nginx

```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Nên bật HTTPS bằng chứng chỉ SSL khi chạy production. Mặc định production dùng cookie bảo mật `Always`; nếu chỉ test nội bộ bằng HTTP, có thể đặt tạm:

```text
Authentication__CookieSecurePolicy=SameAsRequest
```

## 6. Checklist sau deploy

- Website mở được trang đăng nhập.
- Database được tạo/cập nhật khi app khởi động lần đầu.
- Đăng nhập Owner thành công.
- Tạo thử Admin/Driver/Contract.
- Sinh thử PDF và kiểm tra file xuất hiện trong `FileStorage:UploadRootPath`/`contracts`.
- Backup đồng bộ SQL Server và thư mục `FileStorage:UploadRootPath`.

## 7. Upload storage tách riêng

Bản này không lưu chữ ký/PDF trong `wwwroot/uploads` nữa. URL public vẫn là `/uploads/...`, nhưng file vật lý nằm ở `FileStorage:UploadRootPath`. Xem thêm [UPLOAD_STORAGE.md](UPLOAD_STORAGE.md).
