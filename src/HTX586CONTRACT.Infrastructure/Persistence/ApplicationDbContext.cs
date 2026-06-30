using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Signatures;
using HTX586CONTRACT.Domain.Vehicles;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace HTX586CONTRACT.Infrastructure.Persistence;
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
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
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x=>x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x=>x.EmployeeCode).HasMaxLength(30);
            e.Property(x=>x.CitizenId).HasMaxLength(30);
            e.Property(x=>x.DriverLicenseNumber).HasMaxLength(50);
            e.Property(x=>x.DriverSignatureFileUrl).HasMaxLength(500);
            e.Property(x=>x.DriverSignatureHash).HasMaxLength(128);
            e.HasIndex(x=>x.EmployeeCode).IsUnique().HasFilter("[EmployeeCode] IS NOT NULL");
            e.HasIndex(x=>x.CitizenId).HasFilter("[CitizenId] IS NOT NULL");
            e.HasOne(x=>x.CompanyProfile).WithMany(x=>x.Drivers).HasForeignKey(x=>x.CompanyProfileId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CompanyProfile>(e =>
        {
            e.Property(x => x.RepresentativeSignatureFileUrl).HasMaxLength(500);
            e.Property(x => x.RepresentativeSignatureHash).HasMaxLength(128);
        });

        builder.Entity<Vehicle>(e =>
        {
            e.Property(x => x.OwnerSignatureFileUrl).HasMaxLength(500);
            e.Property(x => x.OwnerSignatureHash).HasMaxLength(128);
        });
    }
}
