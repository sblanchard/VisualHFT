using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using VisualHFT.TriggerEngine.Actions;

namespace VisualHFT.TriggerEngine.ViewModel
{
    public class TriggerEngineViewModel : INotifyPropertyChanged
    {

        private string _Name { get; set; }

        public BindingList<TriggerConditionViewModel> Condition { get; set; } = new BindingList<TriggerConditionViewModel>();
        public BindingList<TriggerActionViewModel> Actions { get; set; } = new BindingList<TriggerActionViewModel>();


        private bool IsEnabled { get; set; } = true;


        public string Name
        {
            get => _Name;
            set { _Name = value; OnPropertyChanged(nameof(Name)); }
        }



        

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
         

    }

    public class TilesView
    {
        public string TileName { get; set; }
    }
    public class TriggerConditionViewModel : INotifyPropertyChanged
    {
        private string _Plugin { get; set; }                 // e.g. "MarketMicrostructure"
        private string _Metric { get; set; }                 // e.g. "LOBImbalance"
        private ConditionOperator _Operator { get; set; }    // e.g. CrossesAbove, GreaterThan
        private double _Threshold { get; set; }              // e.g. 0.7
        private TimeWindow _Window { get; set; }             // Optional smoothing/aggregation logic


        public string Plugin
        {
            get => _Plugin;
            set { _Plugin = value; OnPropertyChanged(nameof(Plugin)); }
        }

        public string Metric
        {
            get => _Metric;
            set { _Metric = value; OnPropertyChanged(nameof(Metric)); }
        }
        public ConditionOperator Operator
        {
            get => _Operator;
            set { _Operator = value; OnPropertyChanged(nameof(Operator)); }
        }

        public double Threshold
        {
            get => _Threshold;
            set { _Threshold = value; OnPropertyChanged(nameof(Threshold)); }
        }
        
        public TimeWindow Window
        {
            get => _Window;
            set { _Window = value; OnPropertyChanged(nameof(Window)); }
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public class TriggerActionViewModel:INotifyPropertyChanged
    {
        private ActionType _Type { get; set; } = ActionType.RestApi;

        private RestApiAction? _RestApi { get; set; }         // Only required if Type == RestApi


        public ActionType Type
        {
            get => _Type;
            set { _Type = value; OnPropertyChanged(nameof(Type)); }
        }  
        public RestApiAction RestApi
        {
            get => _RestApi;
            set { _RestApi = value; OnPropertyChanged(nameof(RestApi)); }
        }



        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}