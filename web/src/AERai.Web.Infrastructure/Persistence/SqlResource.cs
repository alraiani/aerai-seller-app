namespace AERai.Web.Infrastructure.Persistence;

/// <summary>
/// Loads versioned SQL scripts (views, procedures, functions) embedded under <c>Persistence/Sql</c>.
/// </summary>
/// <remarks>
/// Scripts live in folders named after the migration that applies them. Once a migration has been
/// applied anywhere, its folder is immutable: change a view by adding a new migration and a new
/// folder, so replaying old migrations always produces the same schema.
/// </remarks>
public static class SqlResource
{
    /// <summary>
    /// Reads an embedded script.
    /// </summary>
    /// <param name="migrationFolder">Folder name under <c>Persistence/Sql</c>, e.g. <c>V001_Initial</c>.</param>
    /// <param name="fileName">Script file name, e.g. <c>rpt.vw_OrderSummary.sql</c>.</param>
    /// <returns>The script text.</returns>
    /// <exception cref="InvalidOperationException">The script is not embedded in the assembly.</exception>
    public static string Read(string migrationFolder, string fileName)
    {
        var assembly = typeof(SqlResource).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Persistence.Sql.{migrationFolder}.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
