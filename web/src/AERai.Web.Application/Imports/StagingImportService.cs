using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Default <see cref="IStagingImportService"/>: checks file limits, lands the file in raw blob
/// storage, then parses it from there with the source's <see cref="IStagingRowMapper"/> and
/// persists the staging batch.
/// </summary>
/// <remarks>
/// Staging always reads from the raw store — never from the upload stream — so an uploaded file and
/// a re-staged or machine-delivered file take exactly the same path into <c>stg</c>.
/// </remarks>
public sealed partial class StagingImportService : IStagingImportService
{
    /// <summary>
    /// Error returned when a file has a header but no data rows. Exposed so automated ingestion can
    /// treat an empty Amazon report as "no data" rather than a failure.
    /// </summary>
    public const string NoDataRowsError = "The file has a header row but no data rows.";

    private readonly Dictionary<ImportSource, IStagingRowMapper> _mappers;
    private readonly IRawFileStore _rawFiles;
    private readonly IStagingRepository _repository;
    private readonly IImportBatchQueries _batches;
    private readonly ImportOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StagingImportService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="mappers">One mapper per supported <see cref="ImportSource"/>.</param>
    /// <param name="rawFiles">Raw landing-zone storage.</param>
    /// <param name="repository">Staging persistence.</param>
    /// <param name="batches">Batch queries (to find the raw file of an earlier batch).</param>
    /// <param name="options">Upload limits.</param>
    /// <param name="timeProvider">Clock used to stamp receive times.</param>
    /// <param name="logger">Logger.</param>
    public StagingImportService(
        IEnumerable<IStagingRowMapper> mappers,
        IRawFileStore rawFiles,
        IStagingRepository repository,
        IImportBatchQueries batches,
        IOptions<ImportOptions> options,
        TimeProvider timeProvider,
        ILogger<StagingImportService> logger)
    {
        ArgumentNullException.ThrowIfNull(mappers);
        ArgumentNullException.ThrowIfNull(options);

        _mappers = mappers.ToDictionary(m => m.Source);
        _rawFiles = rawFiles;
        _repository = repository;
        _batches = batches;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<ImportReceipt>> ImportAsync(ImportFileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = Validate(command);
        if (validation.IsFailure)
        {
            return Result.Failure<ImportReceipt>(validation.Error);
        }

        var displayName = Path.GetFileName(command.FileName);
        var path = RawFilePaths.Build(command.Source, _timeProvider.GetUtcNow(), Guid.NewGuid(), displayName);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = command.Source.ToString(),
            ["marketplaceid"] = command.MarketplaceId,
            ["uploadedby"] = ToAsciiMetadata(command.UploadedBy),
            ["originalname"] = ToAsciiMetadata(displayName),
        };

        var sha256 = await _rawFiles.SaveAsync(path, command.Content, metadata, cancellationToken).ConfigureAwait(false);
        LogLanded(path, command.Source, command.UploadedBy);

        return await StageRawFileAsync(
            new StageRawFileCommand(command.Source, command.MarketplaceId, path, sha256, displayName, command.UploadedBy),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<ImportReceipt>> StageRawFileAsync(StageRawFileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_mappers.TryGetValue(command.Source, out var mapper))
        {
            return Result.Failure<ImportReceipt>($"Imports of type '{command.Source}' are not supported.");
        }

        var content = await _rawFiles.OpenReadAsync(command.RawFilePath, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return Result.Failure<ImportReceipt>($"Raw file '{command.RawFilePath}' was not found in storage.");
        }

        Result<ParsedFile> parsed;
        await using (content.ConfigureAwait(false))
        {
            // detectEncodingFromByteOrderMarks handles UTF-8/UTF-16 BOMs that Excel and Seller Central emit.
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            parsed = await DelimitedTextParser.ParseAsync(reader, _options.MaxRows, cancellationToken).ConfigureAwait(false);
        }

        if (parsed.IsFailure)
        {
            return Result.Failure<ImportReceipt>(parsed.Error);
        }

        var missing = mapper.RequiredColumns.Except(parsed.Value.Headers, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            return Result.Failure<ImportReceipt>(
                $"The file is missing required {command.Source} column(s): {string.Join(", ", missing)}.");
        }

        if (parsed.Value.Records.Count == 0)
        {
            return Result.Failure<ImportReceipt>(NoDataRowsError);
        }

        var batch = new ImportBatch
        {
            Source = command.Source,
            MarketplaceId = command.MarketplaceId,
            FileName = command.DisplayFileName,
            RawFilePath = command.RawFilePath,
            RawFileSha256 = command.RawFileSha256,
            UploadedBy = command.RequestedBy,
            UploadedAt = _timeProvider.GetUtcNow(),
            Status = ImportBatchStatus.Received,
            RowCount = parsed.Value.Records.Count,
        };

        foreach (var record in parsed.Value.Records)
        {
            mapper.Append(batch, record);
        }

        var batchId = await _repository.AddBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        LogStaged(batchId, command.Source, batch.RowCount, command.RawFilePath);

        return Result.Success(new ImportReceipt(batchId, batch.RowCount));
    }

    /// <inheritdoc/>
    public async Task<Result<ImportReceipt>> RestageAsync(long batchId, string requestedBy, CancellationToken cancellationToken)
    {
        var batch = await _batches.GetAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<ImportReceipt>($"Import batch {batchId} was not found.");
        }

        if (batch.RawFilePath is null || batch.RawFileSha256 is null)
        {
            return Result.Failure<ImportReceipt>($"Batch {batchId} has no stored raw file, so it cannot be re-staged.");
        }

        return await StageRawFileAsync(
            new StageRawFileCommand(batch.Source, batch.MarketplaceId, batch.RawFilePath, batch.RawFileSha256, batch.FileName, requestedBy),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks the cheap, metadata-only rules before anything is stored.
    /// </summary>
    private Result Validate(ImportFileCommand command)
    {
        if (!_mappers.ContainsKey(command.Source))
        {
            return Result.Failure($"Imports of type '{command.Source}' are not supported.");
        }

        if (command.Length <= 0)
        {
            return Result.Failure("The file is empty.");
        }

        if (command.Length > _options.MaxFileBytes)
        {
            return Result.Failure($"The file is larger than the {_options.MaxFileBytes / (1024 * 1024)} MB limit.");
        }

        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.Ordinal))
        {
            return Result.Failure($"Only {string.Join(", ", _options.AllowedExtensions)} files can be imported.");
        }

        return Result.Success();
    }

    /// <summary>Blob metadata values must be ASCII; anything else is replaced with '?'.</summary>
    private static string ToAsciiMetadata(string value) =>
        new(value.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());

    [LoggerMessage(Level = LogLevel.Information, Message = "Landed raw {Source} file at {RawFilePath} for {UploadedBy}")]
    private partial void LogLanded(string rawFilePath, ImportSource source, string uploadedBy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Staged batch {BatchId} ({Source}, {RowCount} rows) from {RawFilePath}")]
    private partial void LogStaged(long batchId, ImportSource source, int rowCount, string rawFilePath);
}
