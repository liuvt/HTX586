using HTX586CONTRACT.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContractNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AreaCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ContractValue).HasPrecision(18, 2);
        builder.Property(x => x.TotalKilometers).HasPrecision(18, 2);
        builder.Property(x => x.CargoWeight).HasPrecision(18, 2);
        builder.Property(x => x.ContractContentSnapshot).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.ContractDataJson).HasColumnType("nvarchar(max)").IsRequired();

        builder.Property(x => x.CompanyNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CompanyTaxCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CompanyAddressSnapshot).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CompanyRepresentativeSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CompanyRepresentativePositionSnapshot).HasMaxLength(100);
        builder.Property(x => x.DriverNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DriverLicenseNumberSnapshot).HasMaxLength(50);
        builder.Property(x => x.DriverLicenseClassSnapshot).HasMaxLength(20);
        builder.Property(x => x.CustomerNameSnapshot).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CustomerPhoneSnapshot).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CustomerCitizenIdSnapshot).HasMaxLength(30);
        builder.Property(x => x.CustomerAddressSnapshot).HasMaxLength(500);
        builder.Property(x => x.VehiclePlateSnapshot).HasMaxLength(20);
        builder.Property(x => x.VehicleBrandSnapshot).HasMaxLength(100);
        builder.Property(x => x.VehicleOwnerNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.VehicleOwnerCitizenIdSnapshot).HasMaxLength(30);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.CompanyProfile)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vehicle)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ContractType)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.ContractTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ContractTemplate)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.ContractTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Driver)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ContractNumber).IsUnique();
        builder.HasIndex(x => new { x.DriverId, x.CreatedAt });
        builder.HasIndex(x => new { x.CompanyProfileId, x.CreatedAt });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
