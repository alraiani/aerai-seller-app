using AERai.Seller.Application.Abstractions;
using AERai.Seller.Presentation.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(IAppSettingsStore settingsStore, IThemeService themeService, ILogger<SettingsViewModel> logger)
    {
        _settingsStore = settingsStore;
        _themeService = themeService;
        _logger = logger;
        SelectedTheme = themeService.CurrentTheme;
    }

    [ObservableProperty]
    private string? _clientId;

    [ObservableProperty]
    private string? _clientSecret;

    [ObservableProperty]
    private string? _refreshToken;

    [ObservableProperty]
    private string _apiHost = "https://sellingpartnerapi-na.amazon.com";

    [ObservableProperty]
    private string _marketplaceId = "ATVPDKIKX0DER";

    public IReadOnlyList<AppTheme> AvailableThemes { get; } = [AppTheme.Light, AppTheme.Dark, AppTheme.System];

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        var settings = await _settingsStore.GetAsync();
        ClientId = settings.ClientId;
        ClientSecret = settings.ClientSecret;
        RefreshToken = settings.RefreshToken;
        ApiHost = settings.ApiHost;
        MarketplaceId = settings.MarketplaceId;
        SelectedTheme = Enum.TryParse<AppTheme>(settings.Theme, out var theme) ? theme : AppTheme.System;
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(new AppSettings(
                ClientId, ClientSecret, RefreshToken, ApiHost, MarketplaceId, SelectedTheme.ToString()));
            _themeService.SetTheme(SelectedTheme);
            StatusMessage = "Saved.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = $"Failed to save: {ex.Message}";
        }
    }
}
