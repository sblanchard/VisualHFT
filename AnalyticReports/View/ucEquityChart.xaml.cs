using System.Windows.Controls;
using VisualHFT.AnalyticReports.ViewModel;

namespace VisualHFT.AnalyticReports.View;

/// <summary>
///     Interaction logic for ucEquityChartR.xaml
/// </summary>
public partial class ucEquityChart : UserControl
{
    public ucEquityChart()
    {
        InitializeComponent();
        DataContext = new vmEquityChart();
    }
}