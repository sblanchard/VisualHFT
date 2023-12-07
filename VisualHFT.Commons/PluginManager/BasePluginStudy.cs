using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using log4net;
using Newtonsoft.Json.Linq;
using VisualHFT.Commons.Studies;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.UserSettings;
using ErrorEventArgs = VisualHFT.PluginManager.ErrorEventArgs;

namespace VisualHFT.Commons.PluginManager;

public abstract class BasePluginStudy : IStudy, IPlugin, IDisposable
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    protected bool _disposed = false; // to track whether the object has been disposed

    public BasePluginStudy()
    {
        // Ensure settings are loaded before starting
        LoadSettings();
        if (Settings == null)
            throw new InvalidOperationException($"{Name} plugin settings has not been loaded.");

        Status = ePluginStatus.LOADED;
    }

    public abstract event EventHandler<ErrorEventArgs> OnError;

    public abstract string Name { get; set; }
    public abstract string Version { get; set; }
    public abstract string Description { get; set; }
    public abstract string Author { get; set; }
    public abstract ISetting Settings { get; set; }
    public ePluginStatus Status { get; set; }
    public abstract Action CloseSettingWindow { get; set; }

    public virtual string GetPluginUniqueID()
    {
        // Get the fully qualified name of the assembly
        var assemblyName = GetType().Assembly.FullName;

        // Concatenate the attributes
        var combinedAttributes = $"{Name}{Author}{Version}{Description}{assemblyName}";

        // Compute the SHA256 hash
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedAttributes));
            var builder = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    public abstract object GetUISettings(); //using object type because this csproj doesn't support UI


    public abstract event EventHandler<decimal> OnAlertTriggered;
    public abstract event EventHandler<BaseStudyModel> OnCalculated;
    public abstract string TileTitle { get; set; }
    public abstract string TileToolTip { get; set; }

    public virtual async Task StartAsync()
    {
        Status = ePluginStatus.STARTED;
        log.Info("Plugins: " + Name + " has started.");
    }

    public virtual async Task StopAsync()
    {
        Status = ePluginStatus.STOPPED;
        log.Info("Plugins: " + Name + " has stopped.");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    protected abstract void LoadSettings();
    protected abstract void SaveSettings();
    protected abstract void InitializeDefaultSettings();


    protected void SaveToUserSettings(ISetting settings)
    {
        SettingsManager.Instance.SetSetting(SettingKey.TILE_STUDY, GetPluginUniqueID(), settings);
    }

    protected T LoadFromUserSettings<T>() where T : class
    {
        var jObject =
            SettingsManager.Instance.GetSetting<object>(SettingKey.TILE_STUDY, GetPluginUniqueID()) as JObject;
        if (jObject != null) return jObject.ToObject<T>();
        return null;
    }

    protected virtual void Dispose(bool disposing)
    {
        // Common disposal logic if any
    }
}