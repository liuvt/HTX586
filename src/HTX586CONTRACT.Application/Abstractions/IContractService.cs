using HTX586CONTRACT.Application.Contracts;
namespace HTX586CONTRACT.Application.Abstractions;
public interface IContractService
{
    Task<IReadOnlyList<ContractListItemDto>> GetAsync(ContractFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractListItemDto>> GetDriverContractsAsync(string driverId, CancellationToken cancellationToken = default);
    Task<ContractDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SaveContractResult> CreateAsync(SaveContractRequest request, string userId, CancellationToken cancellationToken = default);
    Task<SaveContractResult> UpdateAsync(Guid id, SaveContractRequest request, string userId, CancellationToken cancellationToken = default);
    Task<SaveContractResult> CancelByDriverAsync(Guid id, string userId, string? reason = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
