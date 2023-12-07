using Newtonsoft.Json;
using VisualHFT.PluginManager;

namespace VisualHFT.Model;

public class Provider
{
    public int ProviderID
    {
        get => ProviderCode;
        set => ProviderCode = value;
    }

    public int ProviderCode { get; set; }
    public string ProviderName { get; set; }
    public eSESSIONSTATUS Status { get; set; }
    public DateTime LastUpdated { get; set; }

    public string StatusImage
    {
        get
        {
            if (Status == eSESSIONSTATUS.BOTH_CONNECTED)
                return "/Images/imgGreenBall.png";
            if (Status == eSESSIONSTATUS.BOTH_DISCONNECTED)
                return "/Images/imgRedBall.png";
            return "/Images/imgYellowBall.png";
        }
    }

    public string Tooltip
    {
        get
        {
            if (Status == eSESSIONSTATUS.BOTH_CONNECTED)
                return "Connected";
            if (Status == eSESSIONSTATUS.BOTH_DISCONNECTED)
                return "Disconnected";
            if (Status == eSESSIONSTATUS.PRICE_CONNECTED_ORDER_DISCONNECTED)
                return "Price connected. Order disconnected";
            if (Status == eSESSIONSTATUS.PRICE_DSICONNECTED_ORDER_CONNECTED)
                return "Price disconnected. Order connected";
            return "";
        }
    }

    [JsonIgnore] public IPlugin Plugin { get; set; } //reference to a plugin (if any)
}