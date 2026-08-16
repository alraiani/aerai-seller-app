using System.Collections.ObjectModel;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Replenishment;
using AERai.Seller.Application.Sync;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

/// <summary>One bar in the selected SKU's FBA-vs-AWD location breakdown widget.</summary>
public sealed record LocationQuantity(string Location, int Quantity);

/// <summary>
/// Backs the Replenishment page: urgency-sorted reorder recommendations, a data-coverage warning
/// with a 90-day backfill action, and per-SKU trend/location detail. Plain data only — chart types
/// (ISeries/Axis) are LiveCharts2/WPF-specific and get built in the View's code-behind instead,
/// keeping this Presentation-layer ViewModel UI-framework-agnostic (see ReplenishmentPage.xaml.cs,
/// which mirrors DashboardPage.xaml.cs's chart-wiring pattern).
/// </summary>
public partial class ReplenishmentViewModel : ObservableObject
{
    private const int RecalculateStalenessMinutes = 15;

    private readonly IReplenishmentQueryService _replenishmentQueryService;
    private readonly IReplenishmentPlanningService _replenishmentPlanningService;
    private readonly IAwdInventorySyncService _awdInventorySyncService;
    private readonly IOrderSyncService _orderSyncService;
    private readonly ISyncMetadataRepository _syncMetadataRepository;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReplenishmentViewModel> _logger;

    private CancellationTokenSource? _backfillCts;

    public ReplenishmentViewModel(
        IReplenishmentQueryService replenishmentQueryService,
        IReplenishmentPlanningService replenishmentPlanningService,
        IAwdInventorySyncService awdInventorySyncService,
        IOrderSyncService orderSyncService,
        ISyncMetadataRepository syncMetadataRepository,
        IMessenger messenger,
        TimeProvider timeProvider,
        ILogger<ReplenishmentViewModel> logger)
    {
        _replenishmentQueryService = replenishmentQueryService;
        _replenishmentPlanningService = replenishmentPlanningService;
        _awdInventorySyncService = awdInventorySyncService;
        _orderSyncService = orderSyncService;
        _syncMetadataRepository = syncMetadataRepository;
        _messenger = messenger;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public ObservableCollection<ReplenishmentRow> Rows { get; } = [];
    public ObservableCollection<SalesVelocityPoint> SalesTrend { get; } = [];
    public ObservableCollection<LocationQuantity> LocationBreakdown { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRecalculating;

    [ObservableProperty]
    private bool _isBackfilling;

    [ObservableProperty]
    private string? _backfillProgressText;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private bool _isCoverageSufficient = true;

    [ObservableProperty]
    private DateOnly? _earliestOrderDate;

    [ObservableProperty]
    private int _skusNeedingActionCount;

    [ObservableProperty]
    private int _totalUnitsToReorder;

    [ObservableProperty]
    private decimal _estimatedReorderCost;

    [ObservableProperty]
    private int _insufficientDataCount;

    [ObservableProperty]
    private ReplenishmentRow? _selectedRow;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            LastError = null;

            var coverage = await _replenishmentQueryService.GetOrderCoverageAsync();
            IsCoverageSufficient = coverage.IsSufficient;
            EarliestOrderDate = coverage.EarliestOrderDate;

            var lastComputed = await _syncMetadataRepository.GetAsync(ReplenishmentPlanningService.JobName);
            var isStale = lastComputed is null
                || _timeProvider.GetUtcNow() - lastComputed.LastSuccessfulSyncAt > TimeSpan.FromMinutes(RecalculateStalenessMinutes);
            if (isStale)
            {
                await _replenishmentPlanningService.RecalculateAsync();
            }

            await ReloadRowsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load replenishment data");
            LastError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RecalculateAsync()
    {
        if (IsRecalculating) return;

        IsRecalculating = true;
        try
        {
            await _replenishmentPlanningService.RecalculateAsync();
            await ReloadRowsAsync();
            _messenger.Send(new StatusMessage("Replenishment recalculated.", StatusSeverity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replenishment recalculation failed");
            _messenger.Send(new StatusMessage($"Recalculation failed: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsRecalculating = false;
        }
    }

    [RelayCommand]
    public async Task BackfillLast90DaysAsync()
    {
        if (IsBackfilling) return;

        IsBackfilling = true;
        _backfillCts = new CancellationTokenSource();
        var progress = new Progress<string>(status => BackfillProgressText = status);
        try
        {
            await _orderSyncService.BackfillLastNDaysAsync(90, progress, _backfillCts.Token);
            _messenger.Send(new StatusMessage("Order history backfill complete.", StatusSeverity.Success));
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            _messenger.Send(new StatusMessage("Backfill cancelled — orders already fetched were kept.", StatusSeverity.Warning));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order backfill failed");
            _messenger.Send(new StatusMessage($"Backfill failed: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsBackfilling = false;
            BackfillProgressText = null;
            _backfillCts?.Dispose();
            _backfillCts = null;
        }
    }

    [RelayCommand]
    public void CancelBackfill() => _backfillCts?.Cancel();

    [RelayCommand]
    public async Task SyncAwdInventoryAsync()
    {
        var progress = new Progress<string>(status => _messenger.Send(new StatusMessage(status, IsBusy: true)));
        try
        {
            await _awdInventorySyncService.SyncAsync(progress);
            await RecalculateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWD inventory sync failed");
            _messenger.Send(new StatusMessage($"AWD sync failed: {ex.Message}", StatusSeverity.Error));
        }
    }

    partial void OnSelectedRowChanged(ReplenishmentRow? value)
    {
        if (value is null)
        {
            SalesTrend.Clear();
            LocationBreakdown.Clear();
            return;
        }

        LocationBreakdown.Clear();
        LocationBreakdown.Add(new LocationQuantity("FBA", value.FbaAvailable));
        LocationBreakdown.Add(new LocationQuantity("AWD", value.AwdOnhand));

        _ = LoadSalesTrendAsync(value.Sku);
    }

    private async Task LoadSalesTrendAsync(string sku)
    {
        try
        {
            var trend = await _replenishmentQueryService.GetSalesVelocityTrendAsync(sku, days: 90);
            SalesTrend.Clear();
            foreach (var point in trend)
            {
                SalesTrend.Add(point);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sales trend for {Sku}", sku);
        }
    }

    private async Task ReloadRowsAsync()
    {
        var rows = await _replenishmentQueryService.GetReplenishmentRowsAsync();
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        SkusNeedingActionCount = rows.Count(r => !r.HasInsufficientData && r.DaysUntilActionNeeded <= 0);
        TotalUnitsToReorder = rows.Sum(r => r.RecommendedOrderQuantity);
        EstimatedReorderCost = rows.Sum(r => r.EstimatedReorderCost ?? 0m);
        InsufficientDataCount = rows.Count(r => r.HasInsufficientData);
    }
}
