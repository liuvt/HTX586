using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Identity;
using HTX586CONTRACT.Domain.Vehicles;

namespace HTX586CONTRACT.Domain.Companies;

/// <summary>
/// Danh mục đơn vị/văn phòng đại diện được Owner quản lý độc lập.
/// Một CompanyProfile có thể gán cho nhiều Admin/Driver và nhiều xe để lấy đúng thông tin/chữ ký trên Contract.
/// </summary>
public class CompanyProfile : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CompanyName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string? BusinessLicenseNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;
    public string? RepresentativePosition { get; set; }
    public string RepresentativeCitizenId { get; set; } = string.Empty;
    public DateTime? RepresentativeCitizenIdIssuedDate { get; set; }
    public string? RepresentativeCitizenIdIssuedPlace { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }

    // Chữ ký cố định của người đại diện HTX/văn phòng.
    // Contract tự lấy chữ ký này, khách hàng không cần yêu cầu bên HTX ký lại.
    public string? RepresentativeSignatureFileUrl { get; set; }
    public string? RepresentativeSignatureHash { get; set; }
    public DateTime? RepresentativeSignedAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Xóa trên giao diện chỉ đánh dấu ẩn; dữ liệu gốc vẫn được giữ trong database.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Một CompanyProfile có thể được gán cho nhiều Admin/Driver và nhiều xe.
    public ICollection<ApplicationUser> Users { get; set; } = [];
    public ICollection<Vehicle> Vehicles { get; set; } = [];

    // Hợp đồng lưu CompanyProfileId để xác định đơn vị đại diện tại thời điểm lập.
    public ICollection<Contract> Contracts { get; set; } = [];
}
