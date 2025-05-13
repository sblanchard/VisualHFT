using Newtonsoft.Json;
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
using VisualHFT.TriggerEngine.Actions;
using VisualHFT.TriggerEngine.ViewModel;
using VisualHFT.ViewModel;

namespace VisualHFT.TriggerEngine.View
{
    /// <summary>
    /// Interaction logic for TriggerSettingAddOrUpdate.xaml
    /// </summary>
    public partial class TriggerSettingAddOrUpdate : Window
    {
        TriggerEngineRuleViewModel model = new TriggerEngineRuleViewModel();
        public ConditionOperator ConditionOperator { get; set; }
        public List<TilesView> PluginNames { get; set; }

        public long selectedID { get; set; }

        public TriggerSettingAddOrUpdate(TriggerEngineRuleViewModel _rule, vmDashboard dashboard)
        {

            var vmDashboard = dashboard;
            PluginNames = new List<TilesView>();
            vmDashboard.Tiles.ToList().ForEach(x =>
            {
                TilesView vm = new TilesView();
                vm.TileName = x.Title;  
                PluginNames.Add(vm);
            });

            if(_rule!=null)
            {
                this.model = _rule;
            }
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
            TriggerEngineRuleViewModel rule = this.model;


            TriggerRule triggerRule=rule.FromViewModel(rule);
            TriggerEngineService.AddOrUpdateRule(triggerRule);

        }

        private void btnAddNewAction_Click(object sender, RoutedEventArgs e)
        {
            TriggerActionViewModel mod = new TriggerActionViewModel();
            mod.id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            this.model.Actions.Add(mod);
            lstDataAction.InvalidateVisual();

        }

        public T DeepClone<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

        private void ClickSetAPI(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.DataContext is TriggerActionViewModel data)
            {
                selectedID = data.id;
                try
                {
                    var restAPIAction = DeepClone<RestApiActionViewModel>(data.RestApi);
                    AddAPISetting frmRuleView = new AddAPISetting(restAPIAction);
                    var d = frmRuleView.ShowDialog();
                    if (d == true)
                    {
                        RestApiActionViewModel mod = frmRuleView.restApiAction;
                        List<TriggerActionViewModel> replaceMod = this.model.Actions.Where(e => e.id == selectedID).ToList();
                        if (replaceMod.Count > 0)
                        {
                            replaceMod[0].RestApi = mod;
                            lstDataAction.InvalidateVisual();
                        }

                    }



                }
                catch (Exception ex)
                {
                }
            }
        }

        private void ComboBox_Selected(object sender, RoutedEventArgs e)
        {

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cbox && cbox.DataContext is TriggerActionViewModel data)
            {
                ActionType? newValue = null;


                if (e.AddedItems.Count > 0)
                {
                     newValue = (ActionType?)e.AddedItems[0]; 
                                                       
                   
                }

                List<TriggerActionViewModel> replaceMod = this.model.Actions.Where(e => e.id == data.id).ToList();
                if (replaceMod.Count > 0)
                {
                    if (newValue != null && newValue == ActionType.RestApi)
                    {
                        replaceMod[0].IsEnabled = true;
                        lstDataAction.InvalidateVisual();
                    } else
                    {
                        replaceMod[0].IsEnabled = false;
                        lstDataAction.InvalidateVisual();
                    }

                      
                }

                
            }

        }
    }
     
}
