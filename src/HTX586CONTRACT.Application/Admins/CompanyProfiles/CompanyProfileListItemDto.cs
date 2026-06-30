namespace HTX586CONTRACT.Application.Admins.CompanyProfiles;

public sealed class CompanyProfileListItemDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;
    public string? RepresentativeSignatureFileUrl { get; set; }
    public bool IsActive { get; set; }
    public int DriverCount { get; set; }
    public int ContractCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
