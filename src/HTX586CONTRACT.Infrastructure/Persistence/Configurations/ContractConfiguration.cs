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

        builder.Property(x => x.ContractNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AreaCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CargoName)
            .HasMaxLength(300);

        builder.Property(x => x.CargoWeight)
            .HasPrecision(18, 2);

        builder.Property(x => x.CargoUnit)
            .HasMaxLength(50);

        builder.Property(x => x.SecondDriverName)
            .HasMaxLength(200);

        builder.Property(x => x.SecondDriverLicenseClass)
            .HasMaxLength(20);

        builder.Property(x => x.RouteDescription)
            .HasMaxLength(2000);

        builder.Property(x => x.TotalKilometers)
            .HasPrecision(18, 2);

        builder.Property(x => x.PickupLocation)
            .HasMaxLength(1000);

        builder.Property(x => x.DropoffLocation)
            .HasMaxLength(1000);

        builder.Property(x => x.ContractValue)
            .HasPrecision(18, 2);

        builder.Property(x => x.PaymentMethod)
            .HasMaxLength(100);

        builder.Property(x => x.PaymentTime)
            .HasMaxLength(200);

        builder.Property(x => x.Note)
            .HasMaxLength(2000);

        builder.Property(x => x.CompanyNameSnapshot)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.CompanyTaxCodeSnapshot)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CompanyAddressSnapshot)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.CompanyRepresentativeSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CompanyRepresentativePositionSnapshot)
            .HasMaxLength(100);

        builder.Property(x => x.DriverNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DriverLicenseNumberSnapshot)
            .HasMaxLength(50);

        builder.Property(x => x.DriverLicenseClassSnapshot)
            .HasMaxLength(20);

        builder.Property(x => x.CustomerNameSnapshot)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.CustomerPhoneSnapshot)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CustomerCitizenIdSnapshot)
            .HasMaxLength(30);

        builder.Property(x => x.CustomerAddressSnapshot)
            .HasMaxLength(500);

        builder.Property(x => x.VehiclePlateSnapshot)
            .HasMaxLength(20);

        builder.Property(x => x.VehicleBrandSnapshot)
            .HasMaxLength(100);

        builder.Property(x => x.VehicleOwnerNameSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.VehicleOwnerCitizenIdSnapshot)
            .HasMaxLength(30);

        builder.Property(x => x.ContractContentSnapshot)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.ContractDataJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        // Dữ liệu PDF được định nghĩa trực tiếp bằng Fluent API, không dùng SQL nâng cấp rời.
        builder.Property(x => x.ContractHash)
            .HasMaxLength(128);

        builder.Property(x => x.PdfFileUrl)
            .HasMaxLength(500);

        builder.Property(x => x.PdfSha256)
            .HasMaxLength(128);

        builder.Property(x => x.PdfGeneratedAt)
            .HasColumnType("datetime2");

        builder.Property(x => x.CompletedAt)
            .HasColumnType("datetime2");

        builder.Property(x => x.CancelledAt)
            .HasColumnType("datetime2");

        builder.Property(x => x.CancelReason)
            .HasMaxLength(1000);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

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

        builder.HasMany(x => x.Passengers)
            .WithOne(x => x.Contract)
            .HasForeignKey(x => x.ContractId)
            // Không cascade delete: hành khách là dữ liệu lịch sử của hợp đồng.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Signatures)
            .WithOne(x => x.Contract)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.Contract)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.AuditLogs)
            .WithOne(x => x.Contract)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ContractNumber)
            .IsUnique()
            .HasDatabaseName("UX_Contracts_ContractNumber");

        builder.HasIndex(x => new { x.DriverId, x.CreatedAt })
            .HasDatabaseName("IX_Contracts_Driver_CreatedAt");

        builder.HasIndex(x => new { x.CompanyProfileId, x.CreatedAt })
            .HasDatabaseName("IX_Contracts_CompanyProfile_CreatedAt");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Contracts_Status");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
