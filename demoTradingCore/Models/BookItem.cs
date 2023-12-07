using System;

namespace demoTradingCore.Models
{
    public class BookItem
    {
        private long EntryID = 0;
        private eINCREMENTALTYPE IncrementalType = eINCREMENTALTYPE.NEWITEM;
        private bool IsBid = false;
        private bool IsTradeable = true;
        private string LayerName = "";
        private DateTime LocalTimeStamp;
        private double MinSize = 0;
        private double Price = 0;
        private int ProviderID = 0;
        private DateTime ServerTimeStamp;
        private double Size = 0;
    }
}