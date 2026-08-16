using System.Linq;
using System.Windows.Controls;
using AERai.Seller.Presentation.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Wpf.Ui.Abstractions.Controls;

namespace AERai.Seller.Desktop.Views;

public partial class DashboardPage : Page, INavigableView<DashboardViewModel>
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        ViewModel.DailySales.CollectionChanged += (_, _) => UpdateCharts();
        Loaded += async (_, _) => await ViewModel.ResetToTodayAsync();
    }

    private void UpdateCharts()
    {
        var labels = ViewModel.DailySales.Select(d => d.Date.ToString("ddd M/d")).ToList();

        UnitsChart.Series = new ISeries[]
        {
            new ColumnSeries<int> { Values = ViewModel.DailySales.Select(d => d.Units).ToArray(), Name = "Units" }
        };
        UnitsChart.XAxes = new[] { new Axis { Labels = labels } };

        RevenueChart.Series = new ISeries[]
        {
            new ColumnSeries<decimal> { Values = ViewModel.DailySales.Select(d => d.Revenue).ToArray(), Name = "Revenue" }
        };
        RevenueChart.XAxes = new[] { new Axis { Labels = labels } };
    }
}
