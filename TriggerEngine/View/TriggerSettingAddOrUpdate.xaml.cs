using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using VisualHFT.TriggerEngine;
using VisualHFT.TriggerEngine.ViewModel;
using VisualHFT.ViewModel;

namespace VisualHFT.TriggerEngine.View
{
    /// <summary>
    /// Interaction logic for TriggerSettingAddOrUpdate.xaml
    /// </summary>
    public partial class TriggerSettingAddOrUpdate : Window
    {
        TriggerEngineViewModel model = new TriggerEngineViewModel();
        public ConditionOperator ConditionOperator { get; set; }
        public List<TilesView> PluginNames { get; set; }

        public TriggerSettingAddOrUpdate(TriggerRule _rule, vmDashboard dashboard)
        { 

            var vmDashboard = dashboard;

            PluginNames = new List<TilesView>();
            
            vmDashboard.Tiles.ToList().ForEach(x =>
            {
                TilesView vm = new TilesView();
                vm.TileName = x.Title;

                PluginNames.Add(vm);
            }); 
            this.DataContext = this.model;
            InitializeComponent();
        }

        private void btnAddNewCondition_Click(object sender, RoutedEventArgs e)
       {
            this.model.Condition.Add(new TriggerConditionViewModel()); 
            lstData.InvalidateVisual(); 
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var items = this.model;

        }
    }
     
}
