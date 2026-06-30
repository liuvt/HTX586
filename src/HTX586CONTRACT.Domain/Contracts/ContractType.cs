using HTX586CONTRACT.Domain.Common;
namespace HTX586CONTRACT.Domain.Contracts;
public class ContractType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequireCustomerSignature { get; set; } = true;
    public bool RequireDriverSignature { get; set; } = true;
    public bool RequireLocation { get; set; } = true;
    public bool RequireCustomerOtp { get; set; }
    public bool RequireCustomerDocument { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ContractTemplate> Templates { get; set; } = [];
    public ICollection<Contract> Contracts { get; set; } = [];
}
