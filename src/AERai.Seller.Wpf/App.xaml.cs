using System.Windows;
using AERai.Seller.Application;
using AERai.Seller.Infrastructure;
using AERai.Seller.Presentation;
using AERai.Seller.Presentation.Abstractions;
using AERai.Seller.Presentation.ViewModels;
using AERai.Seller.SpApiClient;
using AERai.Seller.Desktop.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AERai.Seller.Desktop;

/// <summary>
/// Interaction logic for App.xaml. This is the DI composition root — the only place in the
/// Wpf project allowed to reference Infrastructure/SpApiClient concrete types directly
/// (see CLAUDE.md's composition-root exception).
/// </summary>
// Fully qualified: this project's own AERai.Seller.Application layer shares the AERai.Seller
// root namespace, which shadows System.Windows.Application for unqualified lookups here.
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSpApiClient();
                services.AddInfrastructure(AERai.Seller.Infrastructure.ServiceCollectionExtensions.GetDefaultSqliteDbPath());
                services.AddApplicationServices();
                services.AddPresentation();

                services.AddSingleton<IThemeService, WpfUiThemeService>();

                services.AddTransient<DashboardPage>();
                services.AddTransient<InventoryPage>();
                services.AddTransient<SettingsPage>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        using (var scope = _host.Services.CreateScope())
        {
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SellerDbContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            dbContext.Database.Migrate();
        }

        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.SetTheme(AppTheme.System);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
