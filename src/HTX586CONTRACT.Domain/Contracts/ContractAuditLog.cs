namespace HTX586CONTRACT.Domain.Contracts;
public class ContractAuditLog
{
    public long Id { get; set; }
    public Guid ContractId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldDataJson { get; set; }
    public string? NewDataJson { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Contract Contract { get; set; } = null!;
}
