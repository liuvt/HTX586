namespace HTX586CONTRACT.Application.Admins.AdminAccounts;

public sealed class UpdateAdminAccountRequest
{
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? EmployeeCode { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }
}
