using System.Globalization;
using System.Text.Json;
using HTX586CONTRACT.Domain.Contracts;
using HTX586CONTRACT.Domain.Enums;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace HTX586CONTRACT.Web.Services;

/// <summary>
/// Điền dữ liệu lên PDF nền cố định. Runtime không cần Microsoft Word,
/// LibreOffice, trình duyệt headless hoặc executable cài ngoài.
/// </summary>
public sealed class PdfContractTemplateRenderer(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IUploadFileStorage storage,
    ILogger<PdfContractTemplateRenderer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task RenderPdfAsync(
        Contract contract,
        string outputPdfPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templatePath = ResolveContentPath(
            configuration["DocumentGeneration:ContractTemplatePath"],
            Path.Combine("Templates", "Contracts", "HopDongVanChuyenHanhKhach.template.pdf"));

        var layoutPath = ResolveContentPath(
            configuration["DocumentGeneration:ContractLayoutPath"],
            Path.Combine("Templates", "Contracts", "HopDongVanChuyenHanhKhach.layout.json"));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException(
                $"Không tìm thấy PDF nền tại '{templatePath}'.", templatePath);

        if (!File.Exists(layoutPath))
            throw new FileNotFoundException(
                $"Không tìm thấy file tọa độ PDF tại '{layoutPath}'.", layoutPath);

        var layout = JsonSerializer.Deserialize<PdfTemplateLayout>(
                File.ReadAllText(layoutPath), JsonOptions)
            ?? throw new InvalidOperationException("Không thể đọc cấu hình tọa độ PDF.");

        var textValues = BuildTextValues(contract);
        var imageValues = BuildImageValues(contract);
        var tempOutputPath = outputPdfPath + $".{Guid.NewGuid():N}.tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);

        try
        {
            using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);

            var expectedPages = Math.Max(
                layout.TextFields.Select(x => x.Page).DefaultIfEmpty(0).Max(),
                layout.ImageFields.Select(x => x.Page).DefaultIfEmpty(0).Max());

            if (document.PageCount < expectedPages)
                throw new InvalidOperationException(
                    $"PDF nền chỉ có {document.PageCount} trang nhưng layout cần {expectedPages} trang.");

            for (var pageNumber = 1; pageNumber <= expectedPages; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var graphics = XGraphics.FromPdfPage(
                    document.Pages[pageNumber - 1],
                    XGraphicsPdfPageOptions.Append);

                foreach (var field in layout.TextFields.Where(x => x.Page == pageNumber))
                {
                    if (!textValues.TryGetValue(field.Key, out var value) ||
                        string.IsNullOrWhiteSpace(value))
                        continue;

                    var text = field.Uppercase
                        ? value.ToUpper(CultureInfo.GetCultureInfo("vi-VN"))
                        : value;

                    if (field.ClearBackground)
                    {
                        graphics.DrawRectangle(
                            XBrushes.White,
                            field.X,
                            field.Y,
                            field.Width,
                            field.Height);
                    }

                    var renderedText = TextPngRenderer.Render(
                        text,
                        field,
                        layout.FontFamily,
                        layout.FallbackFontFamilies);

                    using var stream = new MemoryStream(
                        renderedText.PngBytes,
                        0,
                        renderedText.PngBytes.Length,
                        writable: false,
                        publiclyVisible: true);
                    using var image = XImage.FromStream(stream);

                    var drawX = ResolveAlignedX(field, renderedText.WidthPoints);
                    var drawY = ResolveAlignedY(field, renderedText.HeightPoints);

                    graphics.DrawImage(
                        image,
                        drawX,
                        drawY,
                        renderedText.WidthPoints,
                        renderedText.HeightPoints);
                }

                foreach (var field in layout.ImageFields.Where(x => x.Page == pageNumber))
                {
                    if (!imageValues.TryGetValue(field.Key, out var imagePath) ||
                        string.IsNullOrWhiteSpace(imagePath) ||
                        !File.Exists(imagePath))
                        continue;

                    using var image = XImage.FromFile(imagePath);
                    DrawContainedImage(graphics, image, field);
                }
            }

            NormalizeDocumentMetadata(document, contract);
            document.Save(tempOutputPath);

            File.Move(tempOutputPath, outputPdfPath, overwrite: true);

            logger.LogInformation(
                "Đã xuất PDF từ PDF nền. ContractId={ContractId}, Output={Output}",
                contract.Id,
                outputPdfPath);

            return Task.CompletedTask;
        }
        catch
        {
            TryDeleteFile(tempOutputPath);
            throw;
        }
    }


    /// <summary>
    /// Làm sạch metadata của PDF nền trước khi lưu.
    /// Một số PDF xuất từ Word/LibreOffice chứa các khóa /Info có giá trị null
    /// hoặc object thay vì string. PDFsharp 6.2.4 sẽ ném lỗi
    /// "GetString: Object is not a string" khi tạo XMP metadata lúc Save().
    /// </summary>
    private static void NormalizeDocumentMetadata(PdfSharp.Pdf.PdfDocument document, Contract contract)
    {
        var info = document.Info;

        // Không đọc các property cũ vì bản thân getter có thể ném InvalidCastException.
        // Xóa trực tiếp toàn bộ khóa metadata chuẩn rồi ghi lại bằng đúng kiểu PDF string/date.
        string[] keysToRemove =
        [
            "/Title",
            "/Author",
            "/Subject",
            "/Keywords",
            "/Creator",
            "/Producer",
            "/CreationDate",
            "/ModDate",
            "/Trapped"
        ];

        foreach (var key in keysToRemove)
            info.Elements.Remove(key);

        var now = DateTime.UtcNow;
        info.Elements.SetString("/Title", $"Hợp đồng {First(contract.ContractNumber, contract.Id.ToString("N"))}");
        info.Elements.SetString("/Author", "HTX586CONTRACT");
        info.Elements.SetString("/Subject", "Hợp đồng vận chuyển hành khách");
        info.Elements.SetString("/Keywords", "hợp đồng, vận chuyển hành khách, HTX 586");
        info.Elements.SetString("/Creator", "HTX586CONTRACT");
        info.Elements.SetString("/Producer", "HTX586CONTRACT PDF Renderer");
        info.Elements.SetDateTime("/CreationDate", now);
        info.Elements.SetDateTime("/ModDate", now);
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

    private static double ResolveAlignedX(PdfTextFieldLayout field, double renderedWidth)
    {
        if (field.Alignment.Equals("Center", StringComparison.OrdinalIgnoreCase))
            return field.X + Math.Max(0d, (field.Width - renderedWidth) / 2d);

        if (field.Alignment.Equals("Right", StringComparison.OrdinalIgnoreCase))
            return field.X + Math.Max(0d, field.Width - renderedWidth);

        return field.X;
    }

    private static double ResolveAlignedY(PdfTextFieldLayout field, double renderedHeight)
    {
        if (field.VerticalAlignment.Equals("Top", StringComparison.OrdinalIgnoreCase))
            return field.Y;

        if (field.VerticalAlignment.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
            return field.Y + Math.Max(0d, field.Height - renderedHeight);

        return field.Y + Math.Max(0d, (field.Height - renderedHeight) / 2d);
    }

    private static void DrawContainedImage(
        XGraphics graphics,
        XImage image,
        PdfImageFieldLayout field)
    {
        var ratio = Math.Min(field.Width / image.PointWidth, field.Height / image.PointHeight);
        var width = image.PointWidth * ratio;
        var height = image.PointHeight * ratio;
        var x = field.X + (field.Width - width) / 2d;
        var y = field.Y + (field.Height - height) / 2d;
        graphics.DrawImage(image, x, y, width, height);
    }

    private Dictionary<string, string> BuildTextValues(Contract contract)
    {
        var company = contract.CompanyProfile;
        var customer = contract.Customer;
        var vehicle = contract.Vehicle;
        var driver = contract.Driver;
        var contractDate = VietnamTime(contract.CreatedAt);
        var passengerCount = contract.Passengers.Count(x => !string.IsNullOrWhiteSpace(x.FullName));

        var companyName = First(company?.CompanyName, contract.CompanyNameSnapshot, "...");
        var customerName = First(
            customer?.OrganizationName,
            customer?.FullName,
            contract.CustomerNameSnapshot,
            "...");
        var customerRepresentative = First(customer?.FullName, contract.CustomerNameSnapshot, "...");
        var ownerName = First(vehicle?.OwnerName, contract.VehicleOwnerNameSnapshot, "...");
        var driverName = First(driver?.FullName, contract.DriverNameSnapshot, "...");
        var vehicleBrandModel = First(
            string.Join(" ", new[] { vehicle?.Brand, vehicle?.Model }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            contract.VehicleBrandSnapshot,
            "...");

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CONTRACT_NUMBER"] = First(contract.ContractNumber, "..."),
            ["CONTRACT_TIME"] = contractDate.ToString("HH 'giờ' mm 'phút'", CultureInfo.GetCultureInfo("vi-VN")),
            ["CONTRACT_DAY"] = contractDate.ToString("dd"),
            ["CONTRACT_MONTH"] = contractDate.ToString("MM"),
            ["CONTRACT_YEAR"] = contractDate.ToString("yyyy"),
            ["CONTRACT_DATE"] = contractDate.ToString("dd 'tháng' MM 'năm' yyyy", CultureInfo.GetCultureInfo("vi-VN")),
            ["PASSENGER_LIST_SUBTITLE"] =
                $"(Kèm theo hợp đồng vận chuyển số {First(contract.ContractNumber, "...")}/HĐVC-HTX " +
                $"ngày {contractDate:dd} tháng {contractDate:MM} năm {contractDate:yyyy})",

            ["COMPANY_NAME"] = companyName,
            ["COMPANY_OFFICE_NAME"] = First(company?.BranchName, companyName),
            ["COMPANY_TAX_CODE"] = First(company?.TaxCode, contract.CompanyTaxCodeSnapshot, "..."),
            ["COMPANY_LICENSE"] = First(company?.BusinessLicenseNumber, "..."),
            ["COMPANY_ADDRESS"] = First(company?.Address, contract.CompanyAddressSnapshot, "..."),
            ["COMPANY_PHONE"] = First(company?.PhoneNumber, "..."),
            ["COMPANY_REPRESENTATIVE"] = First(
                company?.RepresentativeName,
                contract.CompanyRepresentativeSnapshot,
                "..."),
            ["COMPANY_REP_CITIZEN_ID"] = First(company?.RepresentativeCitizenId, "..."),
            ["COMPANY_REP_ISSUED_DATE"] = FormatDateOnly(company?.RepresentativeCitizenIdIssuedDate),
            ["COMPANY_REP_ISSUED_PLACE"] = First(company?.RepresentativeCitizenIdIssuedPlace, "..."),
            ["COMPANY_REP_POSITION"] = First(
                company?.RepresentativePosition,
                contract.CompanyRepresentativePositionSnapshot,
                "..."),

            ["OWNER_NAME"] = ownerName,
            ["OWNER_CITIZEN_ID"] = First(
                vehicle?.OwnerCitizenId,
                contract.VehicleOwnerCitizenIdSnapshot,
                "..."),
            ["OWNER_ISSUED_DATE"] = FormatDateOnly(vehicle?.OwnerCitizenIdIssuedDate),
            ["OWNER_ISSUED_PLACE"] = First(vehicle?.OwnerCitizenIdIssuedPlace, "..."),

            ["CUSTOMER_NAME"] = customerName,
            ["CUSTOMER_TAX_CODE"] = First(customer?.TaxCode, "..."),
            ["CUSTOMER_PHONE"] = First(customer?.PhoneNumber, contract.CustomerPhoneSnapshot, "..."),
            ["CUSTOMER_ADDRESS"] = First(customer?.Address, contract.CustomerAddressSnapshot, "..."),
            ["CUSTOMER_CITIZEN_ID"] = First(customer?.CitizenId, contract.CustomerCitizenIdSnapshot, "..."),
            ["CUSTOMER_ISSUED_DATE"] = FormatDateOnly(customer?.CitizenIdIssuedDate),
            ["CUSTOMER_ISSUED_PLACE"] = First(customer?.CitizenIdIssuedPlace, "..."),
            ["CUSTOMER_REPRESENTATIVE"] = customerRepresentative,

            ["VEHICLE_PLATE"] = First(vehicle?.PlateNumber, contract.VehiclePlateSnapshot, "..."),
            ["VEHICLE_BRAND_MODEL"] = vehicleBrandModel,
            ["SEAT_COUNT"] = vehicle?.SeatCount?.ToString(CultureInfo.InvariantCulture) ?? "...",
            ["PASSENGER_COUNT"] = passengerCount.ToString(CultureInfo.InvariantCulture),
            ["PASSENGER_COUNT_2D"] = passengerCount.ToString("00", CultureInfo.InvariantCulture),

            ["DRIVER_NAME"] = driverName,
            ["DRIVER_LICENSE_CLASS"] = First(
                driver?.DriverLicenseClass,
                contract.DriverLicenseClassSnapshot,
                "..."),
            ["SECOND_DRIVER_NAME"] = First(contract.SecondDriverName, "Không có"),
            ["SECOND_DRIVER_LICENSE_CLASS"] = First(contract.SecondDriverLicenseClass, "-"),

            ["PICKUP_DATETIME_LOCATION"] = BuildDateTimeLocation(contract.StartTime, contract.PickupLocation),
            ["DROPOFF_DATETIME_LOCATION"] = BuildDateTimeLocation(contract.EndTime, contract.DropoffLocation),
            ["ROUTE_DESCRIPTION"] = First(contract.RouteDescription, "..."),
            ["TOTAL_KILOMETERS"] = contract.TotalKilometers?.ToString("N1", CultureInfo.GetCultureInfo("vi-VN")) ?? "...",
            ["CONTRACT_VALUE"] = contract.ContractValue?.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) ?? "...",
            ["CONTRACT_VALUE_WORDS"] = Capitalize(NumberToVietnameseWords(contract.ContractValue)) + " đồng",
            ["PAYMENT_METHOD"] = First(contract.PaymentMethod, "..."),
            ["PAYMENT_TIME"] = First(contract.PaymentTime, "..."),
            ["CONTRACT_NOTE"] = First(contract.Note, "Không có"),

            ["SIG_OFFICE_NAME"] = First(company?.RepresentativeName, contract.CompanyRepresentativeSnapshot, "..."),
            ["SIG_OWNER_NAME"] = ownerName,
            ["SIG_CUSTOMER_NAME"] = SignatureName(contract, SignatureParty.Customer, customerRepresentative),
            ["SIG_DRIVER_NAME"] = driverName,
            ["VERIFY_CODE"] = ShortHash(contract.ContractHash ?? contract.Id.ToString("N"))
        };

        var passengers = contract.Passengers
            .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Take(20)
            .ToList();

        for (var index = 1; index <= 20; index++)
        {
            var passenger = index <= passengers.Count ? passengers[index - 1] : null;
            values[$"P{index:00}_NAME"] = passenger?.FullName?.Trim() ?? string.Empty;
            values[$"P{index:00}_BIRTH_YEAR"] = passenger?.BirthYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            values[$"P{index:00}_NOTE"] = passenger?.Note?.Trim() ?? string.Empty;
        }

        return values;
    }

    private Dictionary<string, string?> BuildImageValues(Contract contract)
        => new(StringComparer.Ordinal)
        {
            // Chữ ký cố định lấy từ danh mục Company/Vehicle/User.
            ["SIG_OFFICE"] = StoredSignaturePath(contract.CompanyProfile?.RepresentativeSignatureFileUrl),
            ["SIG_OWNER"] = StoredSignaturePath(contract.Vehicle?.OwnerSignatureFileUrl),
            ["SIG_DRIVER"] = StoredSignaturePath(contract.Driver?.DriverSignatureFileUrl),

            // Chữ ký khách hàng vẫn là chữ ký theo từng hợp đồng.
            ["SIG_CUSTOMER"] = ContractSignaturePath(contract, SignatureParty.Customer),
            ["SIG_CUSTOMER_2"] = ContractSignaturePath(contract, SignatureParty.Customer)
        };

    private string? ContractSignaturePath(Contract contract, SignatureParty party)
        => StoredSignaturePath(contract.Signatures.FirstOrDefault(x => x.Party == party)?.SignatureFileUrl);

    private string? StoredSignaturePath(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return null;

        return storage.ToPhysicalPath(relativeUrl);
    }

    private static string SignatureName(Contract contract, SignatureParty party, string fallback)
        => First(contract.Signatures.FirstOrDefault(x => x.Party == party)?.SignerName, fallback, "...");

    private static string BuildDateTimeLocation(DateTime? value, string? location)
    {
        var dateTime = value is null
            ? "..."
            : VietnamTime(value.Value).ToString("dd/MM/yyyy HH:mm");
        return $"{dateTime} - {First(location, "...")}";
    }

    private static DateTime VietnamTime(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value.AddHours(7) : value;

    private static string FormatDateOnly(DateTime? value)
        => value?.ToString("dd/MM/yyyy") ?? "...";

    private static string First(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private static string Capitalize(string value)
        => string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpper(value[0], CultureInfo.GetCultureInfo("vi-VN")) + value[1..];

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
                parts.Add(string.IsNullOrEmpty(units[index]) ? words : $"{words} {units[index]}");
        }

        return string.Join(" ", parts);
    }

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
            if (ones == 1) parts.Add("mốt");
            else if (ones == 4) parts.Add("tư");
            else if (ones == 5) parts.Add("lăm");
            else if (ones > 0) parts.Add(digit[ones]);
        }
        else if (tens == 1)
        {
            parts.Add("mười");
            if (ones == 5) parts.Add("lăm");
            else if (ones > 0) parts.Add(digit[ones]);
        }
        else if (ones > 0)
        {
            parts.Add(digit[ones]);
        }

        return string.Join(" ", parts);
    }

    private static string ShortHash(string value)
        => value.Length <= 16 ? value : value[..16];

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Không che lỗi gốc.
        }
    }

    private sealed class PdfTemplateLayout
    {
        public int Version { get; set; }
        public string TemplateFile { get; set; } = string.Empty;
        public string FontFamily { get; set; } = "Times New Roman";
        public List<string> FallbackFontFamilies { get; set; } = [];
        public List<PdfTextFieldLayout> TextFields { get; set; } = [];
        public List<PdfImageFieldLayout> ImageFields { get; set; } = [];
    }

    private sealed class PdfTextFieldLayout
    {
        public string Key { get; set; } = string.Empty;
        public int Page { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public float FontSize { get; set; } = 11;
        public float MinFontSize { get; set; } = 8.0f;
        public int MaxLines { get; set; } = 1;
        public string Alignment { get; set; } = "Left";
        public string VerticalAlignment { get; set; } = "Center";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Uppercase { get; set; }
        public bool ClearBackground { get; set; }
    }

    private sealed class PdfImageFieldLayout
    {
        public string Key { get; set; } = string.Empty;
        public int Page { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Fit { get; set; } = "Contain";
    }

    private static class TextPngRenderer
    {
        private const float Scale = 4f;
        private const float PaddingX = 1.0f;
        private const float PaddingY = 0.5f;

        public static RenderedTextImage Render(
            string value,
            PdfTextFieldLayout field,
            string primaryFontFamily,
            IReadOnlyList<string> fallbackFontFamilies)
        {
            var maxWidth = Math.Max(1f, (float)(field.Width * Scale) - (PaddingX * 2f * Scale));
            var maxHeight = Math.Max(1f, (float)(field.Height * Scale) - (PaddingY * 2f * Scale));

            using var typeface = ResolveTypeface(
                primaryFontFamily,
                fallbackFontFamilies,
                field.Bold,
                field.Italic);
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Typeface = typeface,
                TextSize = field.FontSize * Scale,
                TextAlign = SKTextAlign.Left,
                IsStroke = false,
                SubpixelText = true
            };

            var fontSize = field.FontSize;
            List<string> lines;
            float widest;
            float blockHeight;
            SKFontMetrics finalMetrics;
            float finalLineHeight;

            while (true)
            {
                paint.TextSize = fontSize * Scale;
                lines = WrapText(value, paint, maxWidth, Math.Max(1, field.MaxLines));
                finalMetrics = paint.FontMetrics;
                finalLineHeight = (finalMetrics.Descent - finalMetrics.Ascent + finalMetrics.Leading) * 1.02f;
                blockHeight = finalLineHeight * lines.Count;
                widest = lines.Count == 0 ? 0 : lines.Max(line => paint.MeasureText(line));

                if ((widest <= maxWidth && blockHeight <= maxHeight && lines.Count <= field.MaxLines) ||
                    fontSize <= field.MinFontSize)
                    break;

                fontSize = Math.Max(field.MinFontSize, fontSize - 0.25f);
            }

            paint.TextSize = fontSize * Scale;
            lines = WrapText(value, paint, maxWidth, Math.Max(1, field.MaxLines));
            finalMetrics = paint.FontMetrics;
            finalLineHeight = (finalMetrics.Descent - finalMetrics.Ascent + finalMetrics.Leading) * 1.02f;
            blockHeight = finalLineHeight * lines.Count;
            widest = lines.Count == 0 ? 0 : lines.Max(line => paint.MeasureText(line));

            var pixelWidth = Math.Max(1, (int)Math.Ceiling(widest + (PaddingX * 2f * Scale)));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(blockHeight + (PaddingY * 2f * Scale)));

            using var bitmap = new SKBitmap(
                pixelWidth,
                pixelHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            var baseline = (PaddingY * Scale) - finalMetrics.Ascent;
            var drawX = PaddingX * Scale;
            foreach (var line in lines)
            {
                canvas.DrawText(line, drawX, baseline, paint);
                baseline += finalLineHeight;
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return new RenderedTextImage(data.ToArray(), pixelWidth / Scale, pixelHeight / Scale);
        }

        private static SKTypeface ResolveTypeface(
            string primary,
            IEnumerable<string> fallbacks,
            bool bold,
            bool italic)
        {
            var style = (bold, italic) switch
            {
                (true, true) => SKFontStyle.BoldItalic,
                (true, false) => SKFontStyle.Bold,
                (false, true) => SKFontStyle.Italic,
                _ => SKFontStyle.Normal
            };
            foreach (var family in new[] { primary }.Concat(fallbacks).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var typeface = SKTypeface.FromFamilyName(family, style);
                if (typeface is not null)
                    return typeface;
            }

            return SKTypeface.FromFamilyName("sans-serif", style);
        }

        private static List<string> WrapText(
            string value,
            SKPaint paint,
            float maxWidth,
            int maxLines)
        {
            var normalized = string.Join(" ", value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (string.IsNullOrWhiteSpace(normalized))
                return [];

            if (maxLines <= 1)
                return [FitSingleLine(normalized, paint, maxWidth)];

            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = string.Empty;

            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (paint.MeasureText(candidate) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                    lines.Add(current);

                current = word;
                if (lines.Count == maxLines - 1)
                    break;
            }

            if (!string.IsNullOrEmpty(current) && lines.Count < maxLines)
                lines.Add(current);

            if (lines.Count > 0)
                lines[^1] = FitSingleLine(lines[^1], paint, maxWidth);

            return lines;
        }

        private static string FitSingleLine(string value, SKPaint paint, float maxWidth)
        {
            if (paint.MeasureText(value) <= maxWidth)
                return value;

            const string ellipsis = "...";
            var low = 0;
            var high = value.Length;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                var candidate = value[..middle].TrimEnd() + ellipsis;
                if (paint.MeasureText(candidate) <= maxWidth)
                    low = middle;
                else
                    high = middle - 1;
            }

            return low <= 0 ? ellipsis : value[..low].TrimEnd() + ellipsis;
        }

        public sealed record RenderedTextImage(byte[] PngBytes, double WidthPoints, double HeightPoints);
    }
}
