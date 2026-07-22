using HTX586CONTRACT.Domain.Common;

namespace HTX586CONTRACT.Domain.Contracts;

/// <summary>
/// Nhật ký hợp đồng là dữ liệu append-only. Việc triển khai ISoftDeletable
/// bảo đảm một lệnh Remove vô tình cũng chỉ ẩn bản ghi, không xóa vật lý.
/// </summary>
public class ContractAuditLog : ISoftDeletable
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
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public Contract Contract { get; set; } = null!;
}
