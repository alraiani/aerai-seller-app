using System.Windows.Controls;
using AERai.Seller.Presentation.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace AERai.Seller.Desktop.Views;

public partial class BookkeepingAccountMappingPage : Page, INavigableView<BookkeepingAccountMappingViewModel>
{
    public BookkeepingAccountMappingViewModel ViewModel { get; }

    public BookkeepingAccountMappingPage(BookkeepingAccountMappingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
