using AERai.Seller.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure;

/// <summary>
/// Writes generated export text (e.g. IIF content) to disk under %AppData%\AERaiSellerApp\BookkeepingExports,
/// or BookkeepingSettings.ExportFolderPath if the user has overridden it.
/// </summary>
public sealed class FileExportStore(IDbContextFactory<SellerDbContext> dbContextFactory) : IExportFileStore
{
    public static string GetDefaultExportFolder()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AERaiSellerApp", "BookkeepingExports");
        return appDataDir;
    }

    public async Task<string> SaveAsync(string fileName, string content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.BookkeepingSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        var folder = string.IsNullOrWhiteSpace(settings?.ExportFolderPath) ? GetDefaultExportFolder() : settings.ExportFolderPath;
        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, fileName);
        await File.WriteAllTextAsync(filePath, content, cancellationToken);
        return filePath;
    }
}
