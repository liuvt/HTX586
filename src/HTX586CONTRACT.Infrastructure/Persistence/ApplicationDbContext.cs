using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Notifications;
using HTX586CONTRACT.Domain.Signatures;
using HTX586CONTRACT.Domain.Vehicles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<ContractType> ContractTypes => Set<ContractType>();
    public DbSet<ContractTemplate> ContractTemplates => Set<ContractTemplate>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractPassenger> ContractPassengers => Set<ContractPassenger>();
    public DbSet<ContractSignature> ContractSignatures => Set<ContractSignature>();
    public DbSet<ContractAttachment> ContractAttachments => Set<ContractAttachment>();
    public DbSet<ContractAuditLog> ContractAuditLogs => Set<ContractAuditLog>();
    public DbSet<DriverNotification> DriverNotifications => Set<DriverNotification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // AspNetUserRoles có trigger TR_AspNetUserRoles_ReleaseAssignedVehicle.
        // RemoveFromRoleAsync có thể phát sinh DELETE dùng OUTPUT; SQL Server không cho
        // dùng OUTPUT trực tiếp trên bảng có trigger nên phải tắt riêng cho bảng này.
        builder.Entity<IdentityUserRole<string>>()
            .ToTable("AspNetUserRoles", table => table.UseSqlOutputClause(false));

        // Tất cả schema tùy chỉnh được khai báo bằng Fluent API trong thư mục Configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertPhysicalDeletesToSoftDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ConvertPhysicalDeletesToSoftDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Lớp bảo vệ cuối cùng của ứng dụng: mọi DbSet.Remove/RemoveRange hoặc
    /// thao tác xóa đối với entity hỗ trợ soft delete đều được đổi thành
    /// UPDATE IsDeleted. Không có dữ liệu nghiệp vụ nào bị DELETE vật lý.
    /// </summary>
    private void ConvertPhysicalDeletesToSoftDeletes()
    {
        var deletedEntries = ChangeTracker.Entries()
            .Where(x => x.State == EntityState.Deleted && x.Entity is ISoftDeletable)
            .ToList();

        if (deletedEntries.Count == 0)
            return;

        // Nếu một ApplicationUser bị gọi Remove/DeleteAsync, EF Identity có thể
        // đánh dấu cascade các bảng role/claim/login/token là Deleted trước khi
        // SaveChanges. Phục hồi các quan hệ này để việc ẩn tài khoản không làm
        // mất quyền và dữ liệu định danh gốc trong database.
        var deletedUserIds = deletedEntries
            .Where(x => x.Entity is ApplicationUser)
            .Select(x => ((ApplicationUser)x.Entity).Id)
            .ToHashSet(StringComparer.Ordinal);

        if (deletedUserIds.Count > 0)
        {
            foreach (var dependent in ChangeTracker.Entries()
                         .Where(x => x.State == EntityState.Deleted && x.Entity is not ISoftDeletable))
            {
                var userIdProperty = dependent.Metadata.FindProperty("UserId");
                if (userIdProperty is null)
                    continue;

                var userId = dependent.Property("UserId").CurrentValue as string;
                if (userId is not null && deletedUserIds.Contains(userId))
                    dependent.State = EntityState.Unchanged;
            }
        }

        var deletedAt = DateTime.UtcNow;

        foreach (var entry in deletedEntries)
        {
            // Chuyển về Unchanged trước để không vô tình ghi đè toàn bộ cột bằng
            // dữ liệu cũ của một entity vừa bị gọi Remove().
            entry.State = EntityState.Unchanged;
            var entity = (ISoftDeletable)entry.Entity;
            entity.IsDeleted = true;
            entity.DeletedAt ??= deletedAt;
            entity.DeletedBy ??= "APPLICATION_SOFT_DELETE";

            entry.Property(nameof(ISoftDeletable.IsDeleted)).IsModified = true;
            entry.Property(nameof(ISoftDeletable.DeletedAt)).IsModified = true;
            entry.Property(nameof(ISoftDeletable.DeletedBy)).IsModified = true;

            switch (entry.Entity)
            {
                case ApplicationUser user:
                    user.IsActive = false;
                    user.SecurityStamp = Guid.NewGuid().ToString("N");
                    user.UpdatedAt = deletedAt;
                    Entry(user).Property(x => x.IsActive).IsModified = true;
                    Entry(user).Property(x => x.SecurityStamp).IsModified = true;
                    Entry(user).Property(x => x.UpdatedAt).IsModified = true;
                    break;

                case CompanyProfile company:
                    company.IsActive = false;
                    company.UpdatedAt = deletedAt;
                    Entry(company).Property(x => x.IsActive).IsModified = true;
                    Entry(company).Property(x => x.UpdatedAt).IsModified = true;
                    break;
            }
        }
    }
}
