using System.Collections.ObjectModel;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Staging;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

public sealed record InventoryRow(string Sku, int Available, int Inbound, int Reserved, int Unfulfillable, bool LowStock);

public partial class InventoryViewModel : ObservableObject
{
    private const int LowStockThreshold = 10;

    private readonly IInventoryRepository _inventoryRepository;
    private readonly IMessenger _messenger;
    private readonly ILogger<InventoryViewModel> _logger;

    public InventoryViewModel(IInventoryRepository inventoryRepository, IMessenger messenger, ILogger<InventoryViewModel> logger)
    {
        _inventoryRepository = inventoryRepository;
        _messenger = messenger;
        _logger = logger;
    }

    public ObservableCollection<InventoryRow> Rows { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _lastError;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            LastError = null;
            var snapshots = await _inventoryRepository.GetLatestSnapshotsAsync();

            var rows = snapshots
                .GroupBy(s => s.Sku)
                .Select(g =>
                {
                    var available = QuantityFor(g, InventoryState.Available);
                    return new InventoryRow(
                        Sku: g.Key,
                        Available: available,
                        Inbound: QuantityFor(g, InventoryState.Inbound),
                        Reserved: QuantityFor(g, InventoryState.Reserved),
                        Unfulfillable: QuantityFor(g, InventoryState.Unfulfillable),
                        LowStock: available < LowStockThreshold);
                })
                .OrderBy(r => r.Sku)
                .ToList();

            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load inventory data");
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Failed to load inventory: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static int QuantityFor(IEnumerable<InventorySnapshot> snapshots, InventoryState state)
        => snapshots.Where(s => s.State == state).Sum(s => s.Quantity);
}
