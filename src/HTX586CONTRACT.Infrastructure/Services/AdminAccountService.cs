using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Application.Admins.CompanyProfiles;
using HTX586CONTRACT.Application.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Services;

public sealed class AdminAccountService(
    IDbContextFactory<ApplicationDbContext> factory,
    UserManager<ApplicationUser> userManager) : IAdminAccountService
{
    public async Task<IReadOnlyList<AdminAccountListItem>> GetAccountsAsync(string? keyword = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = from user in db.Users.AsNoTracking()
                    where db.UserRoles.Any(ur => ur.UserId == user.Id &&
                        db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "Owner" || r.Name == "Admin")))
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
        if (string.IsNullOrWhiteSpace(request.UserName)) throw new InvalidOperationException("Vui lòng nhập tên đăng nhập Admin.");
        if (string.IsNullOrWhiteSpace(request.Password)) throw new InvalidOperationException("Vui lòng nhập mật khẩu Admin.");
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new InvalidOperationException("Vui lòng nhập họ tên Admin.");
        ValidateCompany(request.CompanyProfile);

        await using var db = await factory.CreateDbContextAsync(ct);
        var taxCode = request.CompanyProfile.TaxCode.Trim();
        if (await db.CompanyProfiles.AnyAsync(x => x.TaxCode == taxCode, ct))
            throw new InvalidOperationException("Mã số thuế đã tồn tại.");

        var company = new CompanyProfile
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        MapCompany(company, request.CompanyProfile);
        db.CompanyProfiles.Add(company);
        await db.SaveChangesAsync(ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FullName = request.FullName.Trim(),
            EmployeeCode = N(request.EmployeeCode),
            PhoneNumber = N(request.PhoneNumber),
            Email = N(request.Email),
            CompanyProfileId = company.Id,
            IsActive = true,
            MustChangePassword = request.MustChangePassword,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            db.CompanyProfiles.Remove(company);
            await db.SaveChangesAsync(ct);
            Ensure(createResult);
        }

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            db.CompanyProfiles.Remove(company);
            await db.SaveChangesAsync(ct);
            Ensure(roleResult);
        }

        return new CreateAdminAccountResult
        {
            UserId = user.Id,
            CompanyProfileId = company.Id
        };
    }

    public async Task<AdminAccountDetail?> GetDetailAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
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

        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null) return ServiceResult.Failure("Không tìm thấy tài khoản.");

        user.FullName = request.FullName.Trim();
        user.EmployeeCode = N(request.EmployeeCode);
        user.PhoneNumber = N(request.PhoneNumber);
        user.Email = N(request.Email);
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ServiceResult.Success("Cập nhật tài khoản thành công.")
            : ServiceResult.Failure(result.Errors.Select(x => x.Description));
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

    private static void ValidateCompany(CreateCompanyProfileRequest company)
    {
        if (string.IsNullOrWhiteSpace(company.CompanyName)) throw new InvalidOperationException("Vui lòng nhập tên công ty/văn phòng đại diện.");
        if (string.IsNullOrWhiteSpace(company.TaxCode)) throw new InvalidOperationException("Vui lòng nhập mã số thuế.");
        if (string.IsNullOrWhiteSpace(company.Address)) throw new InvalidOperationException("Vui lòng nhập địa chỉ công ty/văn phòng đại diện.");
        if (string.IsNullOrWhiteSpace(company.RepresentativeName)) throw new InvalidOperationException("Vui lòng nhập người đại diện.");
        if (string.IsNullOrWhiteSpace(company.RepresentativeCitizenId)) throw new InvalidOperationException("Vui lòng nhập CCCD người đại diện.");
    }

    private static void MapCompany(CompanyProfile e, CreateCompanyProfileRequest r)
    {
        e.CompanyName = r.CompanyName.Trim();
        e.BranchName = N(r.BranchName);
        e.TaxCode = r.TaxCode.Trim();
        e.BusinessLicenseNumber = N(r.BusinessLicenseNumber);
        e.Address = r.Address.Trim();
        e.PhoneNumber = N(r.PhoneNumber);
        e.Email = N(r.Email);
        e.RepresentativeName = r.RepresentativeName.Trim();
        e.RepresentativePosition = N(r.RepresentativePosition);
        e.RepresentativeCitizenId = r.RepresentativeCitizenId.Trim();
        e.RepresentativeCitizenIdIssuedDate = r.RepresentativeCitizenIdIssuedDate;
        e.RepresentativeCitizenIdIssuedPlace = N(r.RepresentativeCitizenIdIssuedPlace);
        e.BankAccountNumber = N(r.BankAccountNumber);
        e.BankName = N(r.BankName);
        e.IsActive = r.IsActive;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
