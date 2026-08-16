using System.Windows.Controls;
using AERai.Seller.Presentation.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace AERai.Seller.Desktop.Views;

public partial class CatalogPage : Page, INavigableView<CatalogViewModel>
{
    public CatalogViewModel ViewModel { get; }

    public CatalogPage(CatalogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
