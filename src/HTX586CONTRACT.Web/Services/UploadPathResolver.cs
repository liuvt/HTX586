using HTX586CONTRACT.Web.Options;

namespace HTX586CONTRACT.Web.Services;

public static class UploadPathResolver
{
    public static string ResolveUploadRootPath(string contentRootPath, string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? new FileStorageOptions().UploadRootPath
            : configuredPath.Trim();

        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.Combine(contentRootPath, path);

        return Path.GetFullPath(resolved);
    }

    public static string NormalizeRequestPath(string? requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
            return "/uploads";

        var normalized = requestPath.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        return normalized.TrimEnd('/');
    }
}
