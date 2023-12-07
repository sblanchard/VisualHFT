using System.Windows.Controls;
using VisualHFT.AnalyticReports.ViewModel;

namespace VisualHFT.AnalyticReports.View;

/// <summary>
///     Interaction logic for ucChartsStatistics.xaml
/// </summary>
public partial class ucChartsStatistics : UserControl
{
    public ucChartsStatistics()
    {
        InitializeComponent();
        DataContext = new vmChartsStatistics();
    }
}