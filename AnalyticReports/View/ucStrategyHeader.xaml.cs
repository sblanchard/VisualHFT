using System.Windows.Controls;
using VisualHFT.AnalyticReports.ViewModel;

namespace VisualHFT.AnalyticReports.View;

/// <summary>
///     Interaction logic for ucStrategyHeaderR.xaml
/// </summary>
public partial class ucStrategyHeader : UserControl
{
    public ucStrategyHeader()
    {
        InitializeComponent();
        DataContext = new vmStrategyHeader();
    }
}