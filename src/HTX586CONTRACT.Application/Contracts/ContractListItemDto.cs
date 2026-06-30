using HTX586CONTRACT.Domain.Enums;

namespace HTX586CONTRACT.Application.Contracts;

public sealed class ContractListItemDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public ContractBusinessType BusinessType { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    public DateTime? StartTime { get; set; }
    public decimal? ContractValue { get; set; }
    public ContractStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
