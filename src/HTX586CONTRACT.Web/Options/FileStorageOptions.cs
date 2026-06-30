namespace HTX586CONTRACT.Web.Options;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Provider hiện tại là LocalFileSystem. Thuộc tính này được giữ để sau này mở rộng sang NAS/S3/MinIO/Azure Blob.
    /// </summary>
    public string Provider { get; set; } = "LocalFileSystem";

    /// <summary>
    /// Thư mục vật lý chứa toàn bộ file upload. Có thể là đường dẫn tuyệt đối hoặc tương đối theo ContentRootPath.
    /// Không nên đặt trong wwwroot để tránh bị xóa khi deploy lại.
    /// </summary>
    public string UploadRootPath { get; set; } = "../HTX586CONTRACT_Data/uploads";

    /// <summary>
    /// URL public dùng để mở ảnh chữ ký/PDF đã lưu. Giữ mặc định /uploads để tương thích dữ liệu cũ trong database.
    /// </summary>
    public string PublicRequestPath { get; set; } = "/uploads";

    /// <summary>
    /// Bật phục vụ file upload bằng static file middleware.
    /// </summary>
    public bool ServeUploadsAsStaticFiles { get; set; } = true;
}
