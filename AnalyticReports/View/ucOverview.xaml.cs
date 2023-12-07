using System.Windows.Controls;
using VisualHFT.AnalyticReports.ViewModel;

namespace VisualHFT.AnalyticReports.View;

/// <summary>
///     Interaction logic for ucOverviewR.xaml
/// </summary>
public partial class ucOverview : UserControl
{
    public ucOverview()
    {
        InitializeComponent();
        DataContext = new vmOverview();
    }
}