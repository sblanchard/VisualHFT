using VisualHFT.Model;

namespace VisualHFT.Commons.Studies;

public interface IStudy : IDisposable
{
    string TileTitle { get; set; }
    string TileToolTip { get; set; }
    public event EventHandler<decimal> OnAlertTriggered;
    public event EventHandler<BaseStudyModel> OnCalculated;

    Task StartAsync();
    Task StopAsync();
}