namespace VisualHFT.UserSettings
{
    public enum SettingKey
    {
        APPLICATION_THEME_MODE,
        APPLICATION_INITIALIZATION_SIZE_WIDTH,
        APPLICATION_INITIALIZATION_SIZE_HEIGHT,
        TILE_STUDY,

        PLUGIN
        // Add more settings here
    }
}

namespace VisualHFT.PluginManager
{
    public enum ePluginStatus
    {
        LOADED,
        STARTED,
        STOPPED,
        MALFUNCTIONING
    }
}