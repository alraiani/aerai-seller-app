using AERai.Seller.Application.Abstractions;
using AERai.Seller.Presentation.Abstractions;
using AERai.Seller.Presentation.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly IMessenger _messenger;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(
        IAppSettingsStore settingsStore, IThemeService themeService, IMessenger messenger, ILogger<SettingsViewModel> logger)
    {
        _settingsStore = settingsStore;
        _themeService = themeService;
        _messenger = messenger;
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
    private string? _saveStatusMessage;

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
            SaveStatusMessage = "Saved.";
            _messenger.Send(new StatusMessage("Settings saved.", StatusSeverity.Success));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            SaveStatusMessage = $"Failed to save: {ex.Message}";
            _messenger.Send(new StatusMessage($"Failed to save settings: {ex.Message}", StatusSeverity.Error));
        }
    }
}
