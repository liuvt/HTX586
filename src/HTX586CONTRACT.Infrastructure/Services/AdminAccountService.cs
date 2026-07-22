using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Application.Common;
using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class AdminAccountService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IAdminAccountService
{
    private const string DefaultResetPassword = "htx@586";

    public async Task<IReadOnlyList<AdminAccountListItem>> GetAccountsAsync(string? keyword = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        // Màn /admin/accounts chỉ quản lý tài khoản Admin.
        // Owner được tách khỏi luồng Admin để không bị lộ/chỉnh sửa như một Admin thường.
        var query = from user in db.Users.AsNoTracking()
                    where !user.IsDeleted && user.CompanyProfile != null && !user.CompanyProfile.IsDeleted && db.UserRoles.Any(ur => ur.UserId == user.Id &&
                              db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"))
                          && !db.UserRoles.Any(ur => ur.UserId == user.Id &&
                              db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Owner"))
                    select user;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(value)) ||
                x.FullName.Contains(value) ||
                (x.EmployeeCode != null && x.EmployeeCode.Contains(value)) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(value)) ||
                (x.Email != null && x.Email.Contains(value)) ||
                (x.CompanyProfile != null && x.CompanyProfile.CompanyName.Contains(value)));
        }

        var rows = await query.OrderBy(x => x.FullName)
            .Select(x => new
            {
                x.Id,
                UserName = x.UserName ?? string.Empty,
                x.FullName,
                x.EmployeeCode,
                x.PhoneNumber,
                x.Email,
                CompanyName = x.CompanyProfile != null ? x.CompanyProfile.CompanyName : null,
                x.IsActive,
                x.MustChangePassword
            })
            .ToListAsync(ct);

        var roles = await LoadRoleMapAsync(db, rows.Select(x => x.Id).ToArray(), ct);
        return rows.Select(x => new AdminAccountListItem
            {
                Id = x.Id,
                UserName = x.UserName,
                FullName = x.FullName,
                EmployeeCode = x.EmployeeCode,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                CompanyName = x.CompanyName,
                Roles = roles.TryGetValue(x.Id, out var roleText) ? roleText : string.Empty,
                IsActive = x.IsActive,
                MustChangePassword = x.MustChangePassword
            })
            .ToList();
    }

    public async Task<CreateAdminAccountResult> CreateAdminAsync(CreateAdminAccountRequest request, CancellationToken ct = default)
    {
        ValidateCompany(request.CompanyProfileId);
        if (string.IsNullOrWhiteSpace(request.UserName)) throw new InvalidOperationException("Vui lòng nhập tên đăng nhập Admin.");
        if (string.IsNullOrWhiteSpace(request.Password)) throw new InvalidOperationException("Vui lòng nhập mật khẩu Admin.");
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new InvalidOperationException("Vui lòng nhập họ tên Admin.");
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
            IsActive = true,
            MustChangePassword = request.MustChangePassword,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        Ensure(createResult);

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
        {
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.DeletedBy = "ADMIN_CREATE_ROLLBACK";
            user.IsActive = false;
            Ensure(await userManager.UpdateAsync(user));
            Ensure(roleResult);
        }

        return new CreateAdminAccountResult
        {
            UserId = user.Id,
            CompanyProfileId = request.CompanyProfileId
        };
    }

    public async Task<AdminAccountDetail?> GetDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId && !x.IsDeleted && x.CompanyProfile != null && !x.CompanyProfile.IsDeleted
                && db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"))
                && !db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Owner")))
            .Select(x => new
            {
                x.Id,
                UserName = x.UserName ?? string.Empty,
                x.FullName,
                x.EmployeeCode,
                x.PhoneNumber,
                x.Email,
                x.CompanyProfileId,
                CompanyName = x.CompanyProfile != null ? x.CompanyProfile.CompanyName : null,
                x.IsActive,
                x.MustChangePassword,
                x.CreatedAt,
                x.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;
        var roles = await LoadRoleMapAsync(db, [row.Id], ct);

        return new AdminAccountDetail
        {
            Id = row.Id,
            UserName = row.UserName,
            FullName = row.FullName,
            EmployeeCode = row.EmployeeCode,
            PhoneNumber = row.PhoneNumber,
            Email = row.Email,
            CompanyProfileId = row.CompanyProfileId,
            CompanyName = row.CompanyName,
            Roles = roles.TryGetValue(row.Id, out var roleText) ? roleText : string.Empty,
            IsActive = row.IsActive,
            MustChangePassword = row.MustChangePassword,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    public async Task<ServiceResult> UpdateAccountAsync(UpdateAdminAccountRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserId)) return ServiceResult.Failure("Thiếu mã tài khoản.");
        if (string.IsNullOrWhiteSpace(request.FullName)) return ServiceResult.Failure("Vui lòng nhập họ và tên.");
        if (!VietnamPhoneNumber.TryNormalize(request.PhoneNumber, out var phoneNumber))
            return ServiceResult.Failure(VietnamPhoneNumber.ValidationMessage);
        if (request.CompanyProfileId.HasValue && request.CompanyProfileId.Value == Guid.Empty)
            return ServiceResult.Failure("Vui lòng chọn CompanyProfile hợp lệ.");
        if (request.CompanyProfileId.HasValue)
            await EnsureCompanyAsync(request.CompanyProfileId.Value, ct);

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || user.IsDeleted) return ServiceResult.Failure("Không tìm thấy tài khoản.");
        if (!await IsAdminOnlyAsync(user))
            return ServiceResult.Failure("Màn này chỉ được cập nhật tài khoản Admin. Owner được quản lý ở luồng riêng.");

        try
        {
            await EnsureLoginIdentifiersAvailableAsync(user.UserName ?? string.Empty, phoneNumber, user.Id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return ServiceResult.Failure(ex.Message);
        }

        user.CompanyProfileId = request.CompanyProfileId;
        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = phoneNumber;
        user.Email = N(request.Email);
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ServiceResult.Success("Cập nhật tài khoản thành công.")
            : ServiceResult.Failure(result.Errors.Select(x => x.Description));
    }

    public async Task<ServiceResult> ResetPasswordToDefaultAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Failure("Thiếu mã tài khoản.");

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.IsDeleted)
            return ServiceResult.Failure("Không tìm thấy tài khoản.");

        if (!await IsAdminOnlyAsync(user))
            return ServiceResult.Failure("Màn này chỉ reset mật khẩu cho tài khoản Admin. Owner và Driver có luồng quản lý riêng.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await userManager.ResetPasswordAsync(user, token, DefaultResetPassword);
        if (!resetResult.Succeeded)
            return ServiceResult.Failure(resetResult.Errors.Select(x => x.Description));

        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        return updateResult.Succeeded
            ? ServiceResult.Success($"Đã reset mật khẩu về mặc định: {DefaultResetPassword}. Tài khoản sẽ bắt buộc đổi mật khẩu khi đăng nhập.")
            : ServiceResult.Failure(updateResult.Errors.Select(x => x.Description));
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

    private async Task<bool> IsAdminOnlyAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            && !roles.Any(role => string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureCompanyAsync(Guid companyId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.CompanyProfiles.AnyAsync(x => x.Id == companyId && x.IsActive && !x.IsDeleted, ct))
            throw new InvalidOperationException("CompanyProfile không tồn tại hoặc đã ngừng hoạt động.");
    }

    private static async Task<Dictionary<string, string>> LoadRoleMapAsync(ApplicationDbContext db, string[] userIds, CancellationToken ct)
    {
        if (userIds.Length == 0) return new Dictionary<string, string>();

        var rows = await (from userRole in db.UserRoles.AsNoTracking()
                          join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                          where userIds.Contains(userRole.UserId)
                          select new { userRole.UserId, role.Name })
            .ToListAsync(ct);

        return rows.GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => string.Join(", ", x.Select(r => r.Name).Where(r => !string.IsNullOrWhiteSpace(r)).OrderBy(r => r)));
    }

    private static void ValidateCompany(Guid companyId)
    {
        if (companyId == Guid.Empty) throw new InvalidOperationException("Admin bắt buộc phải được gán CompanyProfile.");
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
