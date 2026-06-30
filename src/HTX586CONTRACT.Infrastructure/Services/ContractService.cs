using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Contracts;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
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
        if (filter.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= filter.FromDate.Value.Date);
        if (filter.ToDate.HasValue) query = query.Where(x => x.CreatedAt < filter.ToDate.Value.Date.AddDays(1));

        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new ContractListItemDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                BusinessType = x.BusinessType,
                CompanyName = x.CompanyNameSnapshot,
                CustomerName = x.CustomerNameSnapshot,
                DriverName = x.DriverNameSnapshot,
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
        return await db.Contracts.AsNoTracking()
            .Include(x => x.Passengers)
            .Include(x => x.Signatures)
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
                VehicleBrand = x.VehicleBrandSnapshot,
                ActualPassengerCount = x.ActualPassengerCount,
                OwnerName = x.VehicleOwnerNameSnapshot,
                OwnerCitizenId = x.VehicleOwnerCitizenIdSnapshot,
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
    }

    public async Task<SaveContractResult> CreateAsync(SaveContractRequest request, string currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return new(false, null, "Vui lòng nhập tên và số điện thoại khách hàng.");

        await using var db = await factory.CreateDbContextAsync(ct);
        var isAdmin = await IsAdminAsync(currentUserId);
        var driverId = isAdmin && !string.IsNullOrWhiteSpace(request.DriverId)
            ? request.DriverId
            : currentUserId;
        var driver = await db.Users.Include(x => x.CompanyProfile).FirstOrDefaultAsync(x => x.Id == driverId, ct);
        if (driver is null) return new(false, null, "Không tìm thấy tài xế.");
        if (driver.CompanyProfileId is null || driver.CompanyProfile is null)
            return new(false, null, "Tài xế chưa được gán công ty/văn phòng đại diện.");
        if (!driver.CompanyProfile.IsActive)
            return new(false, null, "Công ty/văn phòng đại diện của tài xế đã ngừng hoạt động.");

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null) return new(false, null, "Chưa cấu hình loại hợp đồng phù hợp.");
        var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null) return new(false, null, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        var customer = await ResolveCustomerAsync(db, request, driverId, currentUserId, ct);
        var vehicle = await ResolveVehicleAsync(db, request, ct);
        if (vehicle is null) return new(false, null, "Vui lòng chọn xe hợp lệ.");

        var fixedSignatureError = ValidateFixedSignatures(driver, driver.CompanyProfile, vehicle);
        if (fixedSignatureError is not null) return new(false, null, fixedSignatureError);

        var entity = new Contract
        {
            Id = Guid.NewGuid(),
            ContractNumber = string.IsNullOrWhiteSpace(request.ContractNumber)
                ? $"{DateTime.Now:yyyyMMddHHmmss}/{BusinessCode(request.BusinessType)}"
                : request.ContractNumber.Trim(),
            BusinessType = request.BusinessType,
            ContractTypeId = type.Id,
            ContractTemplateId = template.Id,
            CompanyProfileId = driver.CompanyProfileId.Value,
            DriverId = driver.Id,
            CustomerId = customer.Id,
            VehicleId = vehicle.Id,
            Status = ContractStatus.Draft,
            ContractContentSnapshot = template.HtmlContent,
            CreatedBy = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        Apply(entity, request);
        ApplySnapshots(entity, driver, driver.CompanyProfile, customer, vehicle);
        AddPassengers(entity, request.Passengers, currentUserId);
        db.Contracts.Add(entity);
        await db.SaveChangesAsync(ct);
        return new(true, entity.Id, "Đã tạo hợp đồng. Chỉ cần khách hàng ký để hoàn tất.");
    }

    public async Task<SaveContractResult> UpdateAsync(Guid id, SaveContractRequest request, string currentUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entity = await db.Contracts
            .Include(x => x.Passengers)
            .Include(x => x.Signatures)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return new(false, null, "Không tìm thấy hợp đồng.");
        if (entity.Status is ContractStatus.Completed or ContractStatus.Cancelled or ContractStatus.Invalidated)
            return new(false, id, "Hợp đồng đã hoàn tất, đã hủy hoặc đã vô hiệu hóa và không thể chỉnh sửa.");

        var isAdmin = await IsAdminAsync(currentUserId);
        if (!isAdmin && !string.Equals(entity.DriverId, currentUserId, StringComparison.Ordinal))
            return new(false, id, "Bạn không có quyền cập nhật hợp đồng này.");

        if (!isAdmin && entity.Signatures.Count != 0)
            return new(false, id, "Hợp đồng đã có chữ ký. Tài xế không được cập nhật nội dung hợp đồng.");

        if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone))
            return new(false, id, "Vui lòng nhập tên và số điện thoại khách hàng.");

        var driverId = isAdmin && !string.IsNullOrWhiteSpace(request.DriverId)
            ? request.DriverId
            : entity.DriverId;
        var driver = await db.Users.Include(x => x.CompanyProfile).FirstOrDefaultAsync(x => x.Id == driverId, ct);
        if (driver?.CompanyProfileId is null || driver.CompanyProfile is null)
            return new(false, id, "Tài xế chưa được gán công ty/văn phòng đại diện.");

        var type = await ResolveTypeAsync(db, request, ct);
        if (type is null) return new(false, id, "Chưa cấu hình loại hợp đồng phù hợp.");
        var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive, ct);
        if (template is null) return new(false, id, "Chưa cấu hình mẫu hợp đồng đang hoạt động.");

        var customer = await ResolveCustomerAsync(db, request, driverId, currentUserId, ct);
        var vehicle = await ResolveVehicleAsync(db, request, ct);
        if (vehicle is null) return new(false, id, "Vui lòng chọn xe hợp lệ.");

        var fixedSignatureError = ValidateFixedSignatures(driver, driver.CompanyProfile, vehicle);
        if (fixedSignatureError is not null) return new(false, id, fixedSignatureError);

        entity.DriverId = driver.Id;
        entity.ContractTypeId = type.Id;
        entity.ContractTemplateId = template.Id;
        entity.ContractContentSnapshot = template.HtmlContent;
        entity.CompanyProfileId = driver.CompanyProfileId.Value;
        entity.CustomerId = customer.Id;
        entity.VehicleId = vehicle.Id;
        entity.BusinessType = request.BusinessType;
        Apply(entity, request);
        ApplySnapshots(entity, driver, driver.CompanyProfile, customer, vehicle);
        db.ContractPassengers.RemoveRange(entity.Passengers);
        AddPassengers(entity, request.Passengers, currentUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = currentUserId;
        await db.SaveChangesAsync(ct);
        return new(true, id, "Đã cập nhật hợp đồng.");
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

        if (entity.Status == ContractStatus.Cancelled)
            return new(false, id, "Hợp đồng đã được hủy trước đó.");

        if (entity.Status is ContractStatus.Completed or ContractStatus.Invalidated)
            return new(false, id, "Hợp đồng đã hoàn tất hoặc đã vô hiệu hóa nên không thể hủy.");

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
        if (entity is null || entity.Status == ContractStatus.Completed) return false;
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = currentUserId;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> IsAdminAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var user = await userManager.FindByIdAsync(userId);
        return user is not null && (await userManager.IsInRoleAsync(user, "Owner") || await userManager.IsInRoleAsync(user, "Admin"));
    }

    private static async Task<ContractType?> ResolveTypeAsync(ApplicationDbContext db, SaveContractRequest request, CancellationToken ct)
    {
        if (request.ContractTypeId.HasValue)
            return await db.ContractTypes.FirstOrDefaultAsync(x => x.Id == request.ContractTypeId.Value && x.IsActive, ct);

        var code = request.BusinessType switch
        {
            ContractBusinessType.Cargo => "CARGO",
            ContractBusinessType.LongDistance => "LONG_DISTANCE",
            _ => "DRIVER"
        };
        return await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct);
    }

    private static async Task<Customer> ResolveCustomerAsync(ApplicationDbContext db, SaveContractRequest request, string driverId, string currentUserId, CancellationToken ct)
    {
        Customer? customer = null;
        if (request.CustomerId.HasValue)
            customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, ct);
        if (customer is null && !string.IsNullOrWhiteSpace(request.CustomerPhone))
            customer = await db.Customers.FirstOrDefaultAsync(x => x.PhoneNumber == request.CustomerPhone && x.CreatedByDriverId == driverId, ct);

        if (customer is null)
        {
            customer = new Customer { Id = Guid.NewGuid(), CreatedByDriverId = driverId, CreatedBy = currentUserId };
            db.Customers.Add(customer);
        }

        customer.FullName = request.CustomerName.Trim();
        customer.PhoneNumber = request.CustomerPhone.Trim();
        customer.CitizenId = N(request.CustomerCitizenId);
        customer.Address = N(request.CustomerAddress);
        customer.LastUsedAt = DateTime.UtcNow;
        return customer;
    }

    private static async Task<Vehicle?> ResolveVehicleAsync(ApplicationDbContext db, SaveContractRequest request, CancellationToken ct)
    {
        if (request.VehicleId.HasValue)
            return await db.Vehicles.FirstOrDefaultAsync(x => x.Id == request.VehicleId.Value && x.IsActive, ct);
        if (!string.IsNullOrWhiteSpace(request.VehiclePlate))
        {
            var plate = request.VehiclePlate.Trim().ToUpperInvariant();
            return await db.Vehicles.FirstOrDefaultAsync(x => x.PlateNumber == plate && x.IsActive, ct);
        }
        return null;
    }

    private static string? ValidateFixedSignatures(ApplicationUser driver, CompanyProfile company, Vehicle vehicle)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(company.RepresentativeSignatureFileUrl))
            missing.Add("chữ ký cố định Company/văn phòng đại diện");

        if (string.IsNullOrWhiteSpace(vehicle.OwnerSignatureFileUrl))
            missing.Add("chữ ký cố định chủ sở hữu xe");

        if (string.IsNullOrWhiteSpace(driver.DriverSignatureFileUrl))
            missing.Add("chữ ký cố định tài xế");

        return missing.Count == 0
            ? null
            : $"Chưa thể tạo/cập nhật hợp đồng. Còn thiếu: {string.Join(", ", missing)}.";
    }

    private static void Apply(Contract e, SaveContractRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.ContractNumber)) e.ContractNumber = r.ContractNumber.Trim();
        e.AreaCode = string.IsNullOrWhiteSpace(r.AreaCode) ? "N/A" : r.AreaCode.Trim();
        e.CargoName = N(r.CargoName);
        e.CargoWeight = r.CargoWeight;
        e.CargoUnit = N(r.CargoUnit);
        e.ActualPassengerCount = r.ActualPassengerCount;
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

    private static void AddPassengers(Contract e, IEnumerable<ContractPassengerDto> passengers, string userId)
    {
        foreach (var item in passengers.Where(x => !string.IsNullOrWhiteSpace(x.FullName)).Select((x, i) => (x, i)))
            e.Passengers.Add(new ContractPassenger { SortOrder = item.i + 1, FullName = item.x.FullName.Trim(), BirthYear = item.x.BirthYear, Note = N(item.x.Note), CreatedBy = userId });
    }

    private static string BusinessCode(ContractBusinessType type) => type switch
    {
        ContractBusinessType.Cargo => "HH",
        ContractBusinessType.LongDistance => "ĐD",
        _ => "TX"
    };

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
