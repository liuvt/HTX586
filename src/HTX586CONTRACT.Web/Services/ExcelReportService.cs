using ClosedXML.Excel;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Signatures;
using HTX586CONTRACT.Domain.Vehicles;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

public sealed record ExcelExportFile(byte[] Content, string FileName);

public interface IExcelReportService
{
    Task<ExcelExportFile> ExportContractsAsync(string currentUserId, bool isOwner, CancellationToken cancellationToken = default);
    Task<ExcelExportFile> ExportDriversAsync(string currentUserId, bool isOwner, CancellationToken cancellationToken = default);
    Task<ExcelExportFile> ExportVehiclesAsync(string currentUserId, bool isOwner, CancellationToken cancellationToken = default);
    Task<ExcelExportFile> ExportRevenueAsync(string currentUserId, bool isOwner, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public sealed class ExcelReportService(IDbContextFactory<ApplicationDbContext> factory) : IExcelReportService
{
    private static readonly XLColor TitleColor = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor HeaderColor = XLColor.FromHtml("#D9EAF7");
    private static readonly XLColor AccentColor = XLColor.FromHtml("#E2F0D9");

    public async Task<ExcelExportFile> ExportContractsAsync(
        string currentUserId,
        bool isOwner,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scope = await ResolveScopeAsync(db, currentUserId, isOwner, cancellationToken);

        var query = db.Contracts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.ContractType)
            .Include(x => x.CompanyProfile)
            .Include(x => x.Passengers)
            .Include(x => x.Signatures)
            .Include(x => x.Vehicle)
            .AsQueryable();

        if (scope.CompanyProfileId.HasValue)
            query = query.Where(x => x.CompanyProfileId == scope.CompanyProfileId.Value);

        var contracts = await query
            .OrderByDescending(x => x.StartTime ?? x.CreatedAt)
            .ThenBy(x => x.ContractNumber)
            .ToListAsync(cancellationToken);

        using var workbook = CreateWorkbook();
        BuildContractSummarySheet(workbook, contracts, scope);
        BuildContractDetailSheet(workbook, contracts, scope);

        return SaveWorkbook(
            workbook,
            $"BaoCao_HopDong_{scope.FileScope}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    public async Task<ExcelExportFile> ExportDriversAsync(
        string currentUserId,
        bool isOwner,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scope = await ResolveScopeAsync(db, currentUserId, isOwner, cancellationToken);

        var driverIdsQuery =
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == "Driver"
            select userRole.UserId;

        var query = db.Users
            .AsNoTracking()
            .Include(x => x.CompanyProfile)
            .Where(x => !x.IsDeleted && driverIdsQuery.Contains(x.Id));

        if (scope.CompanyProfileId.HasValue)
            query = query.Where(x => x.CompanyProfileId == scope.CompanyProfileId.Value);

        var drivers = await query
            .OrderBy(x => x.CompanyProfile == null ? string.Empty : x.CompanyProfile.CompanyName)
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var driverIds = drivers.Select(x => x.Id).ToList();

        var assignedVehicles = await db.Vehicles
            .AsNoTracking()
            .Where(x => x.AssignedDriverId != null && driverIds.Contains(x.AssignedDriverId))
            .Select(x => new DriverVehicleInfo(
                x.AssignedDriverId!,
                x.PlateNumber,
                x.VehicleCode,
                x.Brand,
                x.Model,
                x.IsActive))
            .ToDictionaryAsync(x => x.DriverId, cancellationToken);

        var driverContractQuery = db.Contracts
            .AsNoTracking()
            .Where(x => driverIds.Contains(x.DriverId));

        if (scope.CompanyProfileId.HasValue)
            driverContractQuery = driverContractQuery.Where(x => x.CompanyProfileId == scope.CompanyProfileId.Value);

        var contractStats = await driverContractQuery
            .GroupBy(x => x.DriverId)
            .Select(g => new DriverContractStats(
                g.Key,
                g.Count(),
                g.Count(x => x.Status == ContractStatus.Completed),
                g.Sum(x => x.Status == ContractStatus.Completed ? x.ContractValue ?? 0 : 0)))
            .ToDictionaryAsync(x => x.DriverId, cancellationToken);

        using var workbook = CreateWorkbook();
        BuildDriverSummarySheet(workbook, drivers, assignedVehicles, contractStats, scope);
        BuildDriverDetailSheet(workbook, drivers, assignedVehicles, contractStats, scope);

        return SaveWorkbook(
            workbook,
            $"BaoCao_TaiXe_{scope.FileScope}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    public async Task<ExcelExportFile> ExportVehiclesAsync(
        string currentUserId,
        bool isOwner,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scope = await ResolveScopeAsync(db, currentUserId, isOwner, cancellationToken);

        var query = db.Vehicles
            .AsNoTracking()
            .Include(x => x.CompanyProfile)
            .Include(x => x.AssignedDriver)
            .AsQueryable();

        if (scope.CompanyProfileId.HasValue)
            query = query.Where(x => x.CompanyProfileId == scope.CompanyProfileId.Value);

        var vehicles = await query
            .OrderBy(x => x.CompanyProfile == null ? string.Empty : x.CompanyProfile.CompanyName)
            .ThenBy(x => x.PlateNumber)
            .ToListAsync(cancellationToken);

        var vehicleIds = vehicles.Select(x => x.Id).ToList();
        var vehicleContractQuery = db.Contracts
            .AsNoTracking()
            .Where(x => x.VehicleId.HasValue && vehicleIds.Contains(x.VehicleId.Value));

        if (scope.CompanyProfileId.HasValue)
            vehicleContractQuery = vehicleContractQuery.Where(x => x.CompanyProfileId == scope.CompanyProfileId.Value);

        var contractStats = await vehicleContractQuery
            .GroupBy(x => x.VehicleId!.Value)
            .Select(g => new VehicleContractStats(
                g.Key,
                g.Count(),
                g.Count(x => x.Status == ContractStatus.Completed),
                g.Sum(x => x.Status == ContractStatus.Completed ? x.ContractValue ?? 0 : 0)))
            .ToDictionaryAsync(x => x.VehicleId, cancellationToken);

        using var workbook = CreateWorkbook();
        BuildVehicleSummarySheet(workbook, vehicles, contractStats, scope);
        BuildVehicleDetailSheet(workbook, vehicles, contractStats, scope);

        return SaveWorkbook(
            workbook,
            $"BaoCao_Xe_ChuSoHuu_{scope.FileScope}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    public async Task<ExcelExportFile> ExportRevenueAsync(
        string currentUserId,
        bool isOwner,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        ValidateRevenueRange(fromDate, toDate);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var scope = await ResolveScopeAsync(db, currentUserId, isOwner, cancellationToken);
        var from = fromDate.Date;
        var toExclusive = toDate.Date.AddDays(1);

        var query = db.Contracts
            .AsNoTracking()
            .Include(x => x.CompanyProfile)
            .Include(x => x.Passengers)
            .Where(x => x.Status == ContractStatus.Completed &&
                        (x.StartTime ?? x.CreatedAt) >= from &&
                        (x.StartTime ?? x.CreatedAt) < toExclusive);

        if (scope.CompanyProfileId.HasValue)
            query = query.Where(x => x.CompanyProfileId == scope.CompanyProfileId.Value);

        var contracts = await query
            .OrderBy(x => x.StartTime ?? x.CreatedAt)
            .ThenBy(x => x.CompanyProfile.CompanyName)
            .ThenBy(x => x.ContractNumber)
            .ToListAsync(cancellationToken);

        using var workbook = CreateWorkbook();
        BuildRevenueOverviewSheet(workbook, contracts, scope, fromDate.Date, toDate.Date);
        BuildRevenueDailySheet(workbook, contracts, scope, fromDate.Date, toDate.Date);
        BuildRevenueMonthlySheet(workbook, contracts, scope, fromDate.Date, toDate.Date);
        BuildRevenueYearlySheet(workbook, contracts, scope, fromDate.Date, toDate.Date);
        BuildRevenueDetailSheet(workbook, contracts, scope, fromDate.Date, toDate.Date);

        return SaveWorkbook(
            workbook,
            $"BaoCao_DoanhThu_{scope.FileScope}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx");
    }

    private static XLWorkbook CreateWorkbook() => new();

    private static ExcelExportFile SaveWorkbook(XLWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExcelExportFile(stream.ToArray(), fileName);
    }

    private static void BuildContractSummarySheet(XLWorkbook workbook, IReadOnlyList<Contract> contracts, ExportScope scope)
    {
        string[] headers =
        [
            "STT", "Số hợp đồng", "Công ty/Văn phòng", "Loại hợp đồng", "Tài xế", "Biển số",
            "Khách hàng/Người thuê", "Số điện thoại", "Khởi hành", "Kết thúc", "Điểm đi", "Điểm đến",
            "Số khách", "Tổng KM", "Giá trị hợp đồng", "Thanh toán", "Trạng thái", "Ngày tạo", "Ngày hoàn tất"
        ];

        var ws = PrepareSheet(workbook, "Tổng hợp", "BÁO CÁO TỔNG HỢP HỢP ĐỒNG", scope.DisplayName, headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var contract in contracts)
        {
            var values = new object?[]
            {
                index++, contract.ContractNumber, CompanyDisplay(contract), ContractTypeText(contract), contract.DriverNameSnapshot,
                contract.VehiclePlateSnapshot, contract.CustomerNameSnapshot, contract.CustomerPhoneSnapshot, contract.StartTime,
                contract.EndTime, contract.PickupLocation, contract.DropoffLocation, contract.ActualPassengerCount ?? contract.Passengers.Count,
                contract.TotalKilometers, contract.ContractValue, contract.PaymentMethod, StatusText(contract.Status), contract.CreatedAt,
                contract.CompletedAt
            };
            WriteRow(ws, row++, values);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "ContractsSummary");
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [9, 10, 18, 19]);
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 14, "#,##0.00");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 15, "#,##0");
    }

    private static void BuildContractDetailSheet(XLWorkbook workbook, IReadOnlyList<Contract> contracts, ExportScope scope)
    {
        string[] headers =
        [
            "STT", "Số hợp đồng", "Công ty/Văn phòng", "Loại hợp đồng", "Trạng thái", "Tài xế", "Hạng GPLX",
            "Biển số", "Hãng xe", "Chủ sở hữu", "CCCD chủ xe", "Khách hàng/Người thuê", "SĐT khách", "CCCD khách",
            "Địa chỉ khách", "Bắt đầu", "Kết thúc", "Điểm đi", "Điểm đến", "Lộ trình chi tiết", "Tổng KM",
            "Số chỗ", "Số khách", "Giá trị hợp đồng", "Phương thức TT", "Thời gian TT", "Ghi chú HĐ",
            "STT hành khách", "Họ tên hành khách", "Năm sinh", "Ghi chú hành khách", "Khách đã ký", "Thời gian khách ký",
            "Ngày tạo", "Ngày hoàn tất", "Lý do hủy"
        ];

        var ws = PrepareSheet(workbook, "Chi tiết", "BÁO CÁO CHI TIẾT HỢP ĐỒNG", scope.DisplayName, headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var contract in contracts)
        {
            var passengers = contract.Passengers.OrderBy(x => x.SortOrder).ToList();
            if (passengers.Count == 0)
                passengers.Add(new ContractPassenger());

            var customerSignature = contract.Signatures
                .Where(x => x.Party == SignatureParty.Customer)
                .OrderByDescending(x => x.ServerSignedAt)
                .FirstOrDefault();

            foreach (var passenger in passengers)
            {
                var values = new object?[]
                {
                    index++, contract.ContractNumber, CompanyDisplay(contract), ContractTypeText(contract), StatusText(contract.Status),
                    contract.DriverNameSnapshot, contract.DriverLicenseClassSnapshot, contract.VehiclePlateSnapshot, contract.VehicleBrandSnapshot,
                    contract.VehicleOwnerNameSnapshot, contract.VehicleOwnerCitizenIdSnapshot, contract.CustomerNameSnapshot,
                    contract.CustomerPhoneSnapshot, contract.CustomerCitizenIdSnapshot, contract.CustomerAddressSnapshot, contract.StartTime,
                    contract.EndTime, contract.PickupLocation, contract.DropoffLocation, contract.RouteDescription, contract.TotalKilometers,
                    (ContractSnapshotData.FromJson(contract.ContractDataJson) is { } snapshot ? snapshot.Vehicle.SeatCount : contract.Vehicle?.SeatCount), contract.ActualPassengerCount ?? contract.Passengers.Count, contract.ContractValue,
                    contract.PaymentMethod, contract.PaymentTime, contract.Note, passenger.SortOrder == 0 ? null : passenger.SortOrder,
                    passenger.FullName, passenger.BirthYear, passenger.Note, customerSignature is null ? "Chưa ký" : "Đã ký",
                    customerSignature?.ServerSignedAt, contract.CreatedAt, contract.CompletedAt, contract.CancelReason
                };
                WriteRow(ws, row++, values);
            }
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "ContractsDetail");
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [16, 17, 33, 34, 35]);
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 21, "#,##0.00");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 24, "#,##0");
    }

    private static void BuildDriverSummarySheet(
        XLWorkbook workbook,
        IReadOnlyList<ApplicationUser> drivers,
        IReadOnlyDictionary<string, DriverVehicleInfo> vehicles,
        IReadOnlyDictionary<string, DriverContractStats> stats,
        ExportScope scope)
    {
        string[] headers =
        [
            "STT", "Mã nhân viên", "Họ tên", "Tên đăng nhập", "Số điện thoại", "Công ty/Văn phòng", "Xe đang gán",
            "Hạng GPLX", "Trạng thái tài khoản", "Trạng thái đăng ký", "Trạng thái chữ ký", "Tổng HĐ", "HĐ hoàn tất",
            "Doanh thu hoàn tất", "Ngày tạo"
        ];

        var ws = PrepareSheet(workbook, "Tổng hợp", "BÁO CÁO TỔNG HỢP TÀI XẾ", scope.DisplayName, headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var driver in drivers)
        {
            vehicles.TryGetValue(driver.Id, out var vehicle);
            stats.TryGetValue(driver.Id, out var driverStats);
            WriteRow(ws, row++,
            [
                index++, driver.EmployeeCode, driver.FullName, driver.UserName, driver.PhoneNumber, CompanyDisplay(driver), vehicle?.PlateNumber,
                driver.DriverLicenseClass, driver.IsActive ? "Đang hoạt động" : "Đã khóa", RegistrationStatusText(driver.RegistrationStatus),
                SignatureStatusText(driver), driverStats?.TotalContracts ?? 0, driverStats?.CompletedContracts ?? 0,
                driverStats?.CompletedRevenue ?? 0, driver.CreatedAt
            ]);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "DriversSummary");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 14, "#,##0");
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [15]);
    }

    private static void BuildDriverDetailSheet(
        XLWorkbook workbook,
        IReadOnlyList<ApplicationUser> drivers,
        IReadOnlyDictionary<string, DriverVehicleInfo> vehicles,
        IReadOnlyDictionary<string, DriverContractStats> stats,
        ExportScope scope)
    {
        string[] headers =
        [
            "STT", "ID tài khoản", "Mã nhân viên", "Họ tên", "Tên đăng nhập", "Số điện thoại", "Email",
            "Công ty/Văn phòng", "Khu vực", "CCCD", "Ngày cấp CCCD", "Nơi cấp CCCD", "Ngày sinh", "Địa chỉ",
            "Số GPLX", "Hạng GPLX", "Ngày cấp GPLX", "Ngày hết hạn GPLX", "Biển số xe", "Mã xe", "Hãng xe", "Dòng xe",
            "Xe hoạt động", "Trạng thái tài khoản", "Bắt đổi mật khẩu", "Trạng thái đăng ký", "Ngày gửi đăng ký",
            "Ngày duyệt", "Ghi chú duyệt", "Trạng thái chữ ký", "Ngày ký", "Ngày vô hiệu chữ ký", "Tổng HĐ", "HĐ hoàn tất",
            "Doanh thu hoàn tất", "Ngày tạo", "Ngày cập nhật"
        ];

        var ws = PrepareSheet(workbook, "Chi tiết", "BÁO CÁO CHI TIẾT TÀI XẾ", scope.DisplayName, headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var driver in drivers)
        {
            vehicles.TryGetValue(driver.Id, out var vehicle);
            stats.TryGetValue(driver.Id, out var driverStats);
            WriteRow(ws, row++,
            [
                index++, driver.Id, driver.EmployeeCode, driver.FullName, driver.UserName, driver.PhoneNumber, driver.Email,
                CompanyDisplay(driver), driver.AreaCode, driver.CitizenId, driver.CitizenIdIssuedDate, driver.CitizenIdIssuedPlace,
                driver.DateOfBirth, driver.Address, driver.DriverLicenseNumber, driver.DriverLicenseClass, driver.DriverLicenseIssuedDate,
                driver.DriverLicenseExpiryDate, vehicle?.PlateNumber, vehicle?.VehicleCode, vehicle?.Brand, vehicle?.Model,
                vehicle is null ? null : vehicle.IsActive ? "Có" : "Không", driver.IsActive ? "Đang hoạt động" : "Đã khóa",
                driver.MustChangePassword ? "Có" : "Không", RegistrationStatusText(driver.RegistrationStatus),
                driver.RegistrationRequestedAt, driver.RegistrationReviewedAt, driver.RegistrationReviewNote, SignatureStatusText(driver),
                driver.DriverSignedAt, driver.DriverSignatureInactiveAt, driverStats?.TotalContracts ?? 0,
                driverStats?.CompletedContracts ?? 0, driverStats?.CompletedRevenue ?? 0, driver.CreatedAt, driver.UpdatedAt
            ]);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "DriversDetail");
        ApplyDateFormat(ws, headerRow + 1, row - 1, [11, 13, 17, 18]);
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [27, 28, 31, 32, 36, 37]);
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 35, "#,##0");
    }

    private static void BuildVehicleSummarySheet(
        XLWorkbook workbook,
        IReadOnlyList<Vehicle> vehicles,
        IReadOnlyDictionary<Guid, VehicleContractStats> stats,
        ExportScope scope)
    {
        string[] headers =
        [
            "STT", "Biển số", "Mã xe", "Hãng xe", "Dòng xe", "Loại xe", "Số chỗ", "Công ty/Văn phòng",
            "Chủ sở hữu", "SĐT chủ xe", "Tài xế đang gán", "Trạng thái xe", "Trạng thái chữ ký chủ xe",
            "Tổng HĐ", "HĐ hoàn tất", "Doanh thu hoàn tất", "Ngày tạo"
        ];

        var ws = PrepareSheet(workbook, "Tổng hợp", "BÁO CÁO TỔNG HỢP XE VÀ CHỦ SỞ HỮU", scope.DisplayName, headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var vehicle in vehicles)
        {
            stats.TryGetValue(vehicle.Id, out var vehicleStats);
            WriteRow(ws, row++,
            [
                index++, vehicle.PlateNumber, vehicle.VehicleCode, vehicle.Brand, vehicle.Model, vehicle.VehicleType, vehicle.SeatCount,
                CompanyDisplay(vehicle), vehicle.OwnerName, vehicle.OwnerPhoneNumber, vehicle.AssignedDriver?.FullName,
                vehicle.IsActive ? "Đang hoạt động" : "Ngừng hoạt động", OwnerSignatureStatusText(vehicle),
                vehicleStats?.TotalContracts ?? 0, vehicleStats?.CompletedContracts ?? 0, vehicleStats?.CompletedRevenue ?? 0,
                vehicle.CreatedAt
            ]);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "VehiclesSummary");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 16, "#,##0");
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [17]);
    }

    private static void BuildVehicleDetailSheet(
        XLWorkbook workbook,
        IReadOnlyList<Vehicle> vehicles,
        IReadOnlyDictionary<Guid, VehicleContractStats> stats,
        ExportScope scope)
    {
        string[] headers =
        [
            "STT", "ID xe", "Biển số", "Mã xe", "Hãng xe", "Dòng xe", "Loại xe", "Số chỗ", "Màu xe",
            "Số khung", "Số máy", "Công ty/Văn phòng", "Chủ sở hữu", "CCCD chủ xe", "Ngày cấp CCCD",
            "Nơi cấp CCCD", "Địa chỉ chủ xe", "SĐT chủ xe", "Tài xế đang gán", "Mã NV tài xế", "SĐT tài xế",
            "Trạng thái xe", "Trạng thái chữ ký chủ xe", "Ngày chủ xe ký", "Tổng HĐ", "HĐ hoàn tất",
            "Doanh thu hoàn tất", "Ngày tạo", "Ngày cập nhật"
        ];

        var ws = PrepareSheet(workbook, "Chi tiết", "BÁO CÁO CHI TIẾT XE VÀ CHỦ SỞ HỮU", scope.DisplayName, headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var vehicle in vehicles)
        {
            stats.TryGetValue(vehicle.Id, out var vehicleStats);
            WriteRow(ws, row++,
            [
                index++, vehicle.Id.ToString(), vehicle.PlateNumber, vehicle.VehicleCode, vehicle.Brand, vehicle.Model,
                vehicle.VehicleType, vehicle.SeatCount, vehicle.Color, vehicle.ChassisNumber, vehicle.EngineNumber,
                CompanyDisplay(vehicle), vehicle.OwnerName, vehicle.OwnerCitizenId, vehicle.OwnerCitizenIdIssuedDate,
                vehicle.OwnerCitizenIdIssuedPlace, vehicle.OwnerAddress, vehicle.OwnerPhoneNumber, vehicle.AssignedDriver?.FullName,
                vehicle.AssignedDriver?.EmployeeCode, vehicle.AssignedDriver?.PhoneNumber,
                vehicle.IsActive ? "Đang hoạt động" : "Ngừng hoạt động", OwnerSignatureStatusText(vehicle), vehicle.OwnerSignedAt,
                vehicleStats?.TotalContracts ?? 0, vehicleStats?.CompletedContracts ?? 0,
                vehicleStats?.CompletedRevenue ?? 0, vehicle.CreatedAt, vehicle.UpdatedAt
            ]);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "VehiclesDetail");
        ApplyDateFormat(ws, headerRow + 1, row - 1, [15]);
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [24, 28, 29]);
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 27, "#,##0");
    }

    private static void BuildRevenueOverviewSheet(
        XLWorkbook workbook,
        IReadOnlyList<Contract> contracts,
        ExportScope scope,
        DateTime fromDate,
        DateTime toDate)
    {
        string[] headers = ["STT", "Công ty/Văn phòng", "Số HĐ hoàn tất", "Tổng doanh thu", "Doanh thu bình quân/HĐ"];
        var ws = PrepareSheet(
            workbook,
            "Tổng quan",
            "BÁO CÁO TỔNG QUAN DOANH THU",
            $"{scope.DisplayName} | Từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}",
            headers.Length,
            out var headerRow);

        ws.Cell(4, 1).Value = "Tổng số HĐ hoàn tất";
        ws.Cell(4, 2).Value = contracts.Count;
        ws.Cell(4, 4).Value = "Tổng doanh thu";
        ws.Cell(4, 5).Value = contracts.Sum(x => x.ContractValue ?? 0);
        ws.Range(4, 1, 4, 5).Style.Fill.BackgroundColor = AccentColor;
        ws.Range(4, 1, 4, 5).Style.Font.Bold = true;
        ws.Cell(4, 5).Style.NumberFormat.Format = "#,##0";

        WriteHeaders(ws, headerRow, headers);
        var groups = contracts
            .GroupBy(CompanyDisplay)
            .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var row = headerRow + 1;
        var index = 1;
        foreach (var group in groups)
        {
            var revenue = group.Sum(x => x.ContractValue ?? 0);
            WriteRow(ws, row++, [index++, group.Key, group.Count(), revenue, group.Count() == 0 ? 0 : revenue / group.Count()]);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "RevenueOverview");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 4, "#,##0");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 5, "#,##0");
    }

    private static void BuildRevenueDailySheet(
        XLWorkbook workbook,
        IReadOnlyList<Contract> contracts,
        ExportScope scope,
        DateTime fromDate,
        DateTime toDate)
    {
        string[] headers = ["STT", "Ngày", "Công ty/Văn phòng", "Số HĐ hoàn tất", "Doanh thu"];
        var ws = PrepareSheet(workbook, "Theo ngày", "DOANH THU THEO NGÀY", RevenueSubtitle(scope, fromDate, toDate), headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var groups = contracts
            .GroupBy(x => new { Date = RevenueDate(x).Date, Company = CompanyDisplay(x) })
            .OrderBy(x => x.Key.Date)
            .ThenBy(x => x.Key.Company, StringComparer.CurrentCultureIgnoreCase);

        var row = headerRow + 1;
        var index = 1;
        foreach (var group in groups)
            WriteRow(ws, row++, [index++, group.Key.Date, group.Key.Company, group.Count(), group.Sum(x => x.ContractValue ?? 0)]);

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "RevenueDaily");
        ApplyDateFormat(ws, headerRow + 1, row - 1, [2]);
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 5, "#,##0");
    }

    private static void BuildRevenueMonthlySheet(
        XLWorkbook workbook,
        IReadOnlyList<Contract> contracts,
        ExportScope scope,
        DateTime fromDate,
        DateTime toDate)
    {
        string[] headers = ["STT", "Tháng", "Công ty/Văn phòng", "Số HĐ hoàn tất", "Doanh thu"];
        var ws = PrepareSheet(workbook, "Theo tháng", "DOANH THU THEO THÁNG", RevenueSubtitle(scope, fromDate, toDate), headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var groups = contracts
            .GroupBy(x => new { RevenueDate(x).Year, RevenueDate(x).Month, Company = CompanyDisplay(x) })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .ThenBy(x => x.Key.Company, StringComparer.CurrentCultureIgnoreCase);

        var row = headerRow + 1;
        var index = 1;
        foreach (var group in groups)
            WriteRow(ws, row++, [index++, new DateTime(group.Key.Year, group.Key.Month, 1), group.Key.Company, group.Count(), group.Sum(x => x.ContractValue ?? 0)]);

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "RevenueMonthly");
        if (row > headerRow + 1)
            ws.Range(headerRow + 1, 2, row - 1, 2).Style.NumberFormat.Format = "MM/yyyy";
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 5, "#,##0");
    }

    private static void BuildRevenueYearlySheet(
        XLWorkbook workbook,
        IReadOnlyList<Contract> contracts,
        ExportScope scope,
        DateTime fromDate,
        DateTime toDate)
    {
        string[] headers = ["STT", "Năm", "Công ty/Văn phòng", "Số HĐ hoàn tất", "Doanh thu"];
        var ws = PrepareSheet(workbook, "Theo năm", "DOANH THU THEO NĂM", RevenueSubtitle(scope, fromDate, toDate), headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var groups = contracts
            .GroupBy(x => new { RevenueDate(x).Year, Company = CompanyDisplay(x) })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Company, StringComparer.CurrentCultureIgnoreCase);

        var row = headerRow + 1;
        var index = 1;
        foreach (var group in groups)
            WriteRow(ws, row++, [index++, group.Key.Year, group.Key.Company, group.Count(), group.Sum(x => x.ContractValue ?? 0)]);

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "RevenueYearly");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 5, "#,##0");
    }

    private static void BuildRevenueDetailSheet(
        XLWorkbook workbook,
        IReadOnlyList<Contract> contracts,
        ExportScope scope,
        DateTime fromDate,
        DateTime toDate)
    {
        string[] headers =
        [
            "STT", "Ngày doanh thu", "Công ty/Văn phòng", "Số hợp đồng", "Tài xế", "Biển số", "Khách hàng/Người thuê",
            "Điểm đi", "Điểm đến", "Lộ trình", "Số khách", "Tổng KM", "Giá trị hợp đồng", "Phương thức TT",
            "Ngày hoàn tất", "Ngày tạo"
        ];

        var ws = PrepareSheet(workbook, "Chi tiết HĐ", "CHI TIẾT HỢP ĐỒNG HOÀN TẤT", RevenueSubtitle(scope, fromDate, toDate), headers.Length, out var headerRow);
        WriteHeaders(ws, headerRow, headers);

        var row = headerRow + 1;
        var index = 1;
        foreach (var contract in contracts)
        {
            WriteRow(ws, row++,
            [
                index++, RevenueDate(contract).Date, CompanyDisplay(contract), contract.ContractNumber, contract.DriverNameSnapshot,
                contract.VehiclePlateSnapshot, contract.CustomerNameSnapshot, contract.PickupLocation, contract.DropoffLocation,
                contract.RouteDescription, contract.ActualPassengerCount ?? contract.Passengers.Count, contract.TotalKilometers,
                contract.ContractValue, contract.PaymentMethod, contract.CompletedAt, contract.CreatedAt
            ]);
        }

        FinalizeSheet(ws, headerRow, row - 1, headers.Length, "RevenueDetail");
        ApplyDateFormat(ws, headerRow + 1, row - 1, [2]);
        ApplyDateTimeFormat(ws, headerRow + 1, row - 1, [15, 16]);
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 12, "#,##0.00");
        ApplyNumberFormat(ws, headerRow + 1, row - 1, 13, "#,##0");
    }

    private static IXLWorksheet PrepareSheet(
        XLWorkbook workbook,
        string sheetName,
        string title,
        string subtitle,
        int columnCount,
        out int headerRow)
    {
        var ws = workbook.Worksheets.Add(sheetName);
        ws.Range(1, 1, 1, columnCount).Merge();
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        ws.Cell(1, 1).Style.Fill.BackgroundColor = TitleColor;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 28;

        ws.Range(2, 1, 2, columnCount).Merge();
        ws.Cell(2, 1).Value = subtitle;
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Range(3, 1, 3, columnCount).Merge();
        ws.Cell(3, 1).Value = $"Thời điểm xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.DarkGray;
        ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        headerRow = 5;
        return ws;
    }

    private static void WriteHeaders(IXLWorksheet ws, int headerRow, IReadOnlyList<string> headers)
    {
        for (var col = 1; col <= headers.Count; col++)
            ws.Cell(headerRow, col).Value = headers[col - 1];

        var header = ws.Range(headerRow, 1, headerRow, headers.Count);
        header.Style.Fill.BackgroundColor = HeaderColor;
        header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.Style.Alignment.WrapText = true;
        header.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        header.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        header.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        ws.Row(headerRow).Height = 32;
    }

    private static void WriteRow(IXLWorksheet ws, int row, IReadOnlyList<object?> values)
    {
        for (var col = 1; col <= values.Count; col++)
            SetCellValue(ws.Cell(row, col), values[col - 1]);
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case string text:
                cell.Value = text;
                break;
            case int integer:
                cell.Value = integer;
                break;
            case long longInteger:
                cell.Value = longInteger;
                break;
            case decimal decimalNumber:
                cell.Value = decimalNumber;
                break;
            case double doubleNumber:
                cell.Value = doubleNumber;
                break;
            case bool boolean:
                cell.Value = boolean;
                break;
            case DateTime dateTime:
                cell.Value = dateTime;
                break;
            case Guid guid:
                cell.Value = guid.ToString();
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static void FinalizeSheet(IXLWorksheet ws, int headerRow, int lastRow, int columnCount, string tableName)
    {
        if (lastRow >= headerRow + 1)
        {
            var dataRange = ws.Range(headerRow + 1, 1, lastRow, columnCount);
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            dataRange.Style.Alignment.WrapText = true;
            dataRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            dataRange.Style.Border.BottomBorderColor = XLColor.LightGray;

            var table = ws.Range(headerRow, 1, lastRow, columnCount).CreateTable(tableName);
            table.Theme = XLTableTheme.TableStyleMedium2;
            table.SetShowAutoFilter(true);
        }

        ws.SheetView.FreezeRows(headerRow);
        ws.Row(headerRow).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
        {
            var headerText = ws.Cell(headerRow, columnIndex).GetString();
            var width = HeaderColumnWidth(headerText);
            ws.Column(columnIndex).Width = width;
        }

        ws.Column(1).Width = 8;
    }


    private static double HeaderColumnWidth(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return 16;

        var normalized = header.ToLowerInvariant();
        if (normalized.Contains("lộ trình") || normalized.Contains("địa chỉ") || normalized.Contains("ghi chú") || normalized.Contains("lý do"))
            return 32;
        if (normalized.Contains("công ty") || normalized.Contains("khách hàng") || normalized.Contains("chủ sở hữu") || normalized.Contains("họ tên"))
            return 24;
        if (normalized.Contains("ngày") || normalized.Contains("thời gian") || normalized.Contains("khởi hành") || normalized.Contains("kết thúc"))
            return 19;
        if (normalized.Contains("doanh thu") || normalized.Contains("giá trị") || normalized.Contains("bình quân"))
            return 18;
        if (normalized.Contains("id") || normalized.Contains("số hợp đồng") || normalized.Contains("biển số"))
            return 18;
        return 16;
    }

    private static void ApplyDateFormat(IXLWorksheet ws, int firstRow, int lastRow, IReadOnlyList<int> columns)
    {
        if (lastRow < firstRow)
            return;
        foreach (var column in columns)
            ws.Range(firstRow, column, lastRow, column).Style.NumberFormat.Format = "dd/MM/yyyy";
    }

    private static void ApplyDateTimeFormat(IXLWorksheet ws, int firstRow, int lastRow, IReadOnlyList<int> columns)
    {
        if (lastRow < firstRow)
            return;
        foreach (var column in columns)
            ws.Range(firstRow, column, lastRow, column).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
    }

    private static void ApplyNumberFormat(IXLWorksheet ws, int firstRow, int lastRow, int column, string format)
    {
        if (lastRow < firstRow)
            return;
        ws.Range(firstRow, column, lastRow, column).Style.NumberFormat.Format = format;
    }

    private static DateTime RevenueDate(Contract contract) => contract.StartTime ?? contract.CreatedAt;

    private static string RevenueSubtitle(ExportScope scope, DateTime fromDate, DateTime toDate)
        => $"{scope.DisplayName} | Từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy} | Chỉ tính hợp đồng Hoàn tất";

    private static string ContractTypeText(Contract contract)
        => !string.IsNullOrWhiteSpace(contract.ContractType?.Name)
            ? contract.ContractType.Name
            : contract.BusinessType == ContractBusinessType.Passenger
                ? "Hợp đồng vận chuyển hành khách"
                : "Hợp đồng vận chuyển hàng hóa";

    private static string StatusText(ContractStatus status) => status switch
    {
        ContractStatus.Draft => "Phát xuống cho tài xế",
        ContractStatus.WaitingCustomerSignature => "Chờ khách ký",
        ContractStatus.CustomerSigned => "Khách đã ký",
        ContractStatus.WaitingDriverConfirmation => "Chờ tài xế xác nhận",
        ContractStatus.Completed => "Hoàn tất",
        ContractStatus.Cancelled => "Đã hủy",
        ContractStatus.Expired => "Hết hạn",
        _ => "Vô hiệu"
    };

    private static string RegistrationStatusText(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "pending" => "Chờ duyệt",
        "approved" => "Đã duyệt",
        "rejected" => "Từ chối",
        _ => status ?? string.Empty
    };

    private static string SignatureStatusText(ApplicationUser driver)
    {
        if (string.IsNullOrWhiteSpace(driver.DriverSignatureFileUrl))
            return "Chưa có chữ ký";
        return driver.DriverSignatureIsActive ? "Đang sử dụng" : "Đã vô hiệu";
    }

    private static string OwnerSignatureStatusText(Vehicle vehicle)
        => string.IsNullOrWhiteSpace(vehicle.OwnerSignatureFileUrl) ? "Chưa có chữ ký" : "Đã xác nhận";

    private static string CompanyDisplay(ApplicationUser user)
    {
        if (user.CompanyProfile is null)
            return string.Empty;
        return string.IsNullOrWhiteSpace(user.CompanyProfile.BranchName)
            ? user.CompanyProfile.CompanyName
            : $"{user.CompanyProfile.CompanyName} - {user.CompanyProfile.BranchName}";
    }

    private static string CompanyDisplay(Vehicle vehicle)
    {
        if (vehicle.CompanyProfile is null)
            return string.Empty;
        return string.IsNullOrWhiteSpace(vehicle.CompanyProfile.BranchName)
            ? vehicle.CompanyProfile.CompanyName
            : $"{vehicle.CompanyProfile.CompanyName} - {vehicle.CompanyProfile.BranchName}";
    }

    private static string CompanyDisplay(Contract contract)
    {
        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        if (snapshot is not null)
            return snapshot.Company.DisplayName;

        // Hợp đồng cũ chưa có JSON snapshot vẫn ưu tiên cột snapshot tên công ty.
        if (!string.IsNullOrWhiteSpace(contract.CompanyNameSnapshot))
            return contract.CompanyNameSnapshot;

        if (contract.CompanyProfile is null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(contract.CompanyProfile.BranchName)
            ? contract.CompanyProfile.CompanyName
            : $"{contract.CompanyProfile.CompanyName} - {contract.CompanyProfile.BranchName}";
    }

    private static void ValidateRevenueRange(DateTime fromDate, DateTime toDate)
    {
        if (toDate.Date < fromDate.Date)
            throw new InvalidOperationException("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
        if (toDate.Date > fromDate.Date.AddYears(1).AddDays(-1))
            throw new InvalidOperationException("Khoảng thời gian xuất doanh thu tối đa là 01 năm.");
    }

    private static async Task<ExportScope> ResolveScopeAsync(
        ApplicationDbContext db,
        string currentUserId,
        bool isOwner,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            throw new InvalidOperationException("Không xác định được tài khoản đang đăng nhập.");

        if (isOwner)
            return new ExportScope(null, "Toàn bộ Công ty/Văn phòng", "ToanHeThong");

        var admin = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserId && !x.IsDeleted && x.IsActive &&
                            x.CompanyProfile != null && x.CompanyProfile.IsActive && !x.CompanyProfile.IsDeleted)
            .Select(x => new
            {
                x.CompanyProfileId,
                CompanyName = x.CompanyProfile == null
                    ? string.Empty
                    : (string.IsNullOrEmpty(x.CompanyProfile.BranchName)
                        ? x.CompanyProfile.CompanyName
                        : x.CompanyProfile.CompanyName + " - " + x.CompanyProfile.BranchName)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (admin?.CompanyProfileId is null)
            throw new InvalidOperationException("Tài khoản Admin chưa được gán Công ty/Văn phòng nên không thể xuất báo cáo.");

        return new ExportScope(admin.CompanyProfileId, admin.CompanyName, "CongTyDuocGan");
    }

    private sealed record ExportScope(Guid? CompanyProfileId, string DisplayName, string FileScope);
    private sealed record DriverVehicleInfo(string DriverId, string PlateNumber, string? VehicleCode, string? Brand, string? Model, bool IsActive);
    private sealed record DriverContractStats(string DriverId, int TotalContracts, int CompletedContracts, decimal CompletedRevenue);
    private sealed record VehicleContractStats(Guid VehicleId, int TotalContracts, int CompletedContracts, decimal CompletedRevenue);
}
