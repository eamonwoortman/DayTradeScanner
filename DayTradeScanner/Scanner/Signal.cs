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
        public SymbolTrend FourHourTrend { get; set; }
        public SymbolTrend OneHourTrend { get; set; }
    }
}