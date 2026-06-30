using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Quản lý chữ ký cố định của Company, Vehicle và User(Driver).
/// Những chữ ký này được tạo một lần ở danh mục và tự động chèn vào mọi hợp đồng.
/// </summary>
public sealed class MasterSignatureService(
    IDbContextFactory<ApplicationDbContext> factory,
    IUploadFileStorage storage)
{
    public async Task<string> SaveCompanyRepresentativeSignatureAsync(
        Guid companyId,
        string dataUrl,
        CancellationToken ct = default)
    {
        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "companies", companyId.ToString("N")],
            "representative",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.CompanyProfiles
            .Where(x => x.Id == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RepresentativeSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.RepresentativeSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.RepresentativeSignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        if (updated != 1)
            throw new KeyNotFoundException("Không tìm thấy công ty/văn phòng đại diện để lưu chữ ký.");

        return stored.RelativeUrl;
    }

    public async Task<string> SaveVehicleOwnerSignatureAsync(
        Guid vehicleId,
        string dataUrl,
        CancellationToken ct = default)
    {
        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "vehicles", vehicleId.ToString("N")],
            "owner",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.Vehicles
            .Where(x => x.Id == vehicleId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.OwnerSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.OwnerSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.OwnerSignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        if (updated != 1)
            throw new KeyNotFoundException("Không tìm thấy xe để lưu chữ ký chủ xe.");

        return stored.RelativeUrl;
    }

    public async Task<string> SaveDriverSignatureAsync(
        string userId,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("Thiếu tài khoản tài xế.");

        var stored = await storage.SavePngDataUrlAsync(
            ["master-signatures", "drivers", userId],
            "driver",
            dataUrl,
            ct);

        await using var db = await factory.CreateDbContextAsync(ct);
        var updated = await db.Users
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DriverSignatureFileUrl, stored.RelativeUrl)
                .SetProperty(x => x.DriverSignatureHash, stored.Sha256Hash)
                .SetProperty(x => x.DriverSignedAt, stored.SavedAt)
                .SetProperty(x => x.UpdatedAt, stored.SavedAt),
                ct);

        if (updated != 1)
            throw new KeyNotFoundException("Không tìm thấy tài xế để lưu chữ ký cố định.");

        return stored.RelativeUrl;
    }
}
