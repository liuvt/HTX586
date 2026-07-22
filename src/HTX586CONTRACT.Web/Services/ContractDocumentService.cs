using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HTX586CONTRACT.Application.Abstractions;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using HTX586CONTRACT.Domain.Signatures;
using HTX586CONTRACT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Web.Services;

public sealed class ContractDocumentService(
    IDbContextFactory<ApplicationDbContext> factory,
    PdfContractTemplateRenderer pdfTemplateRenderer,
    IUploadFileStorage storage,
    ILogger<ContractDocumentService> logger) : IContractDocumentService
{
    // Lưu chữ ký của khách hàng vào thư mục upload riêng, không dùng wwwroot. File chỉ trở thành file chính thức   
    public async Task<string> SaveSignatureAsync(
        Guid contractId,
        string currentUserId,
        string party,
        string signerName,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
            throw new InvalidOperationException("Không xác định được tài khoản tài xế đang ký hợp đồng.");

        if (!Enum.TryParse<SignatureParty>(party, true, out var role))
            throw new InvalidOperationException("Vai trò ký không hợp lệ.");

        if (role != SignatureParty.Customer)
            throw new InvalidOperationException("Company, chủ xe và tài xế dùng chữ ký cố định trong danh mục. Trên hợp đồng chỉ yêu cầu khách hàng ký.");

        logger.LogInformation(
            "Signature pipeline v3-rowversion-free. ContractId={ContractId}, Party={Party}.",
            contractId,
            role);

        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
            throw new InvalidOperationException("Dữ liệu chữ ký không hợp lệ.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Dữ liệu chữ ký không đúng định dạng Base64.");
        }

        if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024)
            throw new InvalidOperationException("Dung lượng chữ ký không hợp lệ hoặc vượt quá 2 MB.");

        var extension = DetectImageExtension(bytes)
            ?? throw new InvalidOperationException("Chữ ký phải là ảnh PNG hoặc JPG hợp lệ.");

        var signatureFolderSegments = new[]
        {
            "contracts",
            contractId.ToString("N"),
            "signatures"
        };
        var directory = storage.GetPhysicalDirectory(signatureFolderSegments);
        Directory.CreateDirectory(directory);

        // Mỗi lần ký dùng một tên file riêng. File chỉ trở thành file chính thức
        // sau khi INSERT chữ ký và UPDATE trạng thái hợp đồng đều thành công.
        var fileName = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(directory, fileName);
        var tempPath = Path.Combine(directory, $".{fileName}.uploading");
        var relativeUrl = storage.BuildRelativeUrl(signatureFolderSegments, fileName);

        await File.WriteAllBytesAsync(tempPath, bytes, ct);
        var finalFileCreated = false;

        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            var callerIsActive = await db.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == currentUserId && x.IsActive && !x.IsDeleted, ct);

            var callerRoles = await (
                    from userRole in db.UserRoles
                    join identityRole in db.Roles on userRole.RoleId equals identityRole.Id
                    where userRole.UserId == currentUserId
                    select identityRole.Name)
                .Where(x => x != null)
                .ToListAsync(ct);

            var callerIsDriver = callerRoles.Any(x =>
                string.Equals(x, "Driver", StringComparison.OrdinalIgnoreCase));
            var callerIsOwnerOrAdmin = callerRoles.Any(x =>
                string.Equals(x, "Owner", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x, "Admin", StringComparison.OrdinalIgnoreCase));

            if (!callerIsActive || !callerIsDriver || callerIsOwnerOrAdmin)
                throw new InvalidOperationException(
                    "Chỉ tài khoản Driver đang hoạt động mới được ghi nhận chữ ký khách hàng và chốt hợp đồng hoàn tất.");

            // Transaction Serializable được mở TRƯỚC khi đọc hợp đồng. Nhờ đó hai
            // thao tác ký cùng một hợp đồng không thể cùng vượt qua bước kiểm tra.
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

            try
            {
                // Không track Contract để EF không sinh UPDATE có điều kiện RowVersion
                // từ một bản ghi đã bị thay đổi bởi thao tác ký khác.
                var contract = await db.Contracts
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM [dbo].[Contracts] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        WHERE [Id] = {contractId} AND [IsDeleted] = 0
                        """)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct)
                    ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

                if (!string.Equals(contract.DriverId, currentUserId, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Chỉ tài xế được gán trên hợp đồng mới được ghi nhận chữ ký của khách hàng.");

                if (contract.Status is ContractStatus.Cancelled or ContractStatus.Expired or ContractStatus.Invalidated)
                    throw new InvalidOperationException("Hợp đồng đã bị hủy, hết hạn hoặc vô hiệu hóa.");

                if (contract.Status == ContractStatus.Completed)
                    throw new InvalidOperationException(
                        "Hợp đồng đã đủ chữ ký và bị khóa. Không thể thay đổi chữ ký.");

                if (contract.Status != ContractStatus.WaitingCustomerSignature)
                    throw new InvalidOperationException(
                        "Thông tin khách hàng và danh sách hành khách phải được lưu trước khi ghi nhận chữ ký.");

                var signatureExists = await db.ContractSignatures
                    .AsNoTracking()
                    .AnyAsync(x => x.ContractId == contractId && x.Party == role, ct);

                if (signatureExists)
                    throw new InvalidOperationException(
                        $"{RoleName(role)} đã ký trước đó. Chữ ký đã xác nhận không được phép ghi đè.");

                var finalSnapshotJson = await EnsureCompletionSnapshotAsync(db, contract, ct);
                contract.ContractDataJson = finalSnapshotJson;

                var now = DateTime.UtcNow;
                var signature = new ContractSignature
                {
                    Id = Guid.NewGuid(),
                    ContractId = contractId,
                    Party = role,
                    SignerName = string.IsNullOrWhiteSpace(signerName)
                        ? DefaultSignerName(contract, role)
                        : signerName.Trim(),
                    SignatureFileUrl = relativeUrl,
                    SignatureHash = Convert.ToHexString(SHA256.HashData(bytes)),
                    ContractHashAtSigning = ContractHash(contract),
                    DeviceSignedAt = now,
                    ServerSignedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                // INSERT trực tiếp để luồng ký tuyệt đối không đi qua ChangeTracker/SaveChanges.
                // Vì vậy EF không thể phát sinh UPDATE Contracts kèm RowVersion cũ.
                var insertedRows = await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [dbo].[ContractSignatures]
                    (
                        [Id], [ContractId], [Party], [SignerName],
                        [SignatureFileUrl], [SignatureHash], [ContractHashAtSigning],
                        [DeviceSignedAt], [ServerSignedAt],
                        [CreatedAt], [UpdatedAt], [IsDeleted]
                    )
                    VALUES
                    (
                        {signature.Id}, {signature.ContractId}, {(int)signature.Party}, {signature.SignerName},
                        {signature.SignatureFileUrl}, {signature.SignatureHash}, {signature.ContractHashAtSigning},
                        {signature.DeviceSignedAt}, {signature.ServerSignedAt},
                        {signature.CreatedAt}, {signature.UpdatedAt}, {false}
                    );
                    """, ct);

                if (insertedRows != 1)
                    throw new InvalidOperationException("Không thể thêm bản ghi chữ ký vào SQL.");

                var nextStatus = ContractStatus.Completed;

                contract.Status = nextStatus;
                contract.CompletedAt = now;
                contract.UpdatedAt = now;
                var contractHash = ContractHash(contract);

                // ExecuteUpdate tạo UPDATE nguyên tử theo Id và không dùng RowVersion cũ.
                // Transaction Serializable vẫn bảo đảm không ghi đè một thao tác ký khác.
                var updatedRows = await db.Contracts
                    .Where(x => x.Id == contractId && x.Status == ContractStatus.WaitingCustomerSignature)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, nextStatus)
                        .SetProperty(x => x.CompletedAt, now)
                        .SetProperty(x => x.UpdatedAt, now)
                        .SetProperty(x => x.UpdatedBy, currentUserId)
                        .SetProperty(x => x.ContractDataJson, finalSnapshotJson)
                        .SetProperty(x => x.ContractHash, contractHash),
                        ct);

                if (updatedRows != 1)
                    throw new InvalidOperationException(
                        "Không thể cập nhật trạng thái hợp đồng sau khi lưu chữ ký.");

                File.Move(tempPath, fullPath);
                finalFileCreated = true;

                await transaction.CommitAsync(ct);

                // Ngay sau khi chốt trạng thái, tạo PDF chính thức từ snapshot để
                // template hoặc dữ liệu danh mục thay đổi về sau không ảnh hưởng HĐ.
                // Lỗi tạo PDF không rollback chữ ký đã lưu; người dùng vẫn có thể
                // bấm mở PDF để hệ thống tạo lại từ đúng snapshot bất biến.
                try
                {
                    await GeneratePdfAsync(contractId, ct);
                }
                catch (Exception pdfException)
                {
                    logger.LogError(
                        pdfException,
                        "Hợp đồng {ContractId} đã hoàn tất nhưng chưa thể tạo PDF chính thức ngay sau khi ký.",
                        contractId);
                }

                return relativeUrl;
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync(ct);
                }
                catch (Exception rollbackException)
                {
                    logger.LogError(
                        rollbackException,
                        "Không thể rollback giao dịch lưu chữ ký của hợp đồng {ContractId}.",
                        contractId);
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            TryDeleteFile(tempPath);
            if (finalFileCreated)
                TryDeleteFile(fullPath);

            logger.LogError(
                ex,
                "Lưu chữ ký thất bại. ContractId={ContractId}, Party={Party}.",
                contractId,
                role);

            throw ex switch
            {
                DbUpdateConcurrencyException => new InvalidOperationException(
                    "Không thể đồng bộ trạng thái hợp đồng khi lưu chữ ký. Vui lòng thử ký lại.",
                    ex),
                DbUpdateException => new InvalidOperationException(
                    "Không thể lưu chữ ký vào SQL. Hệ thống đã xóa file ảnh tạm để tránh lệch dữ liệu. " +
                    "Hãy kiểm tra bảng ContractSignatures và nhật ký lỗi SQL.",
                    ex),
                IOException => new InvalidOperationException(
                    "SQL đã được rollback vì không thể hoàn tất file ảnh chữ ký. Vui lòng kiểm tra quyền ghi thư mục UploadRootPath.",
                    ex),
                _ => ex
            };
        }
    }

    private static async Task<string> EnsureCompletionSnapshotAsync(
        ApplicationDbContext db,
        Contract contract,
        CancellationToken ct)
    {
        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);

        var company = await db.CompanyProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contract.CompanyProfileId, ct);
        var driver = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contract.DriverId, ct);
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == contract.CustomerId, ct);
        var vehicle = contract.VehicleId.HasValue
            ? await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == contract.VehicleId.Value, ct)
            : null;

        if (snapshot is null)
        {
            if (company is null || driver is null || customer is null || vehicle is null)
                throw new InvalidOperationException(
                    "Không thể tạo snapshot hoàn tất vì thiếu hồ sơ Công ty, tài xế, khách hàng hoặc xe.");

            // Hợp đồng cũ vẫn ưu tiên các cột snapshot đã lưu trước đây.
            contract.CompanyProfile = company;
            contract.Driver = driver;
            contract.Customer = customer;
            contract.Vehicle = vehicle;
            snapshot = ContractSnapshotData.CaptureLegacy(contract);
        }
        else
        {
            // Cho phép bổ sung chữ ký master còn thiếu trước thời điểm khách ký.
            // Các trường đã có trong snapshot tuyệt đối không bị ghi đè.
            if (company is not null)
            {
                snapshot.Company.RepresentativeSignatureFileUrl ??= company.RepresentativeSignatureFileUrl;
                snapshot.Company.RepresentativeSignatureHash ??= company.RepresentativeSignatureHash;
                snapshot.Company.RepresentativeSignedAt ??= company.RepresentativeSignedAt;
            }

            if (vehicle is not null)
            {
                snapshot.Vehicle.OwnerSignatureFileUrl ??= vehicle.OwnerSignatureFileUrl;
                snapshot.Vehicle.OwnerSignatureHash ??= vehicle.OwnerSignatureHash;
                snapshot.Vehicle.OwnerSignedAt ??= vehicle.OwnerSignedAt;
            }

            if (driver is not null)
            {
                snapshot.Driver.SignatureFileUrl ??= driver.DriverSignatureFileUrl;
                snapshot.Driver.SignatureHash ??= driver.DriverSignatureHash;
                snapshot.Driver.SignedAt ??= driver.DriverSignedAt;
            }
        }

        return snapshot.ToJson();
    }

    // Tạo PDF cuối cùng từ dữ liệu hợp đồng và chữ ký đã lưu. PDF được tạo trực tiếp từ layout JSON, không cần Word/LibreOffice.
    public async Task<string> GeneratePdfAsync(Guid contractId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        // Dữ liệu được điền trực tiếp lên PDF nền 2 trang theo file layout JSON.
        // Runtime không cần Word, LibreOffice hoặc executable cài ngoài.
        var contract = await db.Contracts.AsNoTracking()
            .Include(x => x.CompanyProfile)
            .Include(x => x.Driver)
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .Include(x => x.Signatures)
            .Include(x => x.Passengers)
            .FirstOrDefaultAsync(x => x.Id == contractId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");

        if (contract.Status != ContractStatus.Completed)
            throw new InvalidOperationException("Chỉ hợp đồng đã hoàn tất mới được tạo PDF chính thức.");

        // PDF đã sinh của hợp đồng hoàn tất là tài liệu bất biến. Không render lại
        // từ template hoặc danh mục hiện tại nếu file chính thức vẫn còn tồn tại.
        if (!string.IsNullOrWhiteSpace(contract.PdfFileUrl) && storage.FileExists(contract.PdfFileUrl))
            return contract.PdfFileUrl;

        var snapshot = ContractSnapshotData.FromJson(contract.ContractDataJson);
        if (snapshot is null)
        {
            snapshot = ContractSnapshotData.CaptureLegacy(contract);
            contract.ContractDataJson = snapshot.ToJson();

            await db.Contracts
                .Where(x => x.Id == contractId && (x.ContractDataJson == null || x.ContractDataJson == "{}"))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ContractDataJson, contract.ContractDataJson),
                    ct);
        }

        var missingSignatures = new List<string>();
        var signedRoles = contract.Signatures.Select(x => x.Party).ToHashSet();

        if (!StoredSignatureExists(snapshot.Company.RepresentativeSignatureFileUrl))
            missingSignatures.Add("chữ ký cố định Company/văn phòng đại diện tại thời điểm lập hợp đồng");

        if (!StoredSignatureExists(snapshot.Vehicle.OwnerSignatureFileUrl))
            missingSignatures.Add("chữ ký cố định chủ sở hữu xe tại thời điểm lập hợp đồng");

        if (!StoredSignatureExists(snapshot.Driver.SignatureFileUrl))
            missingSignatures.Add("chữ ký cố định tài xế tại thời điểm lập hợp đồng");

        if (!signedRoles.Contains(SignatureParty.Customer))
            missingSignatures.Add("chữ ký khách hàng");

        if (missingSignatures.Count > 0)
            throw new InvalidOperationException(
                $"Chưa thể tạo PDF cuối cùng. Còn thiếu: {string.Join(", ", missingSignatures)}.");

        var passengerCount = contract.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName));
        if (passengerCount > 20)
            throw new InvalidOperationException(
                "Mẫu PDF 2 trang chỉ hỗ trợ tối đa 20 hành khách. Vui lòng giảm danh sách trước khi tạo PDF.");

        var pdfFolderSegments = new[]
        {
            "contracts",
            contractId.ToString("N"),
            "pdf"
        };
        var directory = storage.GetPhysicalDirectory(pdfFolderSegments);
        Directory.CreateDirectory(directory);

        var fileName = $"hop-dong-{SafeFileName(contract.ContractNumber)}-{contractId:N}.pdf";
        var fullPath = Path.Combine(directory, fileName);
        var relativeUrl = storage.BuildRelativeUrl(pdfFolderSegments, fileName);

        await pdfTemplateRenderer.RenderPdfAsync(contract, fullPath, ct);

        var pdfBytes = await File.ReadAllBytesAsync(fullPath, ct);
        var generatedAt = DateTime.UtcNow;
        var pdfHash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        // Không dùng SaveChanges để tránh phát sinh UPDATE kèm RowVersion cũ.
        var updatedRows = await db.Contracts
            .Where(x => x.Id == contractId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.PdfFileUrl, relativeUrl)
                .SetProperty(x => x.PdfSha256, pdfHash)
                .SetProperty(x => x.PdfGeneratedAt, generatedAt),
                ct);

        if (updatedRows != 1)
        {
            TryDeleteFile(fullPath);
            throw new InvalidOperationException("Không thể cập nhật thông tin file PDF vào hợp đồng.");
        }

        return relativeUrl;
    }


    private bool StoredSignatureExists(string? relativeUrl)
        => storage.FileExists(relativeUrl);

    private static DateTime VietnamTime(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value.AddHours(7) : value;

    private static string FormatDateOnly(DateTime? value)
        => value?.ToString("dd/MM/yyyy") ?? "...";

    private static string FormatDateTime(DateTime? value)
        => value is null ? "..." : VietnamTime(value.Value).ToString("dd/MM/yyyy HH:mm");

    private static string FormatKilometers(decimal? value)
        => value is null ? "... km" : $"{value.Value:N1} km";

    private static string FormatMoney(decimal? value)
        => value is null
            ? "... đồng"
            : $"{value.Value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} đồng";


    private static string NumberToVietnameseWords(decimal? amount)
    {
        if (amount is null)
            return "chưa xác định";

        var number = (long)Math.Round(amount.Value, 0, MidpointRounding.AwayFromZero);
        if (number == 0)
            return "không";
        if (number < 0)
            return $"âm {ReadPositiveNumber(-number)}";
        return ReadPositiveNumber(number);
    }

    // Chuyển số nguyên dương sang chữ tiếng Việt, ví dụ 123456789 -> "một trăm hai mươi ba triệu bốn trăm năm mươi sáu nghìn bảy trăm tám mươi chín"
    private static string ReadPositiveNumber(long number)
    {
        string[] units = ["", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ"];
        var groups = new List<int>();
        while (number > 0)
        {
            groups.Add((int)(number % 1000));
            number /= 1000;
        }

        var parts = new List<string>();
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (group == 0)
                continue;

            var full = index < groups.Count - 1 && group < 100;
            var words = ReadThreeDigits(group, full);
            if (!string.IsNullOrWhiteSpace(words))
                parts.Add(string.IsNullOrEmpty(units[index])
                    ? words
                    : $"{words} {units[index]}");
        }

        return string.Join(" ", parts);
    }

    // Chuyển một số nguyên từ 0 đến 999 sang chữ tiếng Việt, ví dụ 123 -> "một trăm hai mươi ba"
    private static string ReadThreeDigits(int number, bool full)
    {
        string[] digit = ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];
        var hundreds = number / 100;
        var tens = (number % 100) / 10;
        var ones = number % 10;
        var parts = new List<string>();

        if (hundreds > 0 || full)
        {
            parts.Add($"{digit[hundreds]} trăm");
            if (tens == 0 && ones > 0)
                parts.Add("lẻ");
        }

        if (tens > 1)
        {
            parts.Add($"{digit[tens]} mươi");
            if (ones == 1)
                parts.Add("mốt");
            else if (ones == 4)
                parts.Add("tư");
            else if (ones == 5)
                parts.Add("lăm");
            else if (ones > 0)
                parts.Add(digit[ones]);
        }
        else if (tens == 1)
        {
            parts.Add("mười");
            if (ones == 5)
                parts.Add("lăm");
            else if (ones > 0)
                parts.Add(digit[ones]);
        }
        else if (ones > 0)
        {
            parts.Add(digit[ones]);
        }

        return string.Join(" ", parts);
    }


    // Chuyển tên hợp đồng sang dạng an toàn cho tên file, ví dụ "Hợp đồng #123" -> "hop-dong-123"
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) || !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                ? '-'
                : character)
            .ToArray())
            .Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "hop-dong" : sanitized;
    }

    private static string ShortHash(string value)
        => value.Length <= 16 ? value : value[..16];

    private byte[]? ReadSignature(string? relativeUrl)
    {
        var path = storage.ToPhysicalPath(relativeUrl);
        return path is not null && File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Không che mất lỗi gốc; file rác có thể được dọn bằng tác vụ bảo trì.
        }
    }

    // Hash bao phủ dữ liệu snapshot và toàn bộ nội dung nghiệp vụ chính của hợp đồng.
    // Sau khi hoàn tất, UpdateAsync bị chặn nên hash này đại diện cho bản hợp đồng bất biến.
    private static string ContractHash(Contract contract)
    {
        var payload = string.Join("|",
            contract.Id,
            contract.ContractNumber,
            contract.BusinessType,
            contract.CompanyProfileId,
            contract.DriverId,
            contract.CustomerId,
            contract.VehicleId,
            contract.AreaCode,
            contract.StartTime?.ToString("O"),
            contract.EndTime?.ToString("O"),
            contract.PickupLocation,
            contract.DropoffLocation,
            contract.RouteDescription,
            contract.TotalKilometers,
            contract.ContractValue,
            contract.PaymentMethod,
            contract.PaymentTime,
            contract.Note,
            contract.ActualPassengerCount,
            contract.ContractDataJson,
            contract.Status,
            contract.CompletedAt?.ToString("O"));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string? DetectImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
            return ".png";

        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xD8 &&
            bytes[2] == 0xFF)
            return ".jpg";

        return null;
    }

    // Lấy tên người ký mặc định từ dữ liệu snapshot của hợp đồng. Nếu không có snapshot, trả về chuỗi rỗng.
    private static string DefaultSignerName(HTX586CONTRACT.Domain.Contracts.Contract contract, SignatureParty role) => role switch
    {
        SignatureParty.RepresentativeOffice => contract.CompanyRepresentativeSnapshot,
        SignatureParty.VehicleOwner => contract.VehicleOwnerNameSnapshot ?? string.Empty,
        SignatureParty.Customer => contract.CustomerNameSnapshot,
        _ => contract.DriverNameSnapshot
    };

    // Lấy tên vai trò ký để hiển thị trong thông báo lỗi hoặc nhật ký. Ví dụ: SignatureParty.Customer -> "KHÁCH HÀNG (NGƯỜI THUÊ XE)"
    private static string RoleName(SignatureParty role) => role switch
    {
        SignatureParty.RepresentativeOffice => "VĂN PHÒNG ĐẠI DIỆN",
        SignatureParty.VehicleOwner => "CHỦ SỞ HỮU XE",
        SignatureParty.Customer => "KHÁCH HÀNG (NGƯỜI THUÊ XE)",
        _ => "TÀI XẾ CHẠY"
    };

    // Lấy tiêu đề hợp đồng theo loại kinh doanh.
    private static string BusinessTitle(ContractBusinessType type) => type switch
    {
        ContractBusinessType.Cargo => "HỢP ĐỒNG VẬN CHUYỂN HÀNG HÓA BẰNG XE Ô TÔ",
        _ => "HỢP ĐỒNG VẬN CHUYỂN HÀNH KHÁCH"
    };

    private static string FormatDate(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm") ?? "—";
}
