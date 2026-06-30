using HTX586CONTRACT.Domain.Common;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
namespace HTX586CONTRACT.Domain.Signatures;
public class ContractSignature : BaseEntity
{
    public Guid ContractId { get; set; }
    public SignatureParty Party { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string? SignerPhone { get; set; }
    public string SignatureFileUrl { get; set; } = string.Empty;
    public string? SignatureVectorJson { get; set; }
    public string SignatureHash { get; set; } = string.Empty;
    public string ContractHashAtSigning { get; set; } = string.Empty;
    public DateTime DeviceSignedAt { get; set; }
    public DateTime ServerSignedAt { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public double? LocationAccuracy { get; set; }
    public string? LocationAddress { get; set; }
    public string? LocationError { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? OperatingSystem { get; set; }
    public string? BrowserName { get; set; }
    public string? AppVersion { get; set; }
    public string? ConsentText { get; set; }
    public Contract Contract { get; set; } = null!;
}
