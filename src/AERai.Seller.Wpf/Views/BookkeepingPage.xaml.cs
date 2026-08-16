using System.Windows.Controls;
using AERai.Seller.Presentation.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace AERai.Seller.Desktop.Views;

public partial class BookkeepingPage : Page, INavigableView<BookkeepingViewModel>
{
    public BookkeepingViewModel ViewModel { get; }

    public BookkeepingPage(BookkeepingViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
