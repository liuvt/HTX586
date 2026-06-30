namespace HTX586CONTRACT.Application.Admins.AdminAccounts;

public sealed class CreateAdminAccountResult
{
    public string UserId { get; set; } = string.Empty;
    public Guid CompanyProfileId { get; set; }
}
