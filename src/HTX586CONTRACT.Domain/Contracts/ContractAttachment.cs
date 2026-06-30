using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Enums;
namespace HTX586CONTRACT.Domain.Contracts;
public class ContractAttachment : BaseEntity
{
    public Guid ContractId { get; set; }
    public AttachmentType AttachmentType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
    public Contract Contract { get; set; } = null!;
}
