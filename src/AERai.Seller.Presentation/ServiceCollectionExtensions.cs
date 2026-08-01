using AERai.Seller.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Seller.Presentation;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ViewModels. Requires the Wpf composition root to have already registered an
    /// IThemeService implementation (see CLAUDE.md's composition-root exception).
    /// Transient: a new ViewModel instance per navigation to the corresponding page.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
