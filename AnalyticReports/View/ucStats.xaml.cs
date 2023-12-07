using System.Windows.Controls;
using VisualHFT.AnalyticReports.ViewModel;

namespace VisualHFT.AnalyticReports.View;

/// <summary>
///     Interaction logic for ucStatsR.xaml
/// </summary>
public partial class ucStats : UserControl
{
    public ucStats()
    {
        InitializeComponent();
        DataContext = new vmStats();
    }
}