using System.Windows.Controls;
using VisualHFT.AnalyticReports.ViewModel;

namespace VisualHFT.AnalyticReports.View;

/// <summary>
///     Interaction logic for ucChartsR.xaml
/// </summary>
public partial class ucCharts : UserControl
{
    public ucCharts()
    {
        InitializeComponent();
        DataContext = new vmCharts();
    }
}