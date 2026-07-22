using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Companies;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Customers;
using HTX586CONTRACT.Domain.Notifications;
using Microsoft.AspNetCore.Identity;

namespace HTX586CONTRACT.Domain.Identity;

public class ApplicationUser : IdentityUser, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }

    // Owner không cần CompanyProfile. Admin và Driver được gán CompanyProfile để xác định đơn vị quản lý/chữ ký.
    public Guid? CompanyProfileId { get; set; }
    public CompanyProfile? CompanyProfile { get; set; }

    public string? CitizenId { get; set; }
    public DateTime? CitizenIdIssuedDate { get; set; }
    public string? CitizenIdIssuedPlace { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? AreaCode { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CitizenIdFrontUrl { get; set; }
    public string? CitizenIdBackUrl { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseClass { get; set; }
    public DateTime? DriverLicenseIssuedDate { get; set; }
    public DateTime? DriverLicenseExpiryDate { get; set; }
    public string? DriverLicenseFrontUrl { get; set; }
    public string? DriverLicenseBackUrl { get; set; }

    // Chữ ký cố định của tài xế.
    public string? DriverSignatureFileUrl { get; set; }
    // Chữ ký cố định của tài xế.
    public string? DriverSignatureHash { get; set; }
    // Thời gian tạo chữ ký cố định của tài xế.
    public DateTime? DriverSignedAt { get; set; }
    // Trạng thái chữ ký.
    public bool DriverSignatureIsActive { get; set; }
    // Thơi gian vô hiệu hóa chữ ký
    public DateTime? DriverSignatureInactiveAt { get; set; }

    // Trạng thái đăng ký: Approved (tạo bởi quản trị/đã duyệt), Pending, Rejected.
    public string RegistrationStatus { get; set; } = "Approved";
    // Thời gian yêu cầu đăng ký tài khoản (tạo bởi quản trị/đã duyệt).
    public DateTime? RegistrationRequestedAt { get; set; }
    // Thời gian xem xét đăng ký tài khoản (tạo bởi quản trị/đã duyệt).
    public DateTime? RegistrationViewedAt { get; set; }
    // Ai đã xem xét đăng ký tài khoản (tạo bởi quản trị/đã duyệt).
    public string? RegistrationViewedByUserId { get; set; }
    public DateTime? RegistrationReviewedAt { get; set; }
    public string? RegistrationReviewedByUserId { get; set; }
    public string? RegistrationReviewNote { get; set; }

    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Tài khoản bị "xóa" chỉ được ẩn và khóa đăng nhập, không xóa khỏi AspNetUsers.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<Customer> CreatedCustomers { get; set; } = [];
    public ICollection<DriverNotification> DriverNotifications { get; set; } = [];
}
