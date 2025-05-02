using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VisualHFT.TriggerEngine.ViewModel;
using VisualHFT.ViewModel;

namespace VisualHFT.TriggerEngine.View
{
    /// <summary>
    /// Interaction logic for TriggerSettingsView.xaml
    /// </summary>
    public partial class TriggerSettingsView : Window
    {
        List<TriggerEngineViewModel> lstCurrentRules = new List<TriggerEngineViewModel>();
        vmDashboard dashboard;
        public TriggerSettingsView(vmDashboard _dashboard)
        {
            InitializeComponent();

            

            this.dashboard= _dashboard;
            
            TriggerEngineService.GetRules().ForEach(x =>
            {
                TriggerEngineViewModel vm = new TriggerEngineViewModel();
                vm.Name = x.Name;
                vm.Condition = new BindingList<TriggerConditionViewModel>();
                x.Condition.ForEach(y =>
                {
                    TriggerConditionViewModel vmCondition = new TriggerConditionViewModel();
                    vmCondition.Plugin = y.Plugin;
                    vmCondition.Metric = y.Metric;
                    vmCondition.Operator = y.Operator;
                    vmCondition.Threshold = y.Threshold;
                    vm.Condition.Add(vmCondition);
                });
                lstCurrentRules.Add(vm);
            }); 

            this.DataContext = lstCurrentRules;

        }

        private void NewRule_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                TriggerSettingAddOrUpdate frmRuleView = new TriggerSettingAddOrUpdate(null, dashboard);
                frmRuleView.ShowDialog();
            }
            catch (Exception ex)
            {
            }

        }
    }
}
