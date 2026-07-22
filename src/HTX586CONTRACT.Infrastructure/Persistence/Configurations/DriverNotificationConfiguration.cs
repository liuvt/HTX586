using HTX586CONTRACT.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class DriverNotificationConfiguration : IEntityTypeConfiguration<DriverNotification>
{
    public void Configure(EntityTypeBuilder<DriverNotification> builder)
    {
        builder.ToTable("DriverNotifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DriverId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.LinkUrl).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.ReadAt).HasColumnType("datetime2");
        builder.Property(x => x.DeletedAt).HasColumnType("datetime2");
        builder.Property(x => x.DeletedBy).HasMaxLength(450);

        builder.HasOne(x => x.Driver)
            .WithMany(x => x.DriverNotifications)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.IsDeleted)
            .HasDatabaseName("IX_DriverNotifications_IsDeleted");

        builder.HasIndex(x => new { x.DriverId, x.IsRead, x.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_DriverNotifications_Driver_Read_CreatedAt");
    }
}
