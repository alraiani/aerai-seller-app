using System.Linq;
using System.Windows.Controls;
using AERai.Seller.Presentation.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Wpf.Ui.Abstractions.Controls;

namespace AERai.Seller.Desktop.Views;

public partial class ReplenishmentPage : Page, INavigableView<ReplenishmentViewModel>
{
    public ReplenishmentViewModel ViewModel { get; }

    public ReplenishmentPage(ReplenishmentViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        ViewModel.LocationBreakdown.CollectionChanged += (_, _) => UpdateLocationChart();
        ViewModel.SalesTrend.CollectionChanged += (_, _) => UpdateTrendChart();
        Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void UpdateLocationChart()
    {
        var labels = ViewModel.LocationBreakdown.Select(l => l.Location).ToList();

        LocationChart.Series = new ISeries[]
        {
            new ColumnSeries<int> { Values = ViewModel.LocationBreakdown.Select(l => l.Quantity).ToArray(), Name = "Quantity" }
        };
        LocationChart.XAxes = new[] { new Axis { Labels = labels } };
    }

    private void UpdateTrendChart()
    {
        var labels = ViewModel.SalesTrend.Select(p => p.Date.ToString("M/d")).ToList();

        TrendChart.Series = new ISeries[]
        {
            new ColumnSeries<int> { Values = ViewModel.SalesTrend.Select(p => p.Units).ToArray(), Name = "Units" }
        };
        TrendChart.XAxes = new[] { new Axis { Labels = labels } };
    }
}
