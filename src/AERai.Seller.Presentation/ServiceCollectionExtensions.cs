using AERai.Seller.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Seller.Presentation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ViewModels and cross-cutting Presentation services. Requires the Wpf composition
    /// root to have already registered an IThemeService implementation (see CLAUDE.md's
    /// composition-root exception).
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Singleton: the app-wide status bar must persist and keep receiving messages across
        // page navigation, unlike per-page ViewModels below.
        services.AddSingleton<StatusBarViewModel>();

        // Transient: a new ViewModel instance per navigation to the corresponding page.
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
