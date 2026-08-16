using System.Collections.ObjectModel;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Sync;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

public sealed record CatalogItemRow(string Asin, string? Sku, string? Title, string? Brand, string? ImageUrl);

/// <summary>One row per parent ASIN family. Standalone (non-variation) ASINs form their own single-item group, keyed by their own ASIN.</summary>
public sealed record CatalogGroup(string GroupKey, string GroupTitle, IReadOnlyList<CatalogItemRow> Items);

public partial class CatalogViewModel : ObservableObject
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly ICatalogSyncService _catalogSyncService;
    private readonly IMessenger _messenger;
    private readonly ILogger<CatalogViewModel> _logger;

    public CatalogViewModel(
        ICatalogRepository catalogRepository,
        ICatalogSyncService catalogSyncService,
        IMessenger messenger,
        ILogger<CatalogViewModel> logger)
    {
        _catalogRepository = catalogRepository;
        _catalogSyncService = catalogSyncService;
        _messenger = messenger;
        _logger = logger;
    }

    public ObservableCollection<CatalogGroup> Groups { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private string? _syncStatus;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            LastError = null;
            var items = await _catalogRepository.GetAllItemsAsync();
            var parentTitles = (await _catalogRepository.GetAllParentsAsync())
                .ToDictionary(p => p.ParentAsin, p => p.Title);

            var groups = items
                .GroupBy(i => i.ParentAsin ?? i.Asin)
                .Select(g =>
                {
                    var title = (parentTitles.TryGetValue(g.Key, out var parentTitle) ? parentTitle : null)
                        ?? g.First().Title
                        ?? g.Key;
                    var rows = g
                        .Select(i => new CatalogItemRow(i.Asin, i.Sku, i.Title, i.Brand, i.ImageUrl))
                        .OrderBy(r => r.Asin)
                        .ToList();
                    return new CatalogGroup(g.Key, title, rows);
                })
                .OrderBy(g => g.GroupTitle)
                .ToList();

            Groups.Clear();
            foreach (var group in groups)
            {
                Groups.Add(group);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog data");
            LastError = ex.Message;
            _messenger.Send(new StatusMessage($"Failed to load catalog: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SyncAsync()
    {
        IsSyncing = true;
        try
        {
            var progress = new Progress<string>(status => SyncStatus = status);
            await _catalogSyncService.SyncAsync(progress);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog sync failed");
            _messenger.Send(new StatusMessage($"Catalog sync failed: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsSyncing = false;
        }
    }
}
