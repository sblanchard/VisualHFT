using System.Windows.Controls;
using VisualHFT.Helpers;
using VisualHFT.ViewModel;

namespace VisualHFT.View;

/// <summary>
///     Interaction logic for ucProviderConnectivity.xaml
/// </summary>
public partial class ucProviderConnectivity : UserControl
{
    public ucProviderConnectivity()
    {
        InitializeComponent();
        DataContext = new vmProvider(HelperCommon.GLOBAL_DIALOGS);
    }
}