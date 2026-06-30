using HTX586CONTRACT.Domain.Common;
namespace HTX586CONTRACT.Domain.Contracts;
public class ContractTemplate : BaseEntity
{
    public Guid ContractTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string HtmlContent { get; set; } = string.Empty;
    public string? CssContent { get; set; }
    public string? VariablesJson { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public ContractType ContractType { get; set; } = null!;
    public ICollection<Contract> Contracts { get; set; } = [];
}
