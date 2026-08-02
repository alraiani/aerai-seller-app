using AERai.Seller.Desktop.Views;
using AERai.Seller.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace AERai.Seller.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow(IServiceProvider serviceProvider, StatusBarViewModel statusBarViewModel)
    {
        InitializeComponent();
        DataContext = statusBarViewModel;
        RootNavigation.SetServiceProvider(serviceProvider);

        // NavigationView's content-hosting template part isn't applied until the control has
        // gone through layout, so the initial Navigate() must wait for Loaded rather than
        // running directly in the constructor (throws NullReferenceException otherwise).
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
    }
}
