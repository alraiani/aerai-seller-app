using AERai.Seller.Presentation.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AERai.Seller.Desktop;

/// <summary>
/// Composition-root-only implementation of IThemeService: wraps WPF-UI's ApplicationThemeManager
/// so Presentation ViewModels never take a direct WPF-UI dependency (see CLAUDE.md).
/// </summary>
public sealed class WpfUiThemeService : IThemeService
{
    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;

        switch (theme)
        {
            case AppTheme.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
                break;
            case AppTheme.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
                break;
            case AppTheme.System:
            default:
                ApplicationThemeManager.ApplySystemTheme();
                break;
        }
    }
}
