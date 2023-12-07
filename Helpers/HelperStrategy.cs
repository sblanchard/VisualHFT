using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VisualHFT.Model;

namespace VisualHFT.Helpers;

public class HelperStrategy : ObservableCollection<string>
{
    protected object _LOCK = new();

    public void UpdateData(List<StrategyVM> data)
    {
        lock (_LOCK)
        {
            foreach (var vm in data)
                if (!this.Any(x => x == vm.StrategyCode))
                    Add(vm.StrategyCode);
        }
    }
}