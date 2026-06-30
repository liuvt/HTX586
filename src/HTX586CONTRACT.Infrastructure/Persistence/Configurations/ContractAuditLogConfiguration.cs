using HTX586CONTRACT.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractAuditLogConfiguration : IEntityTypeConfiguration<ContractAuditLog>
{
    public void Configure(EntityTypeBuilder<ContractAuditLog> builder)
    {
        builder.ToTable("ContractAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.UserName).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.DeviceId).HasMaxLength(200);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        // Contract có query filter IsDeleted, nên AuditLog cũng có filter tương ứng
        // để tránh warning EF Core về required navigation bị filter mất Contract.
        builder.HasQueryFilter(x => !x.Contract.IsDeleted);

        builder.HasIndex(x => new { x.ContractId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ContractAuditLogs_Contract_CreatedAt");

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ContractAuditLogs_User_CreatedAt");
    }
}
