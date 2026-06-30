using HTX586CONTRACT.Application.Admins.DriverAccounts;

namespace HTX586CONTRACT.Application.Abstractions;

public interface IDriverAccountService
{
    Task<string> CreateAsync(
        CreateDriverAccountRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        string userId,
        UpdateDriverAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverAccountDetailDto?> GetDetailAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAccountDto>> GetListAsync(
        DriverAccountFilter filter,
        CancellationToken cancellationToken = default);

    Task SetActiveAsync(
        string userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string userId,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Admin yêu cầu người dùng đổi mật khẩu khi đăng nhập lần tiếp theo
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RequirePasswordChangeAsync(
    string userId,
    CancellationToken cancellationToken = default);
}