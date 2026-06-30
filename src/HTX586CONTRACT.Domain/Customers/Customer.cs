using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Identity;
namespace HTX586CONTRACT.Domain.Customers;
public class Customer : BaseEntity
{
    public CustomerType Type { get; set; } = CustomerType.Individual;
    public string FullName { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string? TaxCode { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Email { get; set; }
    public string CreatedByDriverId { get; set; } = string.Empty;
    public DateTime? LastUsedAt { get; set; }
    public int ContractCount { get; set; }
    public ApplicationUser CreatedByDriver { get; set; } = null!;
    public ICollection<Contract> Contracts { get; set; } = [];
}
