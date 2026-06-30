using HTX586CONTRACT.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        builder.ToTable("CompanyProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.CompanyName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.BranchName).HasMaxLength(300);
        builder.Property(x => x.TaxCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.BusinessLicenseNumber).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.RepresentativeName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RepresentativePosition).HasMaxLength(100);
        builder.Property(x => x.RepresentativeCitizenId).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RepresentativeCitizenIdIssuedDate).HasColumnType("date");
        builder.Property(x => x.RepresentativeCitizenIdIssuedPlace).HasMaxLength(300);
        builder.Property(x => x.BankAccountNumber).HasMaxLength(50);
        builder.Property(x => x.BankName).HasMaxLength(200);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(x => x.TaxCode).IsUnique();
        builder.HasIndex(x => x.CompanyName);
        builder.HasIndex(x => x.IsActive);
    }
}
