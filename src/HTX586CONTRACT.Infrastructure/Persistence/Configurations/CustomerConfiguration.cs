using HTX586CONTRACT.Domain.Customers; using Microsoft.EntityFrameworkCore; using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;
public sealed class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(
        EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CitizenId)
            .HasMaxLength(20);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.CreatedByDriver)
            .WithMany(x => x.CreatedCustomers)
            .HasForeignKey(x => x.CreatedByDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PhoneNumber)
            .HasDatabaseName("IX_Customers_PhoneNumber");

        builder.HasIndex(x => x.CitizenId)
            .HasFilter(
                "[CitizenId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("IX_Customers_CitizenId");

        builder.HasIndex(x => new
        {
            x.CreatedByDriverId,
            x.PhoneNumber
        })
        .HasDatabaseName(
            "IX_Customers_Driver_PhoneNumber");

        builder.HasIndex(x => new
        {
            x.CreatedByDriverId,
            x.LastUsedAt
        })
        .IsDescending(false, true)
        .HasDatabaseName(
            "IX_Customers_Driver_LastUsedAt");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}