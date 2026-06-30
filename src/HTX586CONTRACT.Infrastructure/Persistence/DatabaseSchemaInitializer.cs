using Microsoft.EntityFrameworkCore;

namespace HTX586CONTRACT.Infrastructure.Persistence;

public static class DatabaseSchemaInitializer
{
    public static async Task ApplyAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "database", "20260626_ver6.sql");
        if (!File.Exists(path))
            throw new FileNotFoundException("Không tìm thấy script nâng cấp database ver6.", path);
        var sql = await File.ReadAllTextAsync(path, ct);
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
