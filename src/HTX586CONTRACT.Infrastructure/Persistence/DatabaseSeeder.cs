using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var factory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await using var db = await factory.CreateDbContextAsync();

        // Ver6 dùng current model + SQL nâng cấp idempotent, không gọi MigrateAsync
        // để tránh model snapshot cũ chặn ứng dụng khi khởi động.
        await db.Database.EnsureCreatedAsync();
        await DatabaseSchemaInitializer.ApplyAsync(db);

        await SeedRolesAsync(roleManager);
        await SeedOwnerAsync(userManager, configuration);

        // CompanyProfile không seed mặc định nữa.
        // Owner tạo tài khoản Admin mới thì hệ thống tạo CompanyProfile + chữ ký cố định cho Admin đó.
        await SeedContractTypesAsync(db);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Owner", "Admin", "Driver" })
        {
            if (await roleManager.RoleExistsAsync(role)) continue;
            Ensure(await roleManager.CreateAsync(new IdentityRole(role)), $"Không thể tạo quyền {role}");
        }
    }

    private static async Task SeedOwnerAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        // Nếu database đã có Owner thì không tạo thêm Owner mới, tránh đổi config rồi phát sinh tài khoản rác.
        var existingOwners = await userManager.GetUsersInRoleAsync("Owner");
        if (existingOwners.Count > 0) return;

        var userName = configuration["Seed:OwnerUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = configuration["Seed:AdminUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
            userName = "owner";

        var configuredUser = await userManager.FindByNameAsync(userName);
        if (configuredUser is not null)
        {
            Ensure(await userManager.AddToRoleAsync(configuredUser, "Owner"), "Không thể gán quyền Owner cho tài khoản đã cấu hình");
            return;
        }

        var password = configuration["Seed:OwnerPassword"];
        if (string.IsNullOrWhiteSpace(password))
            password = configuration["Seed:AdminPassword"];

        // Cho môi trường Development tự bootstrap Owner để chạy lần đầu không bị crash.
        // Production/Staging vẫn bắt buộc cấu hình Seed:OwnerPassword để tránh tạo mật khẩu mặc định trên server thật.
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        var useDevelopmentDefaultPassword = string.IsNullOrWhiteSpace(password)
            && string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

        if (useDevelopmentDefaultPassword)
            password = "Owner@123456";

        if (!string.IsNullOrWhiteSpace(password))
        {
            var owner = new ApplicationUser
            {
                UserName = userName,
                FullName = "Owner hệ thống",
                EmployeeCode = "OWNER",
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };

            Ensure(await userManager.CreateAsync(owner, password), "Không thể tạo tài khoản Owner");
            Ensure(await userManager.AddToRoleAsync(owner, "Owner"), "Không thể gán quyền Owner");
            return;
        }

        // Database cũ thường đã có tài khoản Admin nhưng không có Owner.
        // Khi chưa cấu hình Seed:OwnerPassword, tự nâng cấp 1 Admin hiện hữu thành Owner để app không bị crash.
        var legacyAdminUserName = configuration["Seed:AdminUserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(legacyAdminUserName))
            legacyAdminUserName = "admin";

        var legacyAdmin = await userManager.FindByNameAsync(legacyAdminUserName);
        if (legacyAdmin is not null)
        {
            Ensure(await userManager.AddToRoleAsync(legacyAdmin, "Owner"), "Không thể gán quyền Owner cho tài khoản admin cũ");
            return;
        }

        var admins = await userManager.GetUsersInRoleAsync("Admin");
        var fallbackAdmin = admins.FirstOrDefault(x => x.IsActive) ?? admins.FirstOrDefault();
        if (fallbackAdmin is not null)
        {
            Ensure(await userManager.AddToRoleAsync(fallbackAdmin, "Owner"), "Không thể gán quyền Owner cho tài khoản Admin hiện hữu");
            return;
        }

        throw new InvalidOperationException(
            "Database mới chưa có tài khoản Owner và chưa có tài khoản Admin cũ để nâng cấp. " +
            "Hãy cấu hình Seed:OwnerPassword bằng user-secrets hoặc biến môi trường Seed__OwnerPassword rồi chạy lại ứng dụng.");
    }

    private static async Task SeedContractTypesAsync(ApplicationDbContext db)
    {
        var types = new[]
        {
            (Code: "DRIVER", Name: "Hợp đồng tài xế"),
            (Code: "CARGO", Name: "Hợp đồng vận chuyển hàng hóa"),
            (Code: "LONG_DISTANCE", Name: "Hợp đồng vận chuyển đường dài")
        };

        foreach (var item in types)
        {
            var type = await db.ContractTypes.FirstOrDefaultAsync(x => x.Code == item.Code);
            if (type is null)
            {
                type = new ContractType
                {
                    Id = Guid.NewGuid(),
                    Code = item.Code,
                    Name = item.Name,
                    IsActive = true,
                    RequireCustomerSignature = true,
                    RequireDriverSignature = true,
                    RequireLocation = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.ContractTypes.Add(type);
            }
            else
            {
                type.Name = item.Name;
                type.IsActive = true;
            }

            await db.SaveChangesAsync();

            var template = await db.ContractTemplates.FirstOrDefaultAsync(x => x.ContractTypeId == type.Id && x.IsActive);
            if (template is null)
            {
                db.ContractTemplates.Add(new ContractTemplate
                {
                    Id = Guid.NewGuid(),
                    ContractTypeId = type.Id,
                    Name = $"Mẫu {item.Name}",
                    Version = 1,
                    HtmlContent = item.Name,
                    IsActive = true,
                    EffectiveFrom = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }

    private static void Ensure(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{message}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}
