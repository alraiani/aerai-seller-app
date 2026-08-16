using System.Collections.ObjectModel;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

/// <summary>One editable row: a raw Amazon settlement category and the QuickBooks account name the user assigns it.</summary>
public sealed partial class AccountMappingRow : ObservableObject
{
    public required string AmountType { get; init; }
    public required string AmountDescription { get; init; }

    [ObservableProperty]
    private string? _quickBooksAccountName;

    public bool IsUnmapped => string.IsNullOrWhiteSpace(QuickBooksAccountName);

    partial void OnQuickBooksAccountNameChanged(string? value) => OnPropertyChanged(nameof(IsUnmapped));
}

/// <summary>
/// Editor for the Amazon-category -> QuickBooks-account mapping plus the singleton bookkeeping
/// settings (deposit account, export folder override). Rows are every (AmountType, AmountDescription)
/// pair seen in synced settlement line items, left-joined against existing mappings — a brand-new
/// Amazon category just shows up here as an unmapped row.
/// </summary>
public partial class BookkeepingAccountMappingViewModel : ObservableObject
{
    private readonly ISettlementRepository _settlementRepository;
    private readonly IBookkeepingAccountMappingRepository _mappingRepository;
    private readonly IBookkeepingSettingsRepository _settingsRepository;
    private readonly IMessenger _messenger;
    private readonly ILogger<BookkeepingAccountMappingViewModel> _logger;

    public BookkeepingAccountMappingViewModel(
        ISettlementRepository settlementRepository,
        IBookkeepingAccountMappingRepository mappingRepository,
        IBookkeepingSettingsRepository settingsRepository,
        IMessenger messenger,
        ILogger<BookkeepingAccountMappingViewModel> logger)
    {
        _settlementRepository = settlementRepository;
        _mappingRepository = mappingRepository;
        _settingsRepository = settingsRepository;
        _messenger = messenger;
        _logger = logger;
    }

    public ObservableCollection<AccountMappingRow> Rows { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _depositAccountName;

    [ObservableProperty]
    private string? _exportFolderPath;

    [ObservableProperty]
    private string? _saveStatusMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            SaveStatusMessage = null;
            var categories = await _settlementRepository.GetDistinctCategoriesAsync();
            var mappings = await _mappingRepository.GetAllAsync();
            var mappingLookup = mappings.ToDictionary(m => (m.AmountType, m.AmountDescription), m => m.QuickBooksAccountName);

            var rows = categories
                .Select(c => new AccountMappingRow
                {
                    AmountType = c.AmountType,
                    AmountDescription = c.AmountDescription,
                    QuickBooksAccountName = mappingLookup.GetValueOrDefault((c.AmountType, c.AmountDescription)),
                })
                .OrderBy(r => r.AmountType, StringComparer.Ordinal)
                .ThenBy(r => r.AmountDescription, StringComparer.Ordinal)
                .ToList();

            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
            }

            var settings = await _settingsRepository.GetAsync();
            DepositAccountName = settings.DepositAccountName;
            ExportFolderPath = settings.ExportFolderPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load account mapping data");
            _messenger.Send(new StatusMessage($"Failed to load account mapping: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            var updatedAt = DateTimeOffset.UtcNow;
            foreach (var row in Rows)
            {
                await _mappingRepository.UpsertAsync(new BookkeepingAccountMapping
                {
                    AmountType = row.AmountType,
                    AmountDescription = row.AmountDescription,
                    QuickBooksAccountName = string.IsNullOrWhiteSpace(row.QuickBooksAccountName) ? null : row.QuickBooksAccountName,
                    UpdatedAt = updatedAt,
                });
            }

            await _settingsRepository.SaveAsync(new BookkeepingSettings
            {
                DepositAccountName = DepositAccountName,
                ExportFolderPath = ExportFolderPath,
            });

            SaveStatusMessage = "Saved.";
            _messenger.Send(new StatusMessage("Account mapping saved.", StatusSeverity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save account mapping");
            SaveStatusMessage = $"Failed to save: {ex.Message}";
            _messenger.Send(new StatusMessage($"Failed to save account mapping: {ex.Message}", StatusSeverity.Error));
        }
    }
}
