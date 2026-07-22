/*
  DRIVER_ACCOUNT_FLOW_AUDIT.sql
  Chỉ đọc - không UPDATE/DELETE dữ liệu.
  Dùng để DBA kiểm tra luồng khóa/nghỉ việc và gán xe.
*/
SET NOCOUNT ON;

PRINT N'1. Trigger bảo vệ trạng thái tài xế và gán xe';
SELECT
    trigger_name = tr.[name],
    is_disabled = tr.[is_disabled],
    parent_table = OBJECT_NAME(tr.[parent_id])
FROM sys.triggers tr
WHERE tr.[name] IN
(
    N'TR_AspNetUsers_ReleaseAssignedVehicle',
    N'TR_Vehicles_ValidateAssignedDriver',
    N'TR_AspNetUserRoles_ReleaseAssignedVehicle'
)
ORDER BY tr.[name];

PRINT N'2. Xe còn gán sai cho tài xế khóa/nghỉ việc/chưa duyệt/khác đơn vị (kết quả phải rỗng)';
SELECT
    vehicle.[Id] AS VehicleId,
    vehicle.[PlateNumber],
    vehicle.[CompanyProfileId] AS VehicleCompanyProfileId,
    driver.[Id] AS DriverId,
    driver.[FullName],
    driver.[EmployeeCode],
    driver.[CompanyProfileId] AS DriverCompanyProfileId,
    driver.[IsActive],
    driver.[IsDeleted],
    driver.[RegistrationStatus]
FROM [dbo].[Vehicles] vehicle
LEFT JOIN [dbo].[AspNetUsers] driver ON driver.[Id] = vehicle.[AssignedDriverId]
LEFT JOIN [dbo].[CompanyProfiles] company ON company.[Id] = driver.[CompanyProfileId]
WHERE vehicle.[AssignedDriverId] IS NOT NULL
  AND vehicle.[IsDeleted] = 0
  AND
  (
      driver.[Id] IS NULL
      OR driver.[IsActive] = 0
      OR driver.[IsDeleted] = 1
      OR ISNULL(driver.[RegistrationStatus], N'') <> N'Approved'
      OR driver.[CompanyProfileId] IS NULL
      OR company.[Id] IS NULL
      OR company.[IsActive] = 0
      OR company.[IsDeleted] = 1
      OR vehicle.[CompanyProfileId] IS NULL
      OR vehicle.[CompanyProfileId] <> driver.[CompanyProfileId]
      OR NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[AspNetUserRoles] userRole
          INNER JOIN [dbo].[AspNetRoles] role ON role.[Id] = userRole.[RoleId]
          WHERE userRole.[UserId] = driver.[Id]
            AND role.[Name] = N'Driver'
      )
  )
ORDER BY vehicle.[PlateNumber];

PRINT N'3. Tài xế khóa/nghỉ việc vẫn còn xe được gán (kết quả phải rỗng)';
SELECT
    driver.[Id] AS DriverId,
    driver.[FullName],
    driver.[EmployeeCode],
    driver.[IsActive],
    driver.[IsDeleted],
    vehicle.[Id] AS VehicleId,
    vehicle.[PlateNumber]
FROM [dbo].[AspNetUsers] driver
INNER JOIN [dbo].[Vehicles] vehicle ON vehicle.[AssignedDriverId] = driver.[Id]
WHERE vehicle.[IsDeleted] = 0
  AND (driver.[IsActive] = 0 OR driver.[IsDeleted] = 1)
ORDER BY driver.[FullName], vehicle.[PlateNumber];

PRINT N'4. Tài xế đủ điều kiện xuất hiện trong select Owner/Admin';
SELECT
    driver.[Id],
    driver.[FullName],
    driver.[EmployeeCode],
    driver.[CompanyProfileId],
    company.[CompanyName],
    assignedVehicle.[PlateNumber] AS AssignedPlateNumber
FROM [dbo].[AspNetUsers] driver
INNER JOIN [dbo].[AspNetUserRoles] userRole ON userRole.[UserId] = driver.[Id]
INNER JOIN [dbo].[AspNetRoles] role ON role.[Id] = userRole.[RoleId] AND role.[Name] = N'Driver'
INNER JOIN [dbo].[CompanyProfiles] company ON company.[Id] = driver.[CompanyProfileId]
LEFT JOIN [dbo].[Vehicles] assignedVehicle
    ON assignedVehicle.[AssignedDriverId] = driver.[Id]
   AND assignedVehicle.[IsDeleted] = 0
WHERE driver.[IsActive] = 1
  AND driver.[IsDeleted] = 0
  AND ISNULL(driver.[RegistrationStatus], N'') = N'Approved'
  AND company.[IsActive] = 1
  AND company.[IsDeleted] = 0
ORDER BY company.[CompanyName], driver.[FullName];

PRINT N'5. Kiểm tra tài xế bị gán nhiều hơn một xe (kết quả phải rỗng)';
SELECT
    vehicle.[AssignedDriverId],
    COUNT_BIG(*) AS AssignedVehicleCount
FROM [dbo].[Vehicles] vehicle
WHERE vehicle.[AssignedDriverId] IS NOT NULL
  AND vehicle.[IsDeleted] = 0
GROUP BY vehicle.[AssignedDriverId]
HAVING COUNT_BIG(*) > 1;
