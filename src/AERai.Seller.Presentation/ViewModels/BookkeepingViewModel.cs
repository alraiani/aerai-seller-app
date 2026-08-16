using System.Collections.ObjectModel;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Bookkeeping;
using AERai.Seller.Application.Sync;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

public sealed record SettlementRow(
    string SettlementId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset? DepositDate,
    decimal TotalAmount,
    string Currency,
    bool IsExported);

public sealed record ExportRecordRow(
    string FileName,
    DateTimeOffset GeneratedAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal NetTotal,
    string Currency);

/// <summary>
/// Settlement list, per-settlement export preview (built via IBookkeepingExportService), and
/// generated-files list — the primary Bookkeeping page. Category-to-account mapping itself lives on
/// BookkeepingAccountMappingViewModel/page.
/// </summary>
public partial class BookkeepingViewModel : ObservableObject
{
    private readonly ISettlementRepository _settlementRepository;
    private readonly IBookkeepingExportRepository _exportRepository;
    private readonly ISettlementSyncService _settlementSyncService;
    private readonly IBookkeepingExportService _exportService;
    private readonly IMessenger _messenger;
    private readonly ILogger<BookkeepingViewModel> _logger;

    public BookkeepingViewModel(
        ISettlementRepository settlementRepository,
        IBookkeepingExportRepository exportRepository,
        ISettlementSyncService settlementSyncService,
        IBookkeepingExportService exportService,
        IMessenger messenger,
        ILogger<BookkeepingViewModel> logger)
    {
        _settlementRepository = settlementRepository;
        _exportRepository = exportRepository;
        _settlementSyncService = settlementSyncService;
        _exportService = exportService;
        _messenger = messenger;
        _logger = logger;
    }

    public ObservableCollection<SettlementRow> Settlements { get; } = [];
    public ObservableCollection<SettlementSummaryRow> PreviewRows { get; } = [];
    public ObservableCollection<ExportRecordRow> ExportedFiles { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string? _syncStatusText;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private SettlementRow? _selectedSettlement;

    [ObservableProperty]
    private decimal _previewNetTotal;

    [ObservableProperty]
    private string? _previewCurrency;

    [ObservableProperty]
    private bool _hasUnmappedCategories;

    [ObservableProperty]
    private int _unmappedCategoryCount;

    [ObservableProperty]
    private bool _isExporting;

    partial void OnSelectedSettlementChanged(SettlementRow? value)
    {
        ExportCommand.NotifyCanExecuteChanged();
        _ = LoadPreviewAsync(value);
    }

    partial void OnHasUnmappedCategoriesChanged(bool value) => ExportCommand.NotifyCanExecuteChanged();

    partial void OnIsExportingChanged(bool value) => ExportCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            LastError = null;
            var settlements = await _settlementRepository.GetAllAsync();
            var exports = await _exportRepository.GetAllAsync();
            var exportedIds = exports.Select(e => e.SettlementId).ToHashSet();

            var rows = settlements
                .Select(s => new SettlementRow(
                    s.SettlementId, s.FinancialEventGroupStart, s.FinancialEventGroupEnd,
                    s.DepositDate, s.TotalAmount, s.Currency, exportedIds.Contains(s.SettlementId)))
                .OrderByDescending(r => r.PeriodEnd)
                .ToList();

            Settlements.Clear();
            foreach (var row in rows)
            {
                Settlements.Add(row);
            }

            ExportedFiles.Clear();
            foreach (var record in exports.OrderByDescending(e => e.GeneratedAt))
            {
                ExportedFiles.Add(new ExportRecordRow(
                    record.FileName, record.GeneratedAt, record.PeriodStart, record.PeriodEnd, record.NetTotal, record.Currency));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settlements");
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Failed to load settlements: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SyncNowAsync()
    {
        if (IsSyncing)
        {
            return;
        }

        IsSyncing = true;
        LastError = null;
        var progress = new Progress<string>(status =>
        {
            SyncStatusText = status;
            _messenger.Send(new StatusMessage(status, IsBusy: true));
        });
        try
        {
            await _settlementSyncService.SyncAsync(progress);
            SyncStatusText = "Refreshing settlements...";
            await LoadAsync();
            _messenger.Send(new StatusMessage("Settlement sync complete.", StatusSeverity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Settlement sync failed");
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Sync failed: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private async Task LoadPreviewAsync(SettlementRow? settlement)
    {
        PreviewRows.Clear();
        HasUnmappedCategories = false;
        UnmappedCategoryCount = 0;
        PreviewNetTotal = 0;
        PreviewCurrency = null;

        if (settlement is null)
        {
            return;
        }

        try
        {
            var summary = await _exportService.PreviewAsync(settlement.SettlementId);
            foreach (var row in summary.Rows)
            {
                PreviewRows.Add(row);
            }

            PreviewNetTotal = summary.NetTotal;
            PreviewCurrency = summary.Currency;
            HasUnmappedCategories = summary.HasUnmappedCategories;
            UnmappedCategoryCount = summary.UnmappedCategories.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build settlement preview for {SettlementId}", settlement.SettlementId);
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Failed to preview settlement: {ex.Message}", StatusSeverity.Error));
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    public async Task ExportAsync()
    {
        if (SelectedSettlement is null)
        {
            return;
        }

        IsExporting = true;
        try
        {
            await _exportService.ExportAsync(SelectedSettlement.SettlementId);
            _messenger.Send(new StatusMessage("Export generated.", StatusSeverity.Success));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed for {SettlementId}", SelectedSettlement.SettlementId);
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Export failed: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsExporting = false;
        }
    }

    private bool CanExport() => SelectedSettlement is not null && !HasUnmappedCategories && !IsExporting;
}
