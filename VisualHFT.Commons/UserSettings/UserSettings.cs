namespace VisualHFT.UserSettings;

[Serializable]
public class UserSettings
{
    public UserSettings()
    {
        Settings = new Dictionary<SettingKey, object>();
        ComponentSettings = new Dictionary<SettingKey, Dictionary<string, object>>();
    }

    public Dictionary<SettingKey, object> Settings { get; set; }
    public Dictionary<SettingKey, Dictionary<string, object>> ComponentSettings { get; set; }
}