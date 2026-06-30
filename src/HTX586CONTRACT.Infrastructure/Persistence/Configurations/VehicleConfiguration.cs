using HTX586CONTRACT.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.VehicleCode).HasMaxLength(50);
        builder.Property(x => x.Brand).HasMaxLength(100);
        builder.Property(x => x.Model).HasMaxLength(100);
        builder.Property(x => x.VehicleType).HasMaxLength(100);
        builder.Property(x => x.Color).HasMaxLength(50);
        builder.Property(x => x.ChassisNumber).HasMaxLength(100);
        builder.Property(x => x.EngineNumber).HasMaxLength(100);
        builder.Property(x => x.OwnerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OwnerCitizenId).HasMaxLength(30);
        builder.Property(x => x.OwnerCitizenIdIssuedDate).HasColumnType("date");
        builder.Property(x => x.OwnerCitizenIdIssuedPlace).HasMaxLength(300);
        builder.Property(x => x.OwnerAddress).HasMaxLength(500);
        builder.Property(x => x.OwnerPhoneNumber).HasMaxLength(20);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.PlateNumber).IsUnique();
        builder.HasIndex(x => x.IsActive);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
