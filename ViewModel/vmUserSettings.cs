using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using Prism.Mvvm;

namespace VisualHFT.ViewModel;

public class PropertyViewModel
{
    public string Name { get; set; }
    public string Value { get; set; }
    public ObservableCollection<PropertyViewModel> Children { get; set; } = new();
    public bool IsObject => Children.Count > 0;
}

public class vmUserSettings : BindableBase
{
    private ObservableCollection<PropertyViewModel> _properties = new();

    public ObservableCollection<PropertyViewModel> Properties
    {
        get => _properties;
        set
        {
            if (_properties != value) SetProperty(ref _properties, value);
        }
    }

    public void LoadJson(string jsonContent)
    {
        var jsonObject = JObject.Parse(jsonContent);
        Properties = ParseJObject(jsonObject);
    }

    private ObservableCollection<PropertyViewModel> ParseJObject(JObject obj)
    {
        var properties = new ObservableCollection<PropertyViewModel>();

        foreach (var prop in obj.Properties())
        {
            var propertyViewModel = new PropertyViewModel { Name = prop.Name };

            if (prop.Value.Type == JTokenType.Object)
                propertyViewModel.Children = ParseJObject(prop.Value as JObject);
            else
                propertyViewModel.Value = prop.Value.ToString();

            properties.Add(propertyViewModel);
        }

        return properties;
    }
}