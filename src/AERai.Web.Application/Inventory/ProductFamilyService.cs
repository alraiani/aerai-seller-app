using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IProductFamilyService"/>.</summary>
/// <param name="repository">Family persistence.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ProductFamilyService(IProductFamilyRepository repository, ILogger<ProductFamilyService> logger) : IProductFamilyService
{
    /// <summary>Most SKUs changed in one bulk assignment (a page of the Inventory list is far fewer).</summary>
    public const int MaxSkusPerAssignment = 500;

    /// <inheritdoc/>
    public Task<IReadOnlyList<FamilySummary>> ListAsync(CancellationToken cancellationToken) => repository.ListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<Result<string>> CreateAsync(string? name, CancellationToken cancellationToken)
    {
        var tidy = FamilyNames.Normalize(name);
        if (FamilyNames.Validate(tidy) is { } error)
        {
            return Result.Failure<string>(error);
        }

        if (await repository.CreateAsync(tidy!, cancellationToken).ConfigureAwait(false) is null) // Non-null: validated above.
        {
            return Result.Failure<string>($"A family called \"{tidy}\" already exists.");
        }

        LogCreated(tidy!);
        return Result.Success(tidy!);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> RenameAsync(int id, string? name, CancellationToken cancellationToken)
    {
        var tidy = FamilyNames.Normalize(name);
        if (FamilyNames.Validate(tidy) is { } error)
        {
            return Result.Failure<string>(error);
        }

        return await repository.RenameAsync(id, tidy!, cancellationToken).ConfigureAwait(false) switch // Non-null: validated above.
        {
            FamilyWriteOutcome.Saved => Result.Success(tidy!),
            FamilyWriteOutcome.Duplicate => Result.Failure<string>($"A family called \"{tidy}\" already exists."),
            _ => Result.Failure<string>("That family no longer exists."),
        };
    }

    /// <inheritdoc/>
    public async Task<Result<string>> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false) is not { } name)
        {
            return Result.Failure<string>("That family no longer exists.");
        }

        LogDeleted(name);
        return Result.Success(name);
    }

    /// <inheritdoc/>
    public async Task<Result<int>> AssignAsync(IReadOnlyCollection<string> skus, string? familyName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var distinct = skus.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
        {
            return Result.Failure<int>("Select at least one SKU.");
        }

        if (distinct.Count > MaxSkusPerAssignment)
        {
            return Result.Failure<int>($"Select at most {MaxSkusPerAssignment} SKUs at a time.");
        }

        int? familyId = null;
        if (FamilyNames.Normalize(familyName) is { } tidy)
        {
            if (FamilyNames.Validate(tidy) is { } error)
            {
                return Result.Failure<int>(error);
            }

            // A new name creates the family, so a list can be grouped in one step. If someone else
            // creates it at the same moment, the second lookup finds theirs.
            familyId = await repository.FindAsync(tidy, cancellationToken).ConfigureAwait(false)
                ?? await repository.CreateAsync(tidy, cancellationToken).ConfigureAwait(false)
                ?? await repository.FindAsync(tidy, cancellationToken).ConfigureAwait(false);
        }

        var updated = await repository.AssignAsync(distinct, familyId, cancellationToken).ConfigureAwait(false);
        LogAssigned(updated, familyName);
        return Result.Success(updated);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created product family {Family}")]
    private partial void LogCreated(string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted product family {Family}")]
    private partial void LogDeleted(string family);

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigned {Count} SKUs to family {Family}")]
    private partial void LogAssigned(int count, string? family);
}
