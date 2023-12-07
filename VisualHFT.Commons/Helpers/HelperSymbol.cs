using System.Reflection;
using log4net;

namespace VisualHFT.Helpers;

public class HelperSymbol : List<string>
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    public static HelperSymbol Instance { get; } = new();

    public event EventHandler OnCollectionChanged;


    public void UpdateData(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return;
        if (Contains(symbol)) return;
        Add(symbol);
        OnCollectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateData(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols) UpdateData(symbol);
    }
}