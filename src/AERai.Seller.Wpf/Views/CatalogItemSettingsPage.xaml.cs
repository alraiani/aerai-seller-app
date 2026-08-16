using System.Windows.Controls;
using AERai.Seller.Presentation.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace AERai.Seller.Desktop.Views;

public partial class CatalogItemSettingsPage : Page, INavigableView<CatalogItemSettingsViewModel>
{
    public CatalogItemSettingsViewModel ViewModel { get; }

    public CatalogItemSettingsPage(CatalogItemSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
