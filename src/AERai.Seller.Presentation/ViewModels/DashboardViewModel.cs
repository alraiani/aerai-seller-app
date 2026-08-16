using System.Collections.ObjectModel;
using AERai.Seller.Application.Dashboard;
using AERai.Seller.Application.Sync;
using AERai.Seller.Domain;
using AERai.Seller.Presentation.Abstractions;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

/// <summary>
/// Backs the Dashboard page. Top widget is an orders-by-SKU summary for a user-selectable day
/// (defaults to today); later phases add more tiles (replenishment alerts, financial summary).
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardQueryService _dashboardQueryService;
    private readonly IInventorySyncService _inventorySyncService;
    private readonly IOrderSyncService _orderSyncService;
    private readonly IClipboardService _clipboardService;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DashboardViewModel> _logger;

    public DashboardViewModel(
        IDashboardQueryService dashboardQueryService,
        IInventorySyncService inventorySyncService,
        IOrderSyncService orderSyncService,
        IClipboardService clipboardService,
        IMessenger messenger,
        TimeProvider timeProvider,
        ILogger<DashboardViewModel> logger)
    {
        _dashboardQueryService = dashboardQueryService;
        _inventorySyncService = inventorySyncService;
        _orderSyncService = orderSyncService;
        _clipboardService = clipboardService;
        _messenger = messenger;
        _timeProvider = timeProvider;
        _logger = logger;
        // "Today" matches Amazon Seller Central's convention (Pacific Time), not UTC or local
        // time — see AmazonBusinessDay for why.
        _selectedDate = AmazonBusinessDay.TodayIn(timeProvider.GetUtcNow());
    }

    public ObservableCollection<SkuOrderSummary> OrdersBySku { get; } = [];
    public ObservableCollection<SyncStatusSummary> SyncStatuses { get; } = [];
    public ObservableCollection<DailySalesSummary> DailySales { get; } = [];

    [ObservableProperty]
    private DateOnly _selectedDate;

    [ObservableProperty]
    private int _totalUnitsSold;

    [ObservableProperty]
    private decimal _totalRevenue;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSyncing;

    /// <summary>Live step-by-step status while a sync is running (e.g. "Orders: fetching page 2..."), so a
    /// multi-minute background operation never looks like the app has stalled.</summary>
    [ObservableProperty]
    private string? _syncStatusText;

    /// <summary>When Sync Now last completed for the Orders job, shown as a standing label so it's
    /// always visible — not just while a sync is actively running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastOrderSyncLabel))]
    private DateTimeOffset? _lastOrderSyncAt;

    public string LastOrderSyncLabel => LastOrderSyncAt is { } at
        ? $"Last synced: {at.LocalDateTime:g}"
        : "Never synced";

    [ObservableProperty]
    private string? _lastError;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            LastError = null;
            var summaries = await _dashboardQueryService.GetOrdersBySkuAsync(SelectedDate);
            OrdersBySku.Clear();
            foreach (var summary in summaries)
            {
                OrdersBySku.Add(summary);
            }

            TotalUnitsSold = summaries.Sum(s => s.UnitsSold);
            TotalRevenue = summaries.Sum(s => s.Revenue);

            var dailySales = await _dashboardQueryService.GetDailySalesForLastNDaysAsync(SelectedDate, 7);
            DailySales.Clear();
            foreach (var day in dailySales)
            {
                DailySales.Add(day);
            }

            var statuses = await _dashboardQueryService.GetSyncStatusAsync();
            SyncStatuses.Clear();
            foreach (var status in statuses)
            {
                SyncStatuses.Add(status);
            }

            LastOrderSyncAt = statuses
                .FirstOrDefault(s => s.SyncJobName == OrderSyncService.SyncJobName)
                ?.LastSuccessfulSyncAt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard data");
            LastError = ex.Message;
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
        // Progress<T> captures the current (UI) SynchronizationContext, so reports from the
        // background sync work marshal back to the UI thread automatically.
        var progress = new Progress<string>(status =>
        {
            SyncStatusText = status;
            _messenger.Send(new StatusMessage(status, IsBusy: true));
        });
        try
        {
            await _orderSyncService.SyncAsync(SelectedDate, progress);
            await _inventorySyncService.SyncAsync(progress);
            SyncStatusText = "Refreshing dashboard...";
            await LoadAsync();
            _messenger.Send(new StatusMessage("Sync complete.", StatusSeverity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual sync failed");
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Sync failed: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsSyncing = false;
            SyncStatusText = null;
        }
    }

    [RelayCommand(CanExecute = nameof(HasError))]
    public void CopyError() => _clipboardService.SetText(LastError ?? string.Empty);

    private bool HasError() => !string.IsNullOrEmpty(LastError);

    partial void OnLastErrorChanged(string? value) => CopyErrorCommand.NotifyCanExecuteChanged();

    partial void OnSelectedDateChanged(DateOnly value) => _ = LoadAsync();

    /// <summary>Called whenever the Dashboard page is shown (including re-navigation to a cached
    /// page instance) so the date picker always reflects "today" rather than whatever day was left
    /// selected from a prior visit.</summary>
    public async Task ResetToTodayAsync()
    {
        var today = AmazonBusinessDay.TodayIn(_timeProvider.GetUtcNow());
        if (SelectedDate != today)
        {
            SelectedDate = today; // triggers OnSelectedDateChanged -> fire-and-forget LoadAsync()
        }
        else
        {
            await LoadAsync();
        }
    }
}
