using HTX586CONTRACT.Domain.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HTX586CONTRACT.Infrastructure.Persistence.Configurations;

public sealed class ContractPassengerConfiguration : IEntityTypeConfiguration<ContractPassenger>
{
    public void Configure(EntityTypeBuilder<ContractPassenger> b)
    {
        b.ToTable("ContractPassengers");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Contract).WithMany(x => x.Passengers).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ContractId, x.SortOrder }).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
