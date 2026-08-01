namespace AERai.Seller.Presentation.Abstractions;

public enum AppTheme
{
    Light,
    Dark,
    System,
}

/// <summary>
/// Abstraction over the actual theming toolkit (WPF-UI) so ViewModels never take a direct
/// WPF-UI dependency. Implemented in the Wpf project's composition root, not here.
/// </summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void SetTheme(AppTheme theme);
}
