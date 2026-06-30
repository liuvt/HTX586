namespace HTX586CONTRACT.Application.Admins.CompanyProfiles;

public sealed class CompanyProfileOptionDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string TaxCode { get; set; } = string.Empty;
}
