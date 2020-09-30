using ExchangeSharp;

namespace DayTradeScanner
{
    public class Signal
    {
        public string Symbol { get; set; }
        public string Trade { get; set; }
        public string Date { get; set; }
        public string TimeFrame { get; set; }
        public string HyperTraderURI { get; set; }
        public ExchangeVolume Volume { get; set; }
        public string FourHourTrend { get; set; }
        public string OneHourTrend { get; set; }
        public SymbolTrend FourHourTrendObject { get; set; }
        public SymbolTrend OneHourTrendObject { get; set; }
        public string BBBandwidth { get; set; }
    }
}