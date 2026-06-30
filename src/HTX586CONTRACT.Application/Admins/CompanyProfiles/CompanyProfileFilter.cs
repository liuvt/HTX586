namespace HTX586CONTRACT.Application.Admins.CompanyProfiles;

public sealed class CompanyProfileFilter
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 100;
}