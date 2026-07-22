using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Contracts;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Notifications;
using HTX586CONTRACT.Domain.Vehicles;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class ContractService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IContractService
{
    public async Task<IReadOnlyList<ContractListItemDto>> GetAsync(ContractFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Contracts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.ContractNumber.Contains(search) ||
                x.CustomerNameSnapshot.Contains(search) ||
                x.DriverNameSnapshot.Contains(search) ||
                (x.VehiclePlateSnapshot != null && x.VehiclePlateSnapshot.Contains(search)));
        }

        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.BusinessType.HasValue) query = query.Where(x => x.BusinessType == filter.BusinessType.Value);
        if (!string.IsNullOrWhiteSpace(filter.DriverId)) query = query.Where(x => x.DriverId == filter.DriverId);
        if (filter.CompanyProfileId.HasValue) query = query.Where(x => x.CompanyProfileId == filter.CompanyProfileId.Value);
        if (filter.FromDate.HasValue)
            query = query.Where(x => (x.StartTime ?? x.CreatedAt) >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue)
            query = query.Where(x => (x.StartTime ?? x.CreatedAt) < filter.ToDate.Value.Date.AddDays(1));

        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContractListItemDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                BusinessType = x.BusinessType,
                CompanyName = x.CompanyNameSnapshot,
                CustomerName = x.CustomerNameSnapshot,
                DriverName = x.DriverNameSnapshot,
                VehicleId = x.VehicleId,
                VehiclePlate = x.VehiclePlateSnapshot,
                StartTime = x.StartTime,
                ContractValue = x.ContractValue,
                Status = x.Status,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<ContractListItemDto>> GetDriverContractsAsync(string driverId, CancellationToken ct = default)
        => GetAsync(new ContractFilter { DriverId = driverId }, ct);

    public async Task<ContractDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var detail = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ContractDetailDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                BusinessType = x.BusinessType,
                ContractTypeId = x.ContractTypeId,
                Status = x.Status,
                CompanyProfileId = x.CompanyProfileId,
                CompanyName = x.CompanyNameSnapshot,
                DriverId = x.DriverId,
                DriverName = x.DriverNameSnapshot,
                DriverLicenseClass = x.DriverLicenseClassSnapshot,
                CustomerId = x.CustomerId,
                CustomerName = x.CustomerNameSnapshot,
                CustomerPhone = x.CustomerPhoneSnapshot,
                CustomerCitizenId = x.CustomerCitizenIdSnapshot,
                CustomerAddress = x.CustomerAddressSnapshot,
                AreaCode = x.AreaCode,
                VehicleId = x.VehicleId,
                VehiclePlate = x.VehiclePlateSnapshot,
                VehicleCode = x.Vehicle != null ? x.Vehicle.VehicleCode : null,
                VehicleBrand = x.VehicleBrandSnapshot,
                SeatCount = x.Vehicle != null ? x.Vehicle.SeatCount : null,
                ActualPassengerCount = x.Passengers.Count(),
                OwnerName = x.VehicleOwnerNameSnapshot,
                OwnerCitizenId = x.VehicleOwnerCitizenIdSnapshot,
                OwnerCitizenIdIssuedDate = x.Vehicle != null ? x.Vehicle.OwnerCitizenIdIssuedDate : null,
                CargoName = x.CargoName,
                CargoWeight = x.CargoWeight,
                CargoUnit = x.CargoUnit,
                SecondDriverName = x.SecondDriverName,
                SecondDriverLicenseClass = x.SecondDriverLicenseClass,
                PickupLocation = x.PickupLocation,
                DropoffLocation = x.DropoffLocation,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                RouteDescription = x.RouteDescription,
                TotalKilometers = x.TotalKilometers,
                ContractValue = x.ContractValue,
                PaymentMethod = x.PaymentMethod,
                PaymentTime = x.PaymentTime,
                Note = x.Note,
                PdfFileUrl = x.PdfFileUrl,
                CreatedAt = x.CreatedAt,
                CreatedByUserId = x.CreatedBy,
                Passengers = x.Passengers.OrderBy(p => p.SortOrder).Select(p => new ContractPassengerDto
                {
                    Id = p.Id,
                    SortOrder = p.SortOrder,
                    FullName = p.FullName,
                    BirthYear = p.BirthYear,
                    Note = p.Note
                }).ToList(),
                Signatures = x.Signatures.OrderBy(s => s.ServerSignedAt).Select(s => new ContractSignatureDto
                {
                    Id = s.Id,
                    Party = s.Party,
                    SignerName = s.SignerName,
                    SignatureFileUrl = s.SignatureFileUrl,
                    ServerSignedAt = s.ServerSignedAt
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (detail is null) return null;

        var snapshotJson = await db.Contracts.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.ContractDataJson)
            .FirstOrDefaultAsync(ct);
        var immutableSnapshot = ContractSnapshotData.FromJson(snapshotJson);
        if (immutableSnapshot is not null)
            ApplyImmutableSnapshot(detail, immutableSnapshot);

        detail.CreatedByName = await GetUserDisplayNameAsync(db, detail.CreatedByUserId, ct);

        var dispatch = await db.ContractAuditLogs.AsNoTracking()
            .Where(x => x.ContractId == id && x.Action == "AssignedToDriver")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.UserId, x.UserName, x.CreatedAt })
            .FirstOrDefaultAsync(ct);

        if (dispatch is not null)
        {
            detail.AssignedByUserId = dispatch.UserId;
            detail.AssignedByName = string.IsNullOrWhiteSpace(dispatch.UserName)
                ? await GetUserDisplayNameAsync(db, dispatch.UserId, ct)
                : dispatch.UserName;
            detail.AssignedAt = dispatch.CreatedAt;
        }
        else if (!string.Equals(detail.CreatedByUserId, detail.DriverId, StringComparison.Ordinal))
        {
            // Hợp đồng cũ chưa có nhật ký phát xuống: dùng người tạo làm thông tin dự phòng.
            detail.AssignedByUserId = detail.CreatedByUserId;
            detail.AssignedByName = detail.CreatedByName;
            detail.AssignedAt = detail.CreatedAt;
        }

        return detail;
    }

    public async Task<SaveContractResult> CreateAsync(SaveContractRequest request, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManageContracts = access.IsOwner || access.IsAdmin;
        if (request.BusinessType == ContractBusinessType.Cargo)
            return new(false, null, "Hợp đồng vận chuyển hàng hóa bằng xe ô tô đang tạm chưa sử dụng.");
        if (request.BusinessType != ContractBusinessType.Passenger)
            return new(false, null, "Loại hợp đồng không hợp lệ.");
        if (MissingCustomerInfo(request))
            return new(false, null, "Vui lòng chọn hoặc nhập đầy đủ tên và số điện thoại khách hàng trước khi tạo hợp đồng.");

        var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
        if (assignment.Error is not null)
            return new(false, null, assignment.Error);

        var vehicle = assignment.Vehicle!;
        var driver = assignment.Driver!;
        if (!driver.CompanyProfileId.HasValue || driver.CompanyProfile is null)
            return new(false, null, "Tài xế chưa được gán CompanyProfile.");

        var companyProfileId = driver.CompanyProfileId.Value;
        var companyProfile = driver.CompanyProfile;

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null) return new(false, null, "Chưa cấu hình loại hợp đồng phù hợp.");
        var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null) return new(false, null, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        var customer = await ResolveCustomerAsync(
            db,
            request,
            currentUserId,
            canManageContracts,
            access.IsOwner,
            companyProfileId,
            existingCustomerId: null,
            allowPlaceholder: false,
            ct);

        var entity = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = string.IsNullOrWhiteSpace(request.ContractNumber)
                ? $"{DateTime.Now:yyyyMMddHHmmss}/{BusinessCode(request.BusinessType)}"
                : request.ContractNumber.Trim(),
            BusinessType = request.BusinessType,
            ContractTypeId = type.Id,
            ContractTemplateId = template.Id,
            CompanyProfileId = companyProfileId,
            DriverId = driver.Id,
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            Status = canManageContracts
                ? ContractStatus.Draft
                : ContractStatus.WaitingCustomerSignature,
            ContractContentSnapshot = template.HtmlContent,
            CreatedBy = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        Apply(entity, request);
        ApplySnapshots(entity, driver, companyProfile, customer, vehicle);
        entity.ContractDataJson = ContractSnapshotData.Capture(companyProfile, driver, customer, vehicle).ToJson();
        AddPassengers(entity, request.Passengers, currentUserId);

        if (canManageContracts)
        {
            var actorName = await GetUserDisplayNameAsync(db, currentUserId, ct);
            entity.AuditLogs.Add(new ContractAuditLog
            {
                ContractId = entity.Id,
                Action = "AssignedToDriver",
                UserId = currentUserId,
                UserName = actorName,
                NewDataJson = $"{{\"driverId\":\"{driver.Id}\",\"vehicleId\":\"{vehicle.Id}\"}}",
                CreatedAt = DateTime.UtcNow
            });
            db.DriverNotifications.Add(new DriverNotification
            {
                DriverId = driver.Id,
                Type = "ContractAssigned",
                Title = "Bạn được phát hợp đồng mới",
                Message = $"Hợp đồng {entity.ContractNumber} đã được {actorName} phát xuống. Vui lòng cập nhật nội dung hợp đồng, danh sách hành khách và lấy chữ ký khách hàng.",
                LinkUrl = $"/driver/contracts/{entity.Id}",
                RelatedContractId = entity.Id,
                RelatedVehicleId = vehicle.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Contracts.Add(entity);
        await db.SaveChangesAsync(ct);
        return new(true, entity.Id, canManageContracts
            ? "Đã tạo và phát hợp đồng xuống tài xế. Thông tin hợp đồng, phương tiện và khách hàng được khóa; tài xế cập nhật nội dung hợp đồng, danh sách hành khách rồi ghi nhận chữ ký khách hàng để hoàn tất."
            : "Đã tạo hợp đồng và lưu khách hàng do tài xế nhập. Sau khi lưu, chỉ Owner/Admin được quản lý hồ sơ khách hàng này.");
    }

    public async Task<SaveContractResult> UpdateAsync(Guid id, SaveContractRequest request, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Passengers)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return new(false, null, "Không tìm thấy hợp đồng.");
        if (entity.Status is ContractStatus.Completed or ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Invalidated)
            return new(false, id, "Hợp đồng đã hoàn tất, đã hủy, đã hết hạn hoặc đã vô hiệu hóa và không thể chỉnh sửa.");

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManageContracts = access.IsOwner || access.IsAdmin;
        if (request.BusinessType == ContractBusinessType.Cargo)
            return new(false, id, "Hợp đồng vận chuyển hàng hóa bằng xe ô tô đang tạm chưa sử dụng.");
        if (request.BusinessType != ContractBusinessType.Passenger)
            return new(false, id, "Loại hợp đồng không hợp lệ.");
        if (access.IsAdmin && !access.IsOwner && entity.CompanyProfileId != access.CompanyProfileId)
            return new(false, id, "Admin chỉ được cập nhật hợp đồng thuộc CompanyProfile được gán.");
        if (!canManageContracts && !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Bạn không có quyền cập nhật hợp đồng này.");

        if (entity.Signatures.Any(x => x.Party == SignatureParty.Customer))
            return new(false, id, "Khách hàng đã ký nên hợp đồng không được cập nhật nội dung.");

        if (canManageContracts && MissingCustomerInfo(request))
            return new(false, id, "Vui lòng chọn hoặc nhập đầy đủ tên và số điện thoại khách hàng.");

        if (!canManageContracts)
        {
            if (entity.Status is not ContractStatus.Draft and not ContractStatus.WaitingCustomerSignature)
                return new(false, id, "Hợp đồng không còn ở trạng thái cho phép tài xế cập nhật nội dung.");

            var vehicleStillAssigned = await db.Vehicles.AsNoTracking().AnyAsync(x =>
                x.Id == entity.VehicleId &&
                x.IsActive &&
                x.AssignedDriverId == currentUserId,
                ct);

            if (!vehicleStillAssigned)
                return new(false, id, "Xe của hợp đồng không còn được gán cho tài xế nên không thể tiếp tục xử lý.");

            if (MissingCustomerInfo(entity))
                return new(false, id, "Hợp đồng chưa có đầy đủ khách hàng/người thuê. Vui lòng yêu cầu Owner/Admin cập nhật trước khi tài xế tiếp tục xử lý.");

            if (DriverChangedCustomer(entity, request))
                return new(false, id, "Tài xế không được thay đổi khách hàng/người thuê của hợp đồng sau khi hợp đồng đã được tạo hoặc phát xuống.");

            var passengerCount = CountPassengers(request.Passengers);
            if (passengerCount > 20)
                return new(false, id, "Danh sách hành khách tối đa 20 người theo mẫu PDF hiện tại.");

            // Driver được cập nhật toàn bộ nhóm Nội dung hợp đồng và Danh sách hành khách.
            // Thông tin hợp đồng, phương tiện và khách hàng luôn giữ nguyên.
            entity.PickupLocation = N(request.PickupLocation);
            entity.DropoffLocation = N(request.DropoffLocation);
            entity.StartTime = request.StartTime;
            entity.EndTime = request.EndTime;
            entity.RouteDescription = N(request.RouteDescription);
            entity.TotalKilometers = request.TotalKilometers;
            entity.ContractValue = request.ContractValue;
            entity.PaymentMethod = N(request.PaymentMethod);
            entity.PaymentTime = N(request.PaymentTime);
            entity.Note = N(request.Note);

            db.ContractPassengers.RemoveRange(entity.Passengers);
            AddPassengers(entity, request.Passengers, currentUserId);
            entity.ActualPassengerCount = passengerCount;
            entity.Status = ContractStatus.WaitingCustomerSignature;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = currentUserId;

            entity.AuditLogs.Add(new ContractAuditLog
            {
                ContractId = entity.Id,
                Action = "DriverUpdatedContractInfo",
                UserId = currentUserId,
                UserName = await GetUserDisplayNameAsync(db, currentUserId, ct),
                NewDataJson = $"{{\"customerId\":\"{entity.CustomerId}\",\"passengerCount\":{passengerCount},\"contractValue\":{request.ContractValue ?? 0}}}",
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
            return new(true, id, "Đã lưu nội dung hợp đồng và danh sách hành khách. Thông tin hợp đồng, phương tiện và khách hàng không thay đổi. Hợp đồng đang chờ khách hàng ký.");
        }

        var assignment = await ResolveAssignmentAsync(db, request, currentUserId, access, ct);
        if (assignment.Error is not null)
            return new(false, id, assignment.Error);

        var vehicle = assignment.Vehicle!;
        var driver = assignment.Driver!;
        if (!driver.CompanyProfileId.HasValue || driver.CompanyProfile is null)
            return new(false, id, "Tài xế chưa được gán CompanyProfile.");

        var companyProfileId = driver.CompanyProfileId.Value;
        var companyProfile = driver.CompanyProfile;

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null) return new(false, id, "Chưa cấu hình loại hợp đồng phù hợp.");
        var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null) return new(false, id, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        var customer = await ResolveCustomerAsync(
            db,
            request,
            currentUserId,
            canManageContracts,
            access.IsOwner,
            companyProfileId,
            entity.CustomerId,
            allowPlaceholder: false,
            ct);

        entity.DriverId = driver.Id;
        entity.ContractTypeId = type.Id;
        entity.ContractTemplateId = template.Id;
        entity.ContractContentSnapshot = template.HtmlContent;
        entity.CompanyProfileId = companyProfileId;
        entity.CustomerId = customer.Id;
        entity.VehicleId = vehicle.Id;
        entity.BusinessType = request.BusinessType;
        Apply(entity, request);
        ApplySnapshots(entity, driver, companyProfile, customer, vehicle);
        entity.ContractDataJson = ContractSnapshotData.Capture(companyProfile, driver, customer, vehicle).ToJson();
        db.ContractPassengers.RemoveRange(entity.Passengers);
        AddPassengers(entity, request.Passengers, currentUserId);
        entity.Status = canManageContracts
            ? ContractStatus.Draft
            : ContractStatus.WaitingCustomerSignature;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;

        if (canManageContracts)
        {
            var actorName = await GetUserDisplayNameAsync(db, currentUserId, ct);
            entity.AuditLogs.Add(new ContractAuditLog
            {
                ContractId = entity.Id,
                Action = "AssignedToDriver",
                UserId = currentUserId,
                UserName = actorName,
                NewDataJson = $"{{\"driverId\":\"{driver.Id}\",\"vehicleId\":\"{vehicle.Id}\"}}",
                CreatedAt = DateTime.UtcNow
            });
            db.DriverNotifications.Add(new DriverNotification
            {
                DriverId = driver.Id,
                Type = "ContractAssigned",
                Title = "Hợp đồng được cập nhật/phát xuống",
                Message = $"Hợp đồng {entity.ContractNumber} đã được {actorName} cập nhật và phát xuống cho bạn xử lý.",
                LinkUrl = $"/driver/contracts/{entity.Id}",
                RelatedContractId = entity.Id,
                RelatedVehicleId = vehicle.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return new(true, id, canManageContracts
            ? "Đã cập nhật và phát hợp đồng xuống tài xế. Tài xế chỉ được điều chỉnh nội dung hợp đồng, danh sách hành khách và ghi nhận chữ ký khách hàng."
            : "Đã lưu nội dung hợp đồng và danh sách hành khách. Hợp đồng đang chờ khách hàng ký.");
    }

    public async Task<SaveContractResult> CancelByDriverAsync(
        Guid id,
        string currentUserId,
        string? reason = null,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity is null)
            return new(false, null, "Không tìm thấy hợp đồng.");

        if (!string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Bạn không có quyền hủy hợp đồng này.");

        var wasDispatchedByManager =
            !string.Equals(entity.CreatedBy, currentUserId, StringComparison.Ordinal) ||
            await db.ContractAuditLogs.AsNoTracking().AnyAsync(x =>
                x.ContractId == id && x.Action == "AssignedToDriver",
                ct);

        if (wasDispatchedByManager)
            return new(false, id, "Hợp đồng do Owner/Admin phát xuống. Tài xế chỉ được cập nhật nội dung hợp đồng, danh sách hành khách và lấy chữ ký khách hàng.");

        if (entity.Status == ContractStatus.Cancelled)
            return new(false, id, "Hợp đồng đã được hủy trước đó.");

        if (entity.Status is ContractStatus.Completed or ContractStatus.Expired or ContractStatus.Invalidated)
            return new(false, id, "Hợp đồng đã hoàn tất, đã hết hạn hoặc đã vô hiệu hóa nên không thể hủy.");

        if (entity.Signatures.Count != 0)
            return new(false, id, "Hợp đồng đã có chân ký nên không thể hủy.");

        entity.Status = ContractStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        entity.CancelReason = string.IsNullOrWhiteSpace(reason)
            ? "Tài xế hủy trước khi các bên ký."
            : reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;

        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã hủy hợp đồng.");
    }

    public async Task<bool> DeleteAsync(Guid id, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || entity.Status is ContractStatus.Completed or ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Invalidated) return false;

        var access = await GetAccessAsync(db, currentUserId, ct);
        var canManageContracts = access.IsOwner || access.IsAdmin;
        if (access.IsAdmin && !access.IsOwner && entity.CompanyProfileId != access.CompanyProfileId) return false;
        if (!canManageContracts && !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal)) return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = currentUserId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private sealed record UserAccess(bool IsOwner, bool IsAdmin, Guid? CompanyProfileId);

    private async Task<UserAccess> GetAccessAsync(ApplicationDbContext db, string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new(false, false, null);

        var accessInfo = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && !x.IsDeleted && x.IsActive)
            .Select(x => new
            {
                x.CompanyProfileId,
                HasOperationalCompany = x.CompanyProfileId.HasValue &&
                                        x.CompanyProfile != null &&
                                        x.CompanyProfile.IsActive &&
                                        !x.CompanyProfile.IsDeleted
            })
            .FirstOrDefaultAsync(ct);

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.IsDeleted || !user.IsActive || accessInfo is null)
            return new(false, false, null);

        var isOwner = await userManager.IsInRoleAsync(user, "Owner");
        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");

        // Owner can operate globally without a company assignment. Operational users must belong
        // to an active, visible company; a hidden parent company immediately removes app access.
        if (!isOwner && !accessInfo.HasOperationalCompany)
            return new(false, false, null);

        return new(isOwner, isAdmin, accessInfo.CompanyProfileId);
    }

    private static async Task<string> GetUserDisplayNameAsync(
        ApplicationDbContext db,
        string? userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "Hệ thống";

        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(ct)
            ?? userId;
    }

    private static async Task<ContractType?> ResolveTypeAsync(ApplicationDbContext db, SaveContractRequest request, CancellationToken ct)
    {
        var code = request.BusinessType switch
        {
            ContractBusinessType.Passenger => "PASSENGER",
            ContractBusinessType.Cargo => "CARGO",
            _ => null
        };

        if (code is null)
            return null;

        // Loại hàng hóa hiện được seed ở trạng thái chưa sử dụng nên sẽ không được trả về tại đây.
        return await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct);
    }

    private static async Task<Customer> ResolveCustomerAsync(
        ApplicationDbContext db,
        SaveContractRequest request,
        string currentUserId,
        bool canManageContracts,
        bool isOwner,
        Guid companyProfileId,
        Guid? existingCustomerId,
        bool allowPlaceholder,
        CancellationToken ct)
    {
        var hasName = !string.IsNullOrWhiteSpace(request.CustomerName);
        var hasPhone = !string.IsNullOrWhiteSpace(request.CustomerPhone);

        IQueryable<Customer> AccessibleCustomers()
        {
            var query = db.Customers.AsQueryable();

            if (isOwner)
                return query;

            if (canManageContracts)
                return query.Where(x =>
                    x.CreatedByDriver.CompanyProfileId == companyProfileId ||
                    x.Contracts.Any(c => c.CompanyProfileId == companyProfileId));

            return query.Where(x => x.CreatedByDriverId == currentUserId);
        }

        Customer? customer = null;
        var isNewCustomer = false;
        if (request.CustomerId.HasValue)
        {
            var customerId = request.CustomerId.Value;

            // Khi Owner/Admin cập nhật hợp đồng, khách hàng hiện tại luôn được phép tải lại
            // để quản trị. Driver không đi qua nhánh cập nhật khách hàng này.
            if (existingCustomerId == customerId)
            {
                customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, ct);
            }
            else
            {
                customer = await AccessibleCustomers().FirstOrDefaultAsync(x => x.Id == customerId, ct);
            }

            if (customer is null && !canManageContracts)
                throw new InvalidOperationException("Tài xế chỉ được chọn khách hàng do chính mình đã tạo trước đó.");
        }

        if (customer is null && hasPhone)
        {
            var phone = request.CustomerPhone!.Trim();
            customer = await AccessibleCustomers().FirstOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        }

        if (customer is null)
        {
            isNewCustomer = true;
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                // Giữ tên cột cũ để tương thích database; giá trị hiện tại là ID người tạo.
                CreatedByDriverId = currentUserId,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow
            };
            db.Customers.Add(customer);
        }

        // Driver chỉ được tạo khách hàng mới hoặc chọn lại khách hàng đã có.
        // Hồ sơ khách hàng đã tồn tại không được Driver chỉnh sửa qua hợp đồng.
        if (!canManageContracts && !isNewCustomer)
        {
            customer.LastUsedAt = DateTime.UtcNow;
            return customer;
        }

        customer.FullName = hasName ? request.CustomerName.Trim() : "Chờ tài xế bổ sung";
        customer.PhoneNumber = hasPhone ? request.CustomerPhone.Trim() : PendingPhone();
        customer.CitizenId = N(request.CustomerCitizenId);
        customer.Address = N(request.CustomerAddress);
        customer.LastUsedAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;

        if (!allowPlaceholder && MissingCustomerInfo(request))
            throw new InvalidOperationException("Vui lòng nhập tên và số điện thoại khách hàng.");

        return customer;
    }

    private async Task<(Vehicle? Vehicle, ApplicationUser? Driver, string? Error)> ResolveAssignmentAsync(
        ApplicationDbContext db,
        SaveContractRequest request,
        string currentUserId,
        UserAccess access,
        CancellationToken ct)
    {
        var canManageContracts = access.IsOwner || access.IsAdmin;

        if (canManageContracts && !request.CompanyProfileId.HasValue)
            return (null, null, "Vui lòng chọn Công ty/Văn phòng trước khi chọn xe và tài xế.");

        if (access.IsAdmin && !access.IsOwner && request.CompanyProfileId != access.CompanyProfileId)
            return (null, null, "Admin chỉ được tạo hợp đồng cho Công ty/Văn phòng đã được gán.");

        Vehicle? vehicle = null;
        if (request.VehicleId.HasValue)
            vehicle = await db.Vehicles.FirstOrDefaultAsync(x => x.Id == request.VehicleId.Value && x.IsActive, ct);
        if (vehicle is null && !string.IsNullOrWhiteSpace(request.VehiclePlate))
        {
            var plate = request.VehiclePlate.Trim().ToUpperInvariant();
            vehicle = await db.Vehicles.FirstOrDefaultAsync(x => x.PlateNumber == plate && x.IsActive, ct);
        }

        if (vehicle is null)
            return (null, null, "Vui lòng chọn xe đang hoạt động.");

        if (vehicle.CompanyProfileId is null)
            return (null, null, "Xe chưa được gán Công ty/Văn phòng nên chưa thể tạo hợp đồng.");

        if (request.CompanyProfileId.HasValue && vehicle.CompanyProfileId != request.CompanyProfileId.Value)
            return (null, null, "Xe không thuộc Công ty/Văn phòng đã chọn.");

        if (access.IsAdmin && !access.IsOwner && vehicle.CompanyProfileId != access.CompanyProfileId)
            return (null, null, "Admin chỉ được chọn xe thuộc CompanyProfile được gán.");

        string driverId;
        if (canManageContracts)
        {
            // Xe đã có tài xế thì tài xế của xe là dữ liệu chuẩn, không cho form gán sang người khác.
            driverId = !string.IsNullOrWhiteSpace(vehicle.AssignedDriverId)
                ? vehicle.AssignedDriverId
                : request.DriverId;

            if (string.IsNullOrWhiteSpace(driverId))
                return (vehicle, null, "Xe chưa được gán tài xế. Vui lòng chọn tài xế để hệ thống tự động gán cho xe.");
        }
        else
        {
            driverId = currentUserId;
        }

        var driver = await db.Users
            .Include(x => x.CompanyProfile)
            .FirstOrDefaultAsync(x => x.Id == driverId && x.IsActive && !x.IsDeleted && x.RegistrationStatus == "Approved", ct);

        if (driver is null || !await userManager.IsInRoleAsync(driver, "Driver"))
            return (vehicle, null, "Không tìm thấy tài xế, tài khoản đã bị khóa hoặc tài khoản không có quyền Driver.");

        if (driver.CompanyProfileId is null || driver.CompanyProfile is null)
            return (vehicle, null, "Tài xế chưa được gán CompanyProfile.");

        if (!driver.CompanyProfile.IsActive || driver.CompanyProfile.IsDeleted)
            return (vehicle, null, "CompanyProfile của tài xế đã ngừng hoạt động hoặc đã bị ẩn.");

        if (vehicle.CompanyProfileId != driver.CompanyProfileId)
            return (vehicle, null, "Xe và tài xế phải thuộc cùng CompanyProfile.");

        if (!canManageContracts &&
            !string.Equals(vehicle.AssignedDriverId, driver.Id, StringComparison.Ordinal))
            return (vehicle, null, "Tài xế chỉ được dùng xe đang hoạt động đã được gán cho mình.");

        if (canManageContracts && string.IsNullOrWhiteSpace(vehicle.AssignedDriverId))
        {
            var otherAssignedVehiclePlate = await db.Vehicles.AsNoTracking()
                .Where(x => x.Id != vehicle.Id && x.AssignedDriverId == driver.Id)
                .Select(x => x.PlateNumber)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(otherAssignedVehiclePlate))
            {
                return (
                    vehicle,
                    null,
                    $"Tài xế đã được gán cho xe {otherAssignedVehiclePlate}. Vui lòng bỏ gán xe cũ trước khi gán xe {vehicle.PlateNumber}.");
            }

            // Xe chưa có tài xế: lựa chọn trên hợp đồng đồng thời trở thành tài xế được gán cho xe.
            // Mỗi tài xế chỉ được gán cho một xe tại cùng thời điểm.
            vehicle.AssignedDriverId = driver.Id;
            vehicle.UpdatedAt = DateTime.UtcNow;
            vehicle.UpdatedBy = currentUserId;

            db.DriverNotifications.Add(new DriverNotification
            {
                DriverId = driver.Id,
                Type = "VehicleAssigned",
                Title = "Bạn được gán phương tiện",
                Message = $"Phương tiện {vehicle.PlateNumber} đã được gán cho tài khoản của bạn khi phát hợp đồng.",
                LinkUrl = "/driver/dashboard",
                RelatedVehicleId = vehicle.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        return (vehicle, driver, null);
    }


    private static void ApplyImmutableSnapshot(ContractDetailDto detail, ContractSnapshotData snapshot)
    {
        // Khi JSON snapshot tồn tại, đây là nguồn dữ liệu duy nhất của hồ sơ lịch sử.
        // Không dùng toán tử fallback sang navigation hiện tại vì kể cả giá trị trống
        // tại thời điểm chụp cũng phải được giữ nguyên, không tự lấp bằng dữ liệu mới.
        detail.CompanyName = snapshot.Company.DisplayName;
        detail.CompanyRepresentativeName = snapshot.Company.RepresentativeName;
        detail.CompanyRepresentativeSignatureFileUrl = snapshot.Company.RepresentativeSignatureFileUrl;
        detail.CompanyRepresentativeSignedAt = snapshot.Company.RepresentativeSignedAt;

        detail.DriverName = snapshot.Driver.FullName ?? string.Empty;
        detail.DriverLicenseClass = snapshot.Driver.DriverLicenseClass;
        detail.DriverSignatureFileUrl = snapshot.Driver.SignatureFileUrl;
        detail.DriverSignedAt = snapshot.Driver.SignedAt;

        detail.CustomerName = snapshot.Customer.FullName ?? string.Empty;
        detail.CustomerPhone = snapshot.Customer.PhoneNumber ?? string.Empty;
        detail.CustomerCitizenId = snapshot.Customer.CitizenId;
        detail.CustomerAddress = snapshot.Customer.Address;

        detail.VehiclePlate = snapshot.Vehicle.PlateNumber;
        detail.VehicleCode = snapshot.Vehicle.VehicleCode;
        detail.VehicleBrand = string.IsNullOrWhiteSpace(snapshot.Vehicle.BrandModel)
            ? null
            : snapshot.Vehicle.BrandModel;
        detail.SeatCount = snapshot.Vehicle.SeatCount;
        detail.OwnerName = snapshot.Vehicle.OwnerName;
        detail.OwnerCitizenId = snapshot.Vehicle.OwnerCitizenId;
        detail.OwnerCitizenIdIssuedDate = snapshot.Vehicle.OwnerCitizenIdIssuedDate;
        detail.VehicleOwnerSignatureFileUrl = snapshot.Vehicle.OwnerSignatureFileUrl;
        detail.VehicleOwnerSignedAt = snapshot.Vehicle.OwnerSignedAt;
    }

    private static void Apply(Contract e, SaveContractRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.ContractNumber)) e.ContractNumber = r.ContractNumber.Trim();
        e.AreaCode = string.IsNullOrWhiteSpace(r.AreaCode) ? "N/A" : r.AreaCode.Trim();
        e.CargoName = N(r.CargoName);
        e.CargoWeight = r.CargoWeight;
        e.CargoUnit = N(r.CargoUnit);
        e.ActualPassengerCount = CountPassengers(r.Passengers);
        e.SecondDriverName = N(r.SecondDriverName);
        e.SecondDriverLicenseClass = N(r.SecondDriverLicenseClass);
        e.PickupLocation = N(r.PickupLocation);
        e.DropoffLocation = N(r.DropoffLocation);
        e.StartTime = r.StartTime;
        e.EndTime = r.EndTime;
        e.RouteDescription = N(r.RouteDescription);
        e.TotalKilometers = r.TotalKilometers;
        e.ContractValue = r.ContractValue;
        e.PaymentMethod = N(r.PaymentMethod);
        e.PaymentTime = N(r.PaymentTime);
        e.Note = N(r.Note);
    }

    private static void ApplySnapshots(Contract e, ApplicationUser driver, CompanyProfile company, Customer customer, Vehicle vehicle)
    {
        e.CompanyNameSnapshot = company.CompanyName;
        e.CompanyTaxCodeSnapshot = company.TaxCode;
        e.CompanyAddressSnapshot = company.Address;
        e.CompanyRepresentativeSnapshot = company.RepresentativeName;
        e.CompanyRepresentativePositionSnapshot = company.RepresentativePosition;
        e.DriverNameSnapshot = driver.FullName;
        e.DriverLicenseNumberSnapshot = driver.DriverLicenseNumber;
        e.DriverLicenseClassSnapshot = driver.DriverLicenseClass;
        e.CustomerNameSnapshot = customer.FullName;
        e.CustomerPhoneSnapshot = customer.PhoneNumber;
        e.CustomerCitizenIdSnapshot = customer.CitizenId;
        e.CustomerAddressSnapshot = customer.Address;
        e.VehiclePlateSnapshot = vehicle.PlateNumber;
        e.VehicleBrandSnapshot = vehicle.Brand;
        e.VehicleOwnerNameSnapshot = vehicle.OwnerName;
        e.VehicleOwnerCitizenIdSnapshot = vehicle.OwnerCitizenId;
    }

    private static void ApplyCustomerSnapshots(Contract contract, Customer customer)
    {
        contract.CustomerNameSnapshot = customer.FullName;
        contract.CustomerPhoneSnapshot = customer.PhoneNumber;
        contract.CustomerCitizenIdSnapshot = customer.CitizenId;
        contract.CustomerAddressSnapshot = customer.Address;
    }

    private static void AddPassengers(Contract e, IEnumerable<ContractPassengerDto> passengers, string userId)
    {
        foreach (var item in passengers.Where(x => !string.IsNullOrWhiteSpace(x.FullName)).Select((x, i) => (x, i)))
            e.Passengers.Add(new ContractPassenger { SortOrder = item.i + 1, FullName = item.x.FullName.Trim(), BirthYear = item.x.BirthYear, Note = N(item.x.Note), CreatedBy = userId });
    }

    private static bool MissingCustomerInfo(SaveContractRequest request)
    {
        var customerName = request.CustomerName?.Trim();
        var customerPhone = request.CustomerPhone?.Trim();
        return string.IsNullOrWhiteSpace(customerName)
               || string.Equals(customerName, "Chờ tài xế bổ sung", StringComparison.OrdinalIgnoreCase)
               || string.IsNullOrWhiteSpace(customerPhone)
               || customerPhone?.StartsWith("PEND", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool MissingCustomerInfo(Contract contract)
        => string.IsNullOrWhiteSpace(contract.CustomerNameSnapshot)
           || string.Equals(contract.CustomerNameSnapshot.Trim(), "Chờ tài xế bổ sung", StringComparison.OrdinalIgnoreCase)
           || string.IsNullOrWhiteSpace(contract.CustomerPhoneSnapshot)
           || contract.CustomerPhoneSnapshot.StartsWith("PEND", StringComparison.OrdinalIgnoreCase);

    private static bool DriverChangedCustomer(Contract contract, SaveContractRequest request)
        => request.CustomerId != contract.CustomerId
           || !Same(request.CustomerName, contract.CustomerNameSnapshot)
           || !Same(request.CustomerPhone, contract.CustomerPhoneSnapshot)
           || !Same(request.CustomerCitizenId, contract.CustomerCitizenIdSnapshot)
           || !Same(request.CustomerAddress, contract.CustomerAddressSnapshot);

    private static bool Same(string? left, string? right)
        => string.Equals(N(left), N(right), StringComparison.Ordinal);

    private static string PendingPhone()
        => ($"PEND{Guid.NewGuid():N}")[..20];

    private static string BusinessCode(ContractBusinessType type) => type switch
    {
        ContractBusinessType.Cargo => "HH",
        _ => "HK"
    };

    private static int CountPassengers(IEnumerable<ContractPassengerDto> passengers)
        => passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName));

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
