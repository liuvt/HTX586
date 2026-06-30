using HTX586CONTRACT.Domain.Enums;

namespace HTX586CONTRACT.Application.Contracts;

public sealed class ContractFilter
{
    public string? Search { get; set; }
    public ContractStatus? Status { get; set; }
    public ContractBusinessType? BusinessType { get; set; }
    public string? DriverId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class ContractPassengerDto
{
    public Guid? Id { get; set; }
    public int SortOrder { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int? BirthYear { get; set; }
    public string? Note { get; set; }
}

public sealed class ContractSignatureDto
{
    public Guid Id { get; set; }
    public SignatureParty Party { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignatureFileUrl { get; set; } = string.Empty;
    public DateTime ServerSignedAt { get; set; }
}

public sealed class ContractDetailDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public ContractBusinessType BusinessType { get; set; }
    public Guid ContractTypeId { get; set; }
    public ContractStatus Status { get; set; }
    public Guid CompanyProfileId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string DriverId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string? DriverLicenseClass { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerCitizenId { get; set; }
    public string? CustomerAddress { get; set; }
    public string AreaCode { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehicleBrand { get; set; }
    public int? SeatCount { get; set; }
    public int? ActualPassengerCount { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? CargoName { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? CargoUnit { get; set; }
    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? RouteDescription { get; set; }
    public decimal? TotalKilometers { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentTime { get; set; }
    public string? Note { get; set; }
    public string? PdfFileUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ContractPassengerDto> Passengers { get; set; } = [];
    public List<ContractSignatureDto> Signatures { get; set; } = [];
}

public sealed class SaveContractRequest
{
    public Guid? Id { get; set; }
    public string? ContractNumber { get; set; }
    public ContractBusinessType BusinessType { get; set; } = ContractBusinessType.Driver;
    public Guid? ContractTypeId { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerCitizenId { get; set; }
    public string? CustomerAddress { get; set; }
    public string AreaCode { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehiclePlate { get; set; }
    public string? VehicleCode { get; set; }
    public string? VehicleBrand { get; set; }
    public int? SeatCount { get; set; }
    public int? ActualPassengerCount { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerCitizenId { get; set; }
    public DateTime? OwnerCitizenIdIssuedDate { get; set; }
    public string? CargoName { get; set; }
    public decimal? CargoWeight { get; set; }
    public string? CargoUnit { get; set; }
    public string? SecondDriverName { get; set; }
    public string? SecondDriverLicenseClass { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? RouteDescription { get; set; }
    public decimal? TotalKilometers { get; set; }
    public decimal? ContractValue { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentTime { get; set; }
    public string? Note { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public List<ContractPassengerDto> Passengers { get; set; } = [];
}

public sealed record SaveContractResult(bool Succeeded, Guid? Id, string Message);
