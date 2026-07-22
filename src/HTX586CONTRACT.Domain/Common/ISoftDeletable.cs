namespace HTX586CONTRACT.Domain.Common;

/// <summary>
/// Đánh dấu bản ghi chỉ được phép xóa mềm.
/// Ứng dụng không xóa vật lý các bản ghi triển khai interface này.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
}
