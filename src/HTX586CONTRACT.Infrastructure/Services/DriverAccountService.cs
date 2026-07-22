using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.DriverAccounts;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HTX586CONTRACT.Application.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components.Forms;
using System.Security.Cryptography;
using System.Linq.Expressions;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class DriverAccountService( 
    UserManager<ApplicationUser> userManager,
    IDbContextFactory<ApplicationDbContext> factory,
    IHostEnvironment environment,
    IOptions<FileStorageOptions> fileStorageOptions) : IDriverAccountService
{
    public async Task<string> CreateAsync(CreateDriverAccountRequest request, CancellationToken ct = default)
    {
        ValidateCompany(request.CompanyProfileId);
        if (string.IsNullOrWhiteSpace(request.UserName)) throw new InvalidOperationException("Vui lòng nhập tên đăng nhập.");
        if (string.IsNullOrWhiteSpace(request.Password)) throw new InvalidOperationException("Vui lòng nhập mật khẩu.");
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new InvalidOperationException("Vui lòng nhập họ tên tài xế.");
        var phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        await EnsureCompanyAsync(request.CompanyProfileId, ct);
        await EnsureLoginIdentifiersAvailableAsync(request.UserName, phoneNumber, null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            EmployeeCode = N(request.EmployeeCode),
            PhoneNumber = phoneNumber,
            Email = N(request.Email),
            CompanyProfileId = request.CompanyProfileId,
            CitizenId = N(request.CitizenId),
            CitizenIdIssuedDate = request.CitizenIdIssuedDate,
            CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace),
            DateOfBirth = request.DateOfBirth,
            Address = N(request.Address),
            AreaCode = N(request.AreaCode),
            DriverLicenseNumber = N(request.DriverLicenseNumber),
            DriverLicenseClass = N(request.DriverLicenseClass),
            DriverLicenseIssuedDate = request.DriverLicenseIssuedDate,
            DriverLicenseExpiryDate = request.DriverLicenseExpiryDate,
            // Tài khoản do Owner/Admin cấp luôn phải đổi mật khẩu và tạo chữ ký ở lần đăng nhập đầu tiên.
            MustChangePassword = true,
            RegistrationStatus = "Approved",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        Ensure(result);
        var roleResult = await userManager.AddToRoleAsync(user, "Driver");
        Ensure(roleResult);
        return user.Id;
    }

    public async Task<string> SubmitRegistrationAsync(SelfRegisterDriverRequest request, CancellationToken ct = default)
    {
        ValidateCompany(request.CompanyProfileId);
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("Vui lòng nhập tên đăng nhập và mật khẩu.");
        var phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(request.FullName) || request.DateOfBirth is null || string.IsNullOrWhiteSpace(request.AreaCode) ||
            string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.CitizenId) || request.CitizenIdIssuedDate is null ||
            string.IsNullOrWhiteSpace(request.CitizenIdIssuedPlace))
            throw new InvalidOperationException("Vui lòng nhập đầy đủ thông tin cá nhân và CCCD.");
        if (string.IsNullOrWhiteSpace(request.DriverLicenseNumber) || string.IsNullOrWhiteSpace(request.DriverLicenseClass) ||
            request.DriverLicenseIssuedDate is null || request.DriverLicenseExpiryDate is null)
            throw new InvalidOperationException("Vui lòng nhập đầy đủ thông tin giấy phép lái xe.");
        if (request.DriverLicenseExpiryDate.Value.Date < DateTime.Today)
            throw new InvalidOperationException("Giấy phép lái xe đã hết hạn, không thể gửi yêu cầu đăng ký.");
        if (string.IsNullOrWhiteSpace(request.SignatureDataUrl))
            throw new InvalidOperationException("Vui lòng ký tên trước khi gửi yêu cầu.");

        await EnsureCompanyAsync(request.CompanyProfileId, ct);
        await EnsureLoginIdentifiersAvailableAsync(request.UserName, phoneNumber, null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = phoneNumber,
            CompanyProfileId = request.CompanyProfileId,
            DateOfBirth = request.DateOfBirth,
            AreaCode = N(request.AreaCode),
            Address = N(request.Address),
            CitizenId = N(request.CitizenId),
            CitizenIdIssuedDate = request.CitizenIdIssuedDate,
            CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace),
            DriverLicenseNumber = N(request.DriverLicenseNumber),
            DriverLicenseClass = N(request.DriverLicenseClass),
            DriverLicenseIssuedDate = request.DriverLicenseIssuedDate,
            DriverLicenseExpiryDate = request.DriverLicenseExpiryDate,
            RegistrationStatus = "Pending",
            RegistrationRequestedAt = DateTime.UtcNow,
            IsActive = false,
            // Tài xế tự đăng ký đã tự đặt mật khẩu và ký tên theo yêu cầu,
            // nên sau khi được duyệt không phải đổi mật khẩu hoặc ký lại.
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        Ensure(await userManager.CreateAsync(user, request.Password));
        try
        {
            Ensure(await userManager.AddToRoleAsync(user, "Driver"));
            var stored = await SaveRegistrationSignatureAsync(user.Id, request.SignatureDataUrl, ct);
            user.DriverSignatureFileUrl = stored.Url;
            user.DriverSignatureHash = stored.Hash;
            user.DriverSignedAt = stored.SavedAt;
            user.DriverSignatureIsActive = true;
            Ensure(await userManager.UpdateAsync(user));
            return user.Id;
        }
        catch
        {
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = "SELF_REGISTRATION_ROLLBACK";
            user.IsActive = false;
            Ensure(await userManager.UpdateAsync(user));
            throw;
        }
    }

    public async Task<IReadOnlyList<DriverRegistrationRequestDto>> GetPendingRegistrationsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db)
            .OrderByDescending(x => x.RegistrationRequestedAt)
            .Select(RegistrationProjection)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnseenPendingRegistrationCountAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await PendingRegistrationQuery(db).CountAsync(x => x.RegistrationViewedAt == null, ct);
    }

    public async Task<DriverRegistrationRequestDto?> GetRegistrationDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && !x.IsDeleted && x.RegistrationStatus == "Pending" && x.CompanyProfile != null && !x.CompanyProfile.IsDeleted)
            .Select(RegistrationProjection)
            .FirstOrDefaultAsync(ct);
    }

    public async Task MarkRegistrationViewedAsync(string userId, string viewerUserId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Users
            .Where(x => x.Id == userId && !x.IsDeleted && x.RegistrationStatus == "Pending" && x.RegistrationViewedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RegistrationViewedAt, DateTime.UtcNow)
                .SetProperty(x => x.RegistrationViewedByUserId, viewerUserId)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
    }

    public async Task ReviewRegistrationAsync(string userId, bool approve, string? note, string reviewerUserId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu đăng ký.");
        EnsureNotDeleted(user, "Không tìm thấy yêu cầu đăng ký.");
        await EnsureDriverRoleAsync(user);
        if (!string.Equals(user.RegistrationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Yêu cầu này đã được xử lý.");
        user.RegistrationStatus = approve ? "Approved" : "Rejected";
        user.IsActive = approve;
        // Yêu cầu tự đăng ký đã có mật khẩu và chữ ký cố định hợp lệ.
        // Khi duyệt chỉ kích hoạt tài khoản, không ép tài xế đổi mật khẩu/ký lại.
        user.MustChangePassword = false;
        user.RegistrationViewedAt ??= DateTime.UtcNow;
        user.RegistrationViewedByUserId ??= reviewerUserId;
        user.RegistrationReviewedAt = DateTime.UtcNow;
        user.RegistrationReviewedByUserId = reviewerUserId;
        user.RegistrationReviewNote = N(note);
        user.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(user));
    }

    public async Task UpdateAsync(string id, UpdateDriverAccountRequest request, CancellationToken ct = default)
    {
        ValidateCompany(request.CompanyProfileId);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Vui lòng nhập họ tên tài xế.");
        var phoneNumber = VietnamPhoneNumber.NormalizeOrThrow(request.PhoneNumber);
        await EnsureCompanyAsync(request.CompanyProfileId, ct);
        var user = await userManager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");
        EnsureNotDeleted(user, "Không tìm thấy tài xế.");
        await EnsureDriverRoleAsync(user);
        await EnsureLoginIdentifiersAvailableAsync(user.UserName ?? string.Empty, phoneNumber, user.Id, ct);

        var wasActive = user.IsActive;
        var previousCompanyProfileId = user.CompanyProfileId;

        user.CompanyProfileId = request.CompanyProfileId;
        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = phoneNumber;
        user.Email = N(request.Email);
        user.CitizenId = N(request.CitizenId);
        user.CitizenIdIssuedDate = request.CitizenIdIssuedDate;
        user.CitizenIdIssuedPlace = N(request.CitizenIdIssuedPlace);
        user.DateOfBirth = request.DateOfBirth;
        user.Address = N(request.Address);
        user.AreaCode = N(request.AreaCode);
        user.DriverLicenseNumber = N(request.DriverLicenseNumber);
        user.DriverLicenseClass = N(request.DriverLicenseClass);
        user.DriverLicenseIssuedDate = request.DriverLicenseIssuedDate;
        user.DriverLicenseExpiryDate = request.DriverLicenseExpiryDate;

        // Trạng thái hoạt động được xử lý riêng bằng SetActiveAsync để việc khóa tài khoản,
        // đổi SecurityStamp và giải phóng xe luôn đi cùng một luồng nghiệp vụ.
        // Không vô hiệu hóa/xóa chữ ký khi khóa tạm thời; chữ ký gốc vẫn được giữ trong DB.
        user.IsActive = wasActive;

        // Tài khoản do Owner/Admin cấp (không có thời điểm gửi đăng ký) phải hoàn tất
        // đổi mật khẩu và tạo chữ ký lần đầu. Không cho thao tác cập nhật hồ sơ vô tình bỏ qua luồng này.
        user.MustChangePassword = user.RegistrationRequestedAt is null && user.DriverSignedAt is null
            ? true
            : request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(user));

        var companyChanged = previousCompanyProfileId != request.CompanyProfileId;

        // Khi khóa (kể cả tài khoản đã khóa từ trước), luôn chạy lại luồng giải phóng xe
        // để tự sửa các dữ liệu cũ không nhất quán. Khi mở khóa không tự gán lại xe cũ.
        if (!request.IsActive || wasActive != request.IsActive)
        {
            await SetActiveAsync(id, request.IsActive, ct);
        }
        else if (companyChanged)
        {
            // Tài xế chuyển Công ty/Văn phòng không được giữ xe thuộc đơn vị cũ.
            await ReleaseAssignedVehiclesAsync(id, "DRIVER_COMPANY_CHANGED", ct);
        }
    }

    public async Task<DriverAccountDetailDto?> GetDetailAsync(string id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (from user in db.Users.AsNoTracking()
                      join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
                      join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                      where user.Id == id && !user.IsDeleted && role.Name == "Driver" && user.CompanyProfile != null && !user.CompanyProfile.IsDeleted
                      select user)
            .Select(x => new DriverAccountDetailDto
            {
                UserId = x.Id,
                UserName = x.UserName ?? string.Empty,
                FullName = x.FullName,
                EmployeeCode = x.EmployeeCode,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                CompanyProfileId = x.CompanyProfileId,
                CompanyName = x.CompanyProfile != null ? x.CompanyProfile.CompanyName : null,
                CitizenId = x.CitizenId,
                CitizenIdIssuedDate = x.CitizenIdIssuedDate,
                CitizenIdIssuedPlace = x.CitizenIdIssuedPlace,
                DateOfBirth = x.DateOfBirth,
                Address = x.Address,
                AreaCode = x.AreaCode,
                DriverLicenseNumber = x.DriverLicenseNumber,
                DriverLicenseClass = x.DriverLicenseClass,
                DriverLicenseIssuedDate = x.DriverLicenseIssuedDate,
                DriverLicenseExpiryDate = x.DriverLicenseExpiryDate,

                DriverSignatureFileUrl = x.DriverSignatureFileUrl,
                DriverSignedAt = x.DriverSignedAt,
                DriverSignatureIsActive = x.DriverSignatureIsActive,
                DriverSignatureInactiveAt = x.DriverSignatureInactiveAt,

                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DriverAccountDto>> GetListAsync(DriverAccountFilter filter, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Lấy danh sách tài xế (Driver) từ bảng Users, kết hợp với bảng UserRoles và Roles để lọc theo vai trò "Driver".
        var query = from user in db.Users.AsNoTracking()
                    join userRole in db.UserRoles on user.Id equals userRole.UserId
                    join role in db.Roles on userRole.RoleId equals role.Id
                    where role.Name == "Driver" && !user.IsDeleted && user.CompanyProfile != null && !user.CompanyProfile.IsDeleted
                    select user;

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            // Tìm kiếm theo họ tên, tên đăng nhập, mã nhân viên, số điện thoại hoặc số CMND/CCCD.
            query = query.Where(x =>
                x.FullName.Contains(keyword) ||
                (x.UserName ?? string.Empty).Contains(keyword) ||
                (x.EmployeeCode ?? string.Empty).Contains(keyword) ||
                (x.PhoneNumber ?? string.Empty).Contains(keyword) ||
                (x.CitizenId ?? string.Empty).Contains(keyword));
        }
        if (filter.IsActive.HasValue) query = query.Where(x => x.IsActive == filter.IsActive.Value);
        if (filter.CompanyProfileId.HasValue) query = query.Where(x => x.CompanyProfileId == filter.CompanyProfileId.Value);

        IQueryable<ApplicationUser> pagedQuery = query.OrderBy(x => x.FullName);
        if (filter.PageSize > 0)
        {
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 500);
            pagedQuery = pagedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }

        return await pagedQuery
            .Select(x => new DriverAccountDto
            {
                Id = x.Id,
                UserName = x.UserName ?? string.Empty,
                FullName = x.FullName,
                EmployeeCode = x.EmployeeCode,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                CompanyName = x.CompanyProfile != null ? x.CompanyProfile.CompanyName : null,
                CitizenId = x.CitizenId,
                DriverLicenseNumber = x.DriverLicenseNumber,
                DriverLicenseClass = x.DriverLicenseClass,

                DriverSignatureFileUrl = x.DriverSignatureFileUrl,
                DriverSignatureIsActive = x.DriverSignatureIsActive,
                DriverSignatureInactiveAt = x.DriverSignatureInactiveAt,

                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToListAsync(ct);
    }

    public Task SetActiveAsync(string id, bool active, CancellationToken ct = default)
        => ChangeOperationalStateAsync(
            id,
            active,
            markDeleted: false,
            changeSource: active ? "ACCOUNT_UNLOCKED" : "ACCOUNT_TEMPORARILY_LOCKED",
            ct: ct);

    public async Task ResetPasswordAsync(string id, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");
        EnsureNotDeleted(user, "Không tìm thấy tài xế.");
        await EnsureDriverRoleAsync(user);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        Ensure(await userManager.ResetPasswordAsync(user, token, password));
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;
        Ensure(await userManager.UpdateAsync(user));
    }

    public async Task RequirePasswordChangeAsync(string id, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");
        EnsureNotDeleted(user, "Không tìm thấy tài xế.");
        await EnsureDriverRoleAsync(user);
        user.MustChangePassword = true;
        Ensure(await userManager.UpdateAsync(user));
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => ChangeOperationalStateAsync(
            id,
            active: false,
            markDeleted: true,
            changeSource: "DRIVER_LEFT_EMPLOYMENT",
            ct: ct);

    /// <summary>
    /// Khóa/mở khóa/nghỉ việc là một thao tác nguyên tử ở tầng database.
    /// Khi tài xế không còn hoạt động, mọi xe đang gán được trả về trạng thái chưa gán.
    /// Hợp đồng, chữ ký, hồ sơ và lịch sử vẫn được giữ nguyên; không xóa vật lý.
    /// </summary>
    private async Task ChangeOperationalStateAsync(
        string id,
        bool active,
        bool markDeleted,
        string changeSource,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tài xế.");

        if (user.IsDeleted)
            throw new KeyNotFoundException("Không tìm thấy tài xế.");

        var isDriver = await (from userRole in db.UserRoles.AsNoTracking()
                              join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                              where userRole.UserId == id && role.Name == "Driver"
                              select userRole.UserId)
            .AnyAsync(ct);

        if (!isDriver)
            throw new KeyNotFoundException("Không tìm thấy tài xế.");

        if (active)
        {
            if (!string.Equals(user.RegistrationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chỉ tài khoản tài xế đã được duyệt mới được mở khóa.");

            if (!user.CompanyProfileId.HasValue ||
                !await db.CompanyProfiles.AsNoTracking().AnyAsync(
                    x => x.Id == user.CompanyProfileId.Value && x.IsActive && !x.IsDeleted,
                    ct))
            {
                throw new InvalidOperationException("Không thể mở khóa vì tài xế chưa thuộc Công ty/Văn phòng đang hoạt động.");
            }
        }

        var now = DateTime.UtcNow;
        user.IsActive = active && !markDeleted;
        user.UpdatedAt = now;

        if (user.IsActive)
        {
            // "Mở khóa" phải đưa tài khoản về trạng thái có thể đăng nhập thực sự,
            // không bị giữ lại bởi LockoutEnd do các lần nhập sai mật khẩu trước đó.
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }

        // Đổi SecurityStamp để cookie đăng nhập cũ bị vô hiệu hóa. Khi mở khóa,
        // tài xế vẫn phải đăng nhập lại; hệ thống không khôi phục phiên cũ.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        if (markDeleted)
        {
            // "Nghỉ việc" là soft delete: chỉ ẩn khỏi giao diện, không xóa hồ sơ nguồn.
            user.IsDeleted = true;
            user.DeletedAt = now;
            user.DeletedBy = changeSource;
        }

        if (!user.IsActive)
        {
            // Giải phóng xe trước khi cập nhật trạng thái tài xế. ExecuteUpdate không
            // phụ thuộc RowVersion của Vehicle và vẫn nằm trong cùng transaction.
            await db.Vehicles
                .Where(x => x.AssignedDriverId == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.AssignedDriverId, (string?)null)
                    .SetProperty(x => x.UpdatedAt, now)
                    .SetProperty(x => x.UpdatedBy, changeSource), ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ReleaseAssignedVehiclesAsync(
        string driverId,
        string changeSource,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        await db.Vehicles
            .Where(x => x.AssignedDriverId == driverId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.AssignedDriverId, (string?)null)
                .SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.UpdatedBy, changeSource), ct);
    }


    private async Task EnsureLoginIdentifiersAvailableAsync(
        string userName,
        string phoneNumber,
        string? excludedUserId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var normalizedPhoneAsUserName = userManager.NormalizeName(phoneNumber);
        var phoneFromUserName = VietnamPhoneNumber.TryNormalize(userName, out var normalizedUserNamePhone)
            ? normalizedUserNamePhone
            : null;

        var users = await db.Users.AsNoTracking()
            .Where(x => x.Id != excludedUserId &&
                (x.PhoneNumber != null || x.NormalizedUserName == normalizedPhoneAsUserName))
            .Select(x => new { x.PhoneNumber, x.NormalizedUserName })
            .ToListAsync(ct);

        var hasConflict = users.Any(x =>
            x.NormalizedUserName == normalizedPhoneAsUserName ||
            (VietnamPhoneNumber.TryNormalize(x.PhoneNumber, out var storedPhone) &&
             (storedPhone == phoneNumber ||
              (phoneFromUserName != null && storedPhone == phoneFromUserName))));

        if (hasConflict)
            throw new InvalidOperationException("Số điện thoại hoặc tên đăng nhập đang được sử dụng bởi tài khoản khác.");
    }

    private static void EnsureNotDeleted(ApplicationUser user, string message)
    {
        if (user.IsDeleted)
            throw new KeyNotFoundException(message);
    }

    private async Task EnsureDriverRoleAsync(ApplicationUser user)
    {
        if (!await userManager.IsInRoleAsync(user, "Driver"))
            throw new KeyNotFoundException("Không tìm thấy tài xế.");
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.CompanyProfiles.AnyAsync(x => x.Id == companyId && x.IsActive && !x.IsDeleted, ct))
            throw new InvalidOperationException("Công ty/văn phòng đại diện không tồn tại hoặc đã ngừng hoạt động.");
    }

    private static IQueryable<ApplicationUser> PendingRegistrationQuery(ApplicationDbContext db) =>
        db.Users.AsNoTracking().Where(x => !x.IsDeleted && x.RegistrationStatus == "Pending" && x.CompanyProfile != null && !x.CompanyProfile.IsDeleted);

    private static readonly Expression<Func<ApplicationUser, DriverRegistrationRequestDto>> RegistrationProjection = x => new DriverRegistrationRequestDto
    {
        UserId = x.Id,
        UserName = x.UserName ?? string.Empty,
        FullName = x.FullName,
        PhoneNumber = x.PhoneNumber,
        CompanyProfileId = x.CompanyProfileId,
        CompanyName = x.CompanyProfile != null
            ? (string.IsNullOrWhiteSpace(x.CompanyProfile.BranchName) ? x.CompanyProfile.CompanyName : x.CompanyProfile.CompanyName + " - " + x.CompanyProfile.BranchName)
            : null,
        DateOfBirth = x.DateOfBirth,
        AreaCode = x.AreaCode,
        Address = x.Address,
        CitizenId = x.CitizenId,
        CitizenIdIssuedDate = x.CitizenIdIssuedDate,
        CitizenIdIssuedPlace = x.CitizenIdIssuedPlace,
        DriverLicenseNumber = x.DriverLicenseNumber,
        DriverLicenseClass = x.DriverLicenseClass,
        DriverLicenseIssuedDate = x.DriverLicenseIssuedDate,
        DriverLicenseExpiryDate = x.DriverLicenseExpiryDate,
        DriverSignatureFileUrl = x.DriverSignatureFileUrl,
        RequestedAt = x.RegistrationRequestedAt ?? x.CreatedAt,
        ViewedAt = x.RegistrationViewedAt
    };

    private async Task<(string Url, string Hash, DateTime SavedAt)> SaveRegistrationSignatureAsync(string userId, string dataUrl, CancellationToken ct)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) throw new InvalidOperationException("Dữ liệu chữ ký không hợp lệ.");
        var header = dataUrl[..comma];
        var extension = header.Contains("image/jpeg", StringComparison.OrdinalIgnoreCase) || header.Contains("image/jpg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { throw new InvalidOperationException("Dữ liệu chữ ký không đúng định dạng Base64."); }
        if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024)
            throw new InvalidOperationException("Dung lượng chữ ký không hợp lệ hoặc vượt quá 2 MB.");

        var root = fileStorageOptions.Value.UploadRootPath;
        if (!Path.IsPathRooted(root)) root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, root));
        var folder = Path.Combine(root, "master-signatures", "drivers", userId);
        Directory.CreateDirectory(folder);
        var fileName = $"driver-{Guid.NewGuid():N}.{extension}";
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes, ct);
        var requestPath = "/" + (fileStorageOptions.Value.PublicRequestPath ?? "/uploads").Trim('/');
        return ($"{requestPath}/master-signatures/drivers/{userId}/{fileName}", Convert.ToHexString(SHA256.HashData(bytes)), DateTime.UtcNow);
    }

    private static void ValidateCompany(Guid companyId)
    {
        if (companyId == Guid.Empty) throw new InvalidOperationException("Tài xế bắt buộc phải được gán công ty/văn phòng đại diện.");
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
