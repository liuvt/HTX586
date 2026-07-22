using HTX586CONTRACT.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // AspNetUsers có trigger TR_AspNetUsers_ReleaseAssignedVehicle.
        // SQL Server không cho phép EF Core dùng OUTPUT trực tiếp trên bảng có trigger,
        // vì vậy phải chuyển SaveChanges sang cơ chế tương thích trigger.
        builder.ToTable("AspNetUsers", table => table.UseSqlOutputClause(false));

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.RegistrationStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RegistrationViewedByUserId).HasMaxLength(450);
        builder.Property(x => x.RegistrationReviewedByUserId).HasMaxLength(450);
        builder.Property(x => x.RegistrationReviewNote).HasMaxLength(1000);

        builder.Property(x => x.EmployeeCode)
            .HasMaxLength(30);

        builder.Property(x => x.CitizenId)
            .HasMaxLength(30);

        builder.Property(x => x.CitizenIdIssuedDate)
            .HasColumnType("date");

        builder.Property(x => x.CitizenIdIssuedPlace)
            .HasMaxLength(300);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.Property(x => x.AreaCode)
            .HasMaxLength(20);

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CitizenIdFrontUrl)
            .HasMaxLength(500);

        builder.Property(x => x.CitizenIdBackUrl)
            .HasMaxLength(500);

        builder.Property(x => x.DriverLicenseNumber)
            .HasMaxLength(50);

        builder.Property(x => x.DriverLicenseClass)
            .HasMaxLength(20);

        builder.Property(x => x.DriverLicenseIssuedDate)
            .HasColumnType("date");

        builder.Property(x => x.DriverLicenseExpiryDate)
            .HasColumnType("date");

        builder.Property(x => x.DriverLicenseFrontUrl)
            .HasMaxLength(500);

        builder.Property(x => x.DriverLicenseBackUrl)
            .HasMaxLength(500);

        // Chữ ký cố định của tài xế, dùng khi xuất hợp đồng.
        builder.Property(x => x.DriverSignatureFileUrl)
            .HasMaxLength(500);

        builder.Property(x => x.DriverSignatureHash)
            .HasMaxLength(128);

        builder.Property(x => x.DriverSignedAt)
            .HasColumnType("datetime2");

        // Owner có thể không gán CompanyProfile; Admin/Driver được Owner gán CompanyProfile.
        builder.HasOne(x => x.CompanyProfile)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.CompanyProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CompanyProfileId)
            .HasDatabaseName("IX_AspNetUsers_CompanyProfileId");

        builder.HasIndex(x => x.EmployeeCode)
            .IsUnique()
            .HasFilter("[EmployeeCode] IS NOT NULL")
            .HasDatabaseName("UX_AspNetUsers_EmployeeCode");

        builder.HasIndex(x => x.CitizenId)
            .HasFilter("[CitizenId] IS NOT NULL")
            .HasDatabaseName("IX_AspNetUsers_CitizenId");

        builder.Property(x => x.DeletedBy)
            .HasMaxLength(450);

        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_AspNetUsers_IsActive");

        builder.HasIndex(x => x.IsDeleted)
            .HasDatabaseName("IX_AspNetUsers_IsDeleted");
    }
}
