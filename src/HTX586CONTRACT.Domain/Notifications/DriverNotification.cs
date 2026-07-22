using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Identity;

namespace HTX586CONTRACT.Domain.Notifications;

/// <summary>
/// Thông báo nghiệp vụ dành riêng cho tài xế.
/// </summary>
public sealed class DriverNotification : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DriverId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public Guid? RelatedContractId { get; set; }
    public Guid? RelatedVehicleId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ApplicationUser Driver { get; set; } = null!;
}
