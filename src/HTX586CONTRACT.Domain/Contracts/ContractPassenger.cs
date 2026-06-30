using HTX586CONTRACT.Domain.Common;

namespace HTX586CONTRACT.Domain.Contracts;

public sealed class ContractPassenger : BaseEntity
{
    public Guid ContractId { get; set; }
    public int SortOrder { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public string? Note { get; set; }
    public Contract Contract { get; set; } = null!;
}
