﻿using System.Reflection;
using log4net;
using Newtonsoft.Json;

namespace VisualHFT.UserSettings;
/*
 USAGE:
        // To set a setting
        SettingsManager.Instance.SetSetting(SettingKey.Theme, "Dark");

        // To get a setting
        object theme = SettingsManager.Instance.GetSetting(SettingKey.Theme);

        // To save settings
        SettingsManager.Instance.SaveSettings();     
 */

public class SettingsManager
{
    private static readonly Lazy<SettingsManager> lazy = new(() => new SettingsManager());
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private readonly string appDataPath;
    private readonly string settingsFilePath;

    private SettingsManager()
    {
        appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        settingsFilePath = Path.Combine(appDataPath, "VisualHFT", "settings.json");
        // Load settings from file or create new
        LoadSettings();
    }


    public static SettingsManager Instance => lazy.Value;

    public UserSettings UserSettings { get; set; }

    public string GetAllSettings()
    {
        if (File.Exists(settingsFilePath))
        {
            var json = File.ReadAllText(settingsFilePath);
            return json;
        }

        return null;
    }

    private void LoadSettings()
    {
        // Deserialize from JSON file
        if (File.Exists(settingsFilePath))
        {
            var json = File.ReadAllText(settingsFilePath);
            UserSettings = JsonConvert.DeserializeObject<UserSettings>(json);
        }
        else
        {
            UserSettings = new UserSettings();
        }
    }

    private void SaveSettings()
    {
        try
        {
            // Ensure the directory exists
            var directoryPath = Path.GetDirectoryName(settingsFilePath);
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            // Serialize to JSON file
            var json = JsonConvert.SerializeObject(UserSettings);

            // Write to file
            File.WriteAllText(settingsFilePath, json);
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            log.Error($"An error occurred while saving settings: {ex}");
        }
    }

    public T GetSetting<T>(SettingKey key, string id) where T : class
    {
        if (UserSettings.ComponentSettings.TryGetValue(key, out var idSettings))
            if (idSettings.TryGetValue(id, out var value))
                try
                {
                    if (value is T)
                    {
                        return value as T;
                    }

                    // Attempt to deserialize the object into the expected type
                    var typedValue = JsonConvert.DeserializeObject<T>(value.ToString());
                    if (typedValue != null) return typedValue;
                }
                catch (JsonException ex)
                {
                    return null;
                }

        return null;
    }

    public void SetSetting(SettingKey key, string id, object value)
    {
        if (!UserSettings.ComponentSettings.ContainsKey(key))
            UserSettings.ComponentSettings[key] = new Dictionary<string, object>();
        UserSettings.ComponentSettings[key][id] = value;
        SaveSettings();
    }
}