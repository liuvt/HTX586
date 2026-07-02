using System.Text.Json;
using System.Text.Json.Serialization;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Service quản lý template PDF và file layout.json cho màn hình kéo thả vị trí data.
/// Không tham gia xuất hợp đồng thật, chỉ dùng để chỉnh tọa độ/format layout.
/// </summary>
public sealed class PdfLayoutDesignerService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<PdfLayoutDesignerService> logger)
{
    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public async Task<PdfLayoutDesignerDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        var templatePath = ResolveContentPath(
            configuration["DocumentGeneration:ContractTemplatePath"],
            Path.Combine("Templates", "Contracts", "HopDongVanChuyenHanhKhach.template.pdf"));

        var layoutPath = ResolveContentPath(
            configuration["DocumentGeneration:ContractLayoutPath"],
            Path.Combine("Templates", "Contracts", "HopDongVanChuyenHanhKhach.layout.json"));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Không tìm thấy PDF template tại '{templatePath}'.", templatePath);

        if (!File.Exists(layoutPath))
            throw new FileNotFoundException($"Không tìm thấy layout JSON tại '{layoutPath}'.", layoutPath);

        var layoutJson = await File.ReadAllTextAsync(layoutPath, cancellationToken);
        var layout = JsonSerializer.Deserialize<PdfTemplateLayoutDto>(layoutJson, ReadJsonOptions)
            ?? throw new InvalidOperationException("Không thể đọc layout JSON.");

        var pdfBytes = await File.ReadAllBytesAsync(templatePath, cancellationToken);

        return new PdfLayoutDesignerDocument
        {
            TemplatePath = templatePath,
            LayoutPath = layoutPath,
            TemplateBase64 = Convert.ToBase64String(pdfBytes),
            Layout = layout
        };
    }

    public async Task SaveLayoutAsync(
        PdfTemplateLayoutDto layout,
        CancellationToken cancellationToken = default)
    {
        var layoutPath = ResolveContentPath(
            configuration["DocumentGeneration:ContractLayoutPath"],
            Path.Combine("Templates", "Contracts", "HopDongVanChuyenHanhKhach.layout.json"));

        Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);

        if (File.Exists(layoutPath))
        {
            var backupPath = Path.Combine(
                Path.GetDirectoryName(layoutPath)!,
                $"{Path.GetFileName(layoutPath)}.bak-{DateTime.Now:yyyyMMddHHmmss}");
            File.Copy(layoutPath, backupPath, overwrite: false);
            logger.LogInformation("Đã backup layout PDF. Backup={BackupPath}", backupPath);
        }

        var json = JsonSerializer.Serialize(layout, WriteJsonOptions);
        await File.WriteAllTextAsync(layoutPath, json, cancellationToken);

        logger.LogInformation("Đã lưu layout PDF. Layout={LayoutPath}", layoutPath);
    }

    private string ResolveContentPath(string? configuredPath, string defaultRelativePath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRelativePath
            : configuredPath.Trim();

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));
    }
}

public sealed class PdfLayoutDesignerDocument
{
    public string TemplatePath { get; set; } = string.Empty;
    public string LayoutPath { get; set; } = string.Empty;
    public string TemplateBase64 { get; set; } = string.Empty;
    public PdfTemplateLayoutDto Layout { get; set; } = new();
}

public sealed class PdfTemplateLayoutDto
{
    public int Version { get; set; }
    public string CoordinateSystem { get; set; } = "PDF points, origin top-left, A4 595.28 x 841.89";
    public string TemplateFile { get; set; } = "HopDongVanChuyenHanhKhach.template.pdf";
    public string FontFamily { get; set; } = "Times New Roman";
    public List<string> FallbackFontFamilies { get; set; } = [];
    public List<PdfTextFieldLayoutDto> TextFields { get; set; } = [];
    public List<PdfImageFieldLayoutDto> ImageFields { get; set; } = [];
}

public sealed class PdfTextFieldLayoutDto
{
    public string Key { get; set; } = string.Empty;
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public float FontSize { get; set; } = 11f;
    public float MinFontSize { get; set; } = 8f;
    public int MaxLines { get; set; } = 1;
    public string Alignment { get; set; } = "Left";
    public string VerticalAlignment { get; set; } = "Center";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Uppercase { get; set; }
    public bool ClearBackground { get; set; }
}

public sealed class PdfImageFieldLayoutDto
{
    public string Key { get; set; } = string.Empty;
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Fit { get; set; } = "Contain";
}

public sealed class PdfLayoutDesignerFieldUpdate
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Page { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
