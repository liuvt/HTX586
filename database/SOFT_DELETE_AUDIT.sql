/*
    HTX586CONTRACT - KIỂM TRA DỮ LIỆU ĐÃ ẨN
    Script CHỈ ĐỌC. Không chứa DELETE, UPDATE, TRUNCATE hoặc phục hồi dữ liệu.
    Chỉ DBA/nhân sự được cấp quyền SQL Server mới chạy script này.
*/

SET NOCOUNT ON;

-- 1. Tổng hợp số lượng bản ghi đang bị ẩn.
SELECT N'AspNetUsers' AS TableName, COUNT_BIG(*) AS HiddenRows
FROM dbo.AspNetUsers WHERE IsDeleted = 1
UNION ALL
SELECT N'CompanyProfiles', COUNT_BIG(*)
FROM dbo.CompanyProfiles WHERE IsDeleted = 1
UNION ALL
SELECT N'Contracts', COUNT_BIG(*)
FROM dbo.Contracts WHERE IsDeleted = 1
UNION ALL
SELECT N'ContractPassengers', COUNT_BIG(*)
FROM dbo.ContractPassengers WHERE IsDeleted = 1
UNION ALL
SELECT N'ContractSignatures', COUNT_BIG(*)
FROM dbo.ContractSignatures WHERE IsDeleted = 1
UNION ALL
SELECT N'ContractAttachments', COUNT_BIG(*)
FROM dbo.ContractAttachments WHERE IsDeleted = 1
UNION ALL
SELECT N'Customers', COUNT_BIG(*)
FROM dbo.Customers WHERE IsDeleted = 1
UNION ALL
SELECT N'Vehicles', COUNT_BIG(*)
FROM dbo.Vehicles WHERE IsDeleted = 1
UNION ALL
SELECT N'ContractTypes', COUNT_BIG(*)
FROM dbo.ContractTypes WHERE IsDeleted = 1
UNION ALL
SELECT N'ContractTemplates', COUNT_BIG(*)
FROM dbo.ContractTemplates WHERE IsDeleted = 1
UNION ALL
SELECT N'ContractAuditLogs', COUNT_BIG(*)
FROM dbo.ContractAuditLogs WHERE IsDeleted = 1
UNION ALL
SELECT N'DriverNotifications', COUNT_BIG(*)
FROM dbo.DriverNotifications WHERE IsDeleted = 1;

-- 2. Tài khoản đã ẩn.
SELECT
    Id,
    UserName,
    FullName,
    PhoneNumber,
    CompanyProfileId,
    IsActive,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.AspNetUsers
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, UserName;

-- 3. Công ty/Văn phòng đã ẩn.
SELECT
    Id,
    CompanyName,
    BranchName,
    TaxCode,
    IsActive,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.CompanyProfiles
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, CompanyName;

-- 4. Hợp đồng đã ẩn nhưng vẫn còn trong nguồn dữ liệu.
SELECT
    Id,
    ContractNumber,
    Status,
    CompanyProfileId,
    DriverId,
    VehicleId,
    CustomerId,
    PdfFileUrl,
    PdfSha256,
    ContractDataJson,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.Contracts
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, CreatedAt DESC;

-- 5. Hành khách đã được bỏ khỏi giao diện hợp đồng.
SELECT
    Id,
    ContractId,
    SortOrder,
    FullName,
    CitizenId,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.ContractPassengers
WHERE IsDeleted = 1
ORDER BY ContractId, SortOrder, DeletedAt DESC;

-- 6. Xe và khách hàng đã ẩn.
SELECT
    Id,
    PlateNumber,
    CompanyProfileId,
    AssignedDriverId,
    IsActive,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.Vehicles
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, PlateNumber;

SELECT
    Id,
    FullName,
    PhoneNumber,
    CitizenId,
    CreatedByDriverId,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.Customers
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, FullName;


-- 7. Nhật ký và thông báo bị ẩn (nếu có thao tác kỹ thuật trong tương lai).
SELECT
    Id,
    ContractId,
    Action,
    UserId,
    UserName,
    CreatedAt,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.ContractAuditLogs
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, CreatedAt DESC;

SELECT
    Id,
    DriverId,
    Type,
    Title,
    CreatedAt,
    IsRead,
    IsDeleted,
    DeletedAt,
    DeletedBy
FROM dbo.DriverNotifications
WHERE IsDeleted = 1
ORDER BY DeletedAt DESC, CreatedAt DESC;

-- 8. Kiểm tra hành vi ON DELETE của các foreign key nghiệp vụ.
-- delete_referential_action_desc nên là NO_ACTION đối với các quan hệ lịch sử.
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) + N'.' + OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) + N'.' + OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
    fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN
(
    N'AspNetUsers', N'Vehicles', N'Contracts', N'ContractPassengers',
    N'ContractSignatures', N'ContractAttachments', N'ContractAuditLogs',
    N'Customers', N'DriverNotifications'
)
ORDER BY ChildTable, ForeignKeyName;
