using HTX586CONTRACT.Application.Admins.AdminAccounts;
using HTX586CONTRACT.Application.Common;

namespace HTX586CONTRACT.Application.Abstractions;

public interface IAdminAccountService
{
    Task<IReadOnlyList<AdminAccountListItem>> GetAccountsAsync(string? keyword = null, CancellationToken cancellationToken = default);
    Task<CreateAdminAccountResult> CreateAdminAsync(CreateAdminAccountRequest request, CancellationToken cancellationToken = default);
    Task<AdminAccountDetail?> GetDetailAsync(string userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAccountAsync(UpdateAdminAccountRequest request, CancellationToken cancellationToken = default);
}
