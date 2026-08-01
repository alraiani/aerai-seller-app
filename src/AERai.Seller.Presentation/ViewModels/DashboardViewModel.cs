using System.Collections.ObjectModel;
using AERai.Seller.Application.Dashboard;
using AERai.Seller.Application.Sync;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

/// <summary>
/// Backs the Dashboard page. First widget shown is today's orders summary grouped by SKU;
/// later phases add more tiles (replenishment alerts, financial summary) to this same page.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardQueryService _dashboardQueryService;
    private readonly IInventorySyncService _inventorySyncService;
    private readonly IOrderSyncService _orderSyncService;
    private readonly ILogger<DashboardViewModel> _logger;

    public DashboardViewModel(
        IDashboardQueryService dashboardQueryService,
        IInventorySyncService inventorySyncService,
        IOrderSyncService orderSyncService,
        ILogger<DashboardViewModel> logger)
    {
        _dashboardQueryService = dashboardQueryService;
        _inventorySyncService = inventorySyncService;
        _orderSyncService = orderSyncService;
        _logger = logger;
    }

    public ObservableCollection<SkuOrderSummary> TodaysOrdersBySku { get; } = [];
    public ObservableCollection<SyncStatusSummary> SyncStatuses { get; } = [];

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string? _lastError;

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            LastError = null;
            var summaries = await _dashboardQueryService.GetTodaysOrdersBySkuAsync();
            TodaysOrdersBySku.Clear();
            foreach (var summary in summaries)
            {
                TodaysOrdersBySku.Add(summary);
            }

            var statuses = await _dashboardQueryService.GetSyncStatusAsync();
            SyncStatuses.Clear();
            foreach (var status in statuses)
            {
                SyncStatuses.Add(status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard data");
            LastError = ex.Message;
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
        try
        {
            await _orderSyncService.SyncAsync();
            await _inventorySyncService.SyncAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual sync failed");
            LastError = ex.Message;
        }
        finally
        {
            IsSyncing = false;
        }
    }
}
