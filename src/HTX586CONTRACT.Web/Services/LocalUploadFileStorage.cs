using System.Security.Cryptography;
using HTX586CONTRACT.Web.Options;
using Microsoft.Extensions.Options;

namespace HTX586CONTRACT.Web.Services;

public sealed record StoredUploadFile(string RelativeUrl, string Sha256Hash, DateTime SavedAt);

public interface IUploadFileStorage
{
    Task<StoredUploadFile> SavePngDataUrlAsync(
        IReadOnlyList<string> folderSegments,
        string prefix,
        string dataUrl,
        CancellationToken ct = default);

    string GetPhysicalDirectory(IReadOnlyList<string> folderSegments);

    string BuildRelativeUrl(IReadOnlyList<string> folderSegments, string fileName);

    string? ToPhysicalPath(string? relativeUrl);

    bool FileExists(string? relativeUrl);

    string GetUploadRootPath();
}

/// <summary>
/// Lưu file upload vào thư mục cấu hình riêng, không phụ thuộc wwwroot.
/// URL public mặc định vẫn là /uploads/... để tương thích dữ liệu đã lưu trong database.
/// </summary>
public sealed class LocalUploadFileStorage(
    IWebHostEnvironment environment,
    IOptions<FileStorageOptions> options) : IUploadFileStorage
{
    private const int MaxSignatureBytes = 2 * 1024 * 1024;

    private string UploadRootPath => UploadPathResolver.ResolveUploadRootPath(
        environment.ContentRootPath,
        options.Value.UploadRootPath);

    private string PublicRequestPath => UploadPathResolver.NormalizeRequestPath(
        options.Value.PublicRequestPath);

    public async Task<StoredUploadFile> SavePngDataUrlAsync(
        IReadOnlyList<string> folderSegments,
        string prefix,
        string dataUrl,
        CancellationToken ct = default)
    {
        if (folderSegments.Count == 0)
            throw new InvalidOperationException("Thiếu thư mục lưu chữ ký.");

        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "signature";

        var bytes = DecodeDataUrl(dataUrl);
        if (bytes.Length == 0 || bytes.Length > MaxSignatureBytes)
            throw new InvalidOperationException("Dung lượng chữ ký không hợp lệ hoặc vượt quá 2 MB.");

        var safePrefix = SafeSegment(prefix);
        var safeFolderSegments = folderSegments.Select(SafeSegment).ToArray();
        var fileName = $"{safePrefix}-{Guid.NewGuid():N}.png";
        var physicalDirectory = GetPhysicalDirectory(safeFolderSegments);

        Directory.CreateDirectory(physicalDirectory);

        var physicalPath = Path.Combine(physicalDirectory, fileName);
        var tempPath = Path.Combine(physicalDirectory, $".{fileName}.uploading");

        await File.WriteAllBytesAsync(tempPath, bytes, ct);
        File.Move(tempPath, physicalPath, overwrite: true);

        var relativeUrl = BuildRelativeUrl(safeFolderSegments, fileName);
        return new StoredUploadFile(
            relativeUrl,
            Convert.ToHexString(SHA256.HashData(bytes)),
            DateTime.UtcNow);
    }

    public string GetPhysicalDirectory(IReadOnlyList<string> folderSegments)
    {
        if (folderSegments.Count == 0)
            return UploadRootPath;

        var safeSegments = folderSegments.Select(SafeSegment).ToArray();
        var directory = Path.Combine(new[] { UploadRootPath }.Concat(safeSegments).ToArray());
        var fullDirectory = Path.GetFullPath(directory);
        EnsureInsideUploadRoot(fullDirectory);
        return fullDirectory;
    }

    public string BuildRelativeUrl(IReadOnlyList<string> folderSegments, string fileName)
    {
        var safeSegments = folderSegments.Select(SafeSegment).ToArray();
        var safeFileName = SafeFileName(fileName);
        return PublicRequestPath + "/" + string.Join("/", safeSegments.Append(safeFileName));
    }

    public string? ToPhysicalPath(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return null;

        var url = relativeUrl.Trim().Replace('\\', '/');
        if (!url.StartsWith('/'))
            url = "/" + url;

        var publicRequestPath = PublicRequestPath;
        string relativePath;

        if (url.Equals(publicRequestPath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = string.Empty;
        }
        else if (url.StartsWith(publicRequestPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = url[(publicRequestPath.Length + 1)..];
        }
        else if (url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            // Tương thích các URL cũ đã lưu trong database trước khi tách UploadRootPath.
            relativePath = url["/uploads/".Length..];
        }
        else
        {
            relativePath = url.TrimStart('/');
        }

        var physicalPath = Path.GetFullPath(Path.Combine(
            UploadRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        EnsureInsideUploadRoot(physicalPath);
        return physicalPath;
    }

    public bool FileExists(string? relativeUrl)
    {
        var path = ToPhysicalPath(relativeUrl);
        return path is not null && File.Exists(path);
    }

    public string GetUploadRootPath()
    {
        var path = UploadRootPath;
        Directory.CreateDirectory(path);
        return path;
    }

    private void EnsureInsideUploadRoot(string fullPath)
    {
        var root = Path.GetFullPath(UploadRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(fullPath);

        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đường dẫn file upload không hợp lệ.");
        }
    }

    private static byte[] DecodeDataUrl(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0)
            throw new InvalidOperationException("Dữ liệu chữ ký không hợp lệ.");

        var header = dataUrl[..comma];
        if (!header.Contains("image/png", StringComparison.OrdinalIgnoreCase) &&
            !header.Contains("image/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chữ ký phải là dữ liệu ảnh.");

        try
        {
            return Convert.FromBase64String(dataUrl[(comma + 1)..]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Dữ liệu chữ ký không đúng định dạng Base64.");
        }
    }

    private static string SafeSegment(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();

        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "file" : result.ToLowerInvariant();
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            return $"file-{Guid.NewGuid():N}";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name
            .Select(character => invalid.Contains(character) || !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                ? '-'
                : character)
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"file-{Guid.NewGuid():N}"
            : sanitized;
    }
}
