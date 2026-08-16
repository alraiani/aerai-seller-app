using System.Collections.ObjectModel;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Ai;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

public sealed record CatalogItemSettingsRow(string Sku, string? Title, string? Brand);

/// <summary>
/// Backs the Catalog Item Settings page: a read-only master list of known SKUs (left) and a flat
/// detail edit form (right) for per-SKU data SP-API can't supply — cost of goods and lead time
/// profile. No inline-editable grid exists anywhere else in the app, so this master-list-plus-
/// detail-panel shape (not an editable DataGrid) matches the only existing editing precedent,
/// SettingsPage's flat form, applied per selected row instead of to one global record.
/// </summary>
public partial class CatalogItemSettingsViewModel : ObservableObject
{
    private readonly ICatalogRepository _catalogRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILeadTimeProfileRepository _leadTimeProfileRepository;
    private readonly IAppSettingsStore _appSettingsStore;
    private readonly IMessenger _messenger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CatalogItemSettingsViewModel> _logger;

    private Product? _existingProduct;

    public CatalogItemSettingsViewModel(
        ICatalogRepository catalogRepository,
        IProductRepository productRepository,
        ILeadTimeProfileRepository leadTimeProfileRepository,
        IAppSettingsStore appSettingsStore,
        IMessenger messenger,
        TimeProvider timeProvider,
        ILogger<CatalogItemSettingsViewModel> logger)
    {
        _catalogRepository = catalogRepository;
        _productRepository = productRepository;
        _leadTimeProfileRepository = leadTimeProfileRepository;
        _appSettingsStore = appSettingsStore;
        _messenger = messenger;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public ObservableCollection<CatalogItemSettingsRow> Items { get; } = [];

    [ObservableProperty]
    private CatalogItemSettingsRow? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _saveStatusMessage;

    [ObservableProperty]
    private int _globalDefaultTargetStockDays;

    [ObservableProperty]
    private decimal? _costOfGoods;

    [ObservableProperty]
    private int _supplierLeadTimeDays;

    [ObservableProperty]
    private int _prepTimeDays;

    [ObservableProperty]
    private int _fbaTransitDays;

    [ObservableProperty]
    private int _safetyStockDays;

    /// <summary>When false, this SKU uses <see cref="GlobalDefaultTargetStockDays"/> (LeadTimeProfile.TargetStockDays stays null).</summary>
    [ObservableProperty]
    private bool _overrideTargetStockDays;

    [ObservableProperty]
    private int _targetStockDays;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _catalogRepository.GetAllItemsAsync();
            var appSettings = await _appSettingsStore.GetAsync();
            GlobalDefaultTargetStockDays = appSettings.DefaultTargetStockDays;

            var rows = items
                .Where(i => !string.IsNullOrEmpty(i.Sku))
                .GroupBy(i => i.Sku!)
                .Select(g => new CatalogItemSettingsRow(g.Key, g.First().Title, g.First().Brand))
                .OrderBy(r => r.Title ?? r.Sku)
                .ToList();

            Items.Clear();
            foreach (var row in rows)
            {
                Items.Add(row);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog item settings list");
            _messenger.Send(new StatusMessage($"Failed to load items: {ex.Message}", StatusSeverity.Error));
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedItemChanged(CatalogItemSettingsRow? value)
    {
        if (value is not null)
        {
            _ = LoadDetailAsync(value.Sku);
        }
    }

    private async Task LoadDetailAsync(string sku)
    {
        try
        {
            SaveStatusMessage = null;
            var products = await _productRepository.GetAllAsync();
            _existingProduct = products.FirstOrDefault(p => p.Sku == sku);
            CostOfGoods = _existingProduct?.CostOfGoods;

            var profile = await _leadTimeProfileRepository.GetAsync(sku);
            SupplierLeadTimeDays = profile?.SupplierLeadTimeDays ?? 0;
            PrepTimeDays = profile?.PrepTimeDays ?? 0;
            FbaTransitDays = profile?.FbaTransitDays ?? 0;
            SafetyStockDays = profile?.SafetyStockDays ?? 0;
            OverrideTargetStockDays = profile?.TargetStockDays.HasValue ?? false;
            TargetStockDays = profile?.TargetStockDays ?? GlobalDefaultTargetStockDays;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings for {Sku}", sku);
            SaveStatusMessage = $"Failed to load: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (SelectedItem is null) return;

        var sku = SelectedItem.Sku;
        try
        {
            var now = _timeProvider.GetUtcNow();

            var product = new Product
            {
                Sku = sku,
                Asin = _existingProduct?.Asin,
                Title = _existingProduct?.Title,
                CostOfGoods = CostOfGoods,
                CreatedAt = _existingProduct?.CreatedAt ?? now,
            };
            await _productRepository.UpsertAsync(product);
            _existingProduct = product;

            var profile = new LeadTimeProfile
            {
                Sku = sku,
                SupplierLeadTimeDays = SupplierLeadTimeDays,
                PrepTimeDays = PrepTimeDays,
                FbaTransitDays = FbaTransitDays,
                SafetyStockDays = SafetyStockDays,
                TargetStockDays = OverrideTargetStockDays ? TargetStockDays : null,
                UpdatedAt = now,
            };
            await _leadTimeProfileRepository.UpsertAsync(profile);

            SaveStatusMessage = "Saved.";
            _messenger.Send(new StatusMessage($"Settings saved for {sku}.", StatusSeverity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings for {Sku}", sku);
            SaveStatusMessage = $"Failed to save: {ex.Message}";
            _messenger.Send(new StatusMessage($"Failed to save settings: {ex.Message}", StatusSeverity.Error));
        }
    }
}
