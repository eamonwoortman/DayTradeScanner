using Avalonia.Controls;
using DayTradeScanner.Bot.Implementation;
using ExchangeSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DayTradeScanner
{

    public class SimpleWeightedAverage {
        private class AveragePair
        {
            public decimal Value;
            public decimal Weight;
            public AveragePair(decimal value, decimal weight)
            {
                Value = value;
                Weight = weight;
            }
        }
        private List<AveragePair> pairs = new List<AveragePair>(); 
        public void Add(decimal val, decimal weight)
        {
            pairs.Add(new AveragePair(val, weight));
        }

        public void Clear()
        {
            pairs.Clear();
        }

        public decimal Result()
        {
            decimal total = 0m;
            decimal totalWeight = pairs.Sum(x => x.Weight);
            foreach(AveragePair pair in pairs)
            {
                total += ((pair.Value * pair.Weight) / totalWeight);
            }
            return total;
        }
    }

    public struct SymbolTrend {
        public int TimeframeInHours { get; set; }
        public decimal Trend { get { return TrendRaw / 100M; } }
        public decimal TrendRaw { get; set; }
        public MarketCandle Candle { get; set; }
        public SymbolTrend(int timeframe) {
            TimeframeInHours = timeframe;
            TrendRaw = 0M;
            Candle = null;
        }
	}


    //
    // Summary:
    //     Provides data for the System.ComponentModel.INotifyPropertyChanged.PropertyChanged
    //     event.
    public class TimeframeChangedEventArgs : EventArgs {

        public TimeframeChangedEventArgs(int timeframePeriod) {
            TimeframePeriod = timeframePeriod;
        }

        public int TimeframePeriod { get; private set; }
    }


    public class ExtendedSymbol {
        public ExchangeMarket Symbol { get; private set; }
        public ExchangeTicker Ticker { get; private set; }
        public SymbolTrend[] Trends { get; private set; }

        public string MarketSymbol { get; private set; }

        public Dictionary<int, List<MarketCandle>> TimeframeCandles { get; private set; }
        public Dictionary<int, DateTime> SignalTimestamps { get; private set; }

        public ExtendedSymbol(ExchangeMarket symbol, ExchangeTicker ticker, SymbolTrend[] trends) {
            this.Symbol = symbol;
            this.Ticker = ticker;
            this.Trends = trends;
            MarketSymbol = symbol.MarketSymbol;
            TimeframeCandles = new Dictionary<int, List<MarketCandle>>();
            SignalTimestamps = new Dictionary<int, DateTime>();
        }

        public delegate void TimeframeChangedEventHandler(object sender, TimeframeChangedEventArgs e);
        public event TimeframeChangedEventHandler TimeframeChanged;

        private decimal GetRawTrend(MarketCandle candle) {
            decimal diff = candle.ClosePrice - candle.OpenPrice;
            decimal trendPercentage = (diff / candle.OpenPrice) * 100M;
            return trendPercentage;
        }

        public void UpdateTrends() {
            for (int i = 0; i < Trends.Length; i++) {
                SymbolTrend trend = Trends[i];
                int minutes = trend.TimeframeInHours * 60;
                if (TimeframeCandles?.Count == 0 || !TimeframeCandles.ContainsKey(minutes) || TimeframeCandles[minutes].Count == 0) {
                    continue;
				}
                trend.Candle = TimeframeCandles[minutes][0];
                trend.TrendRaw = GetRawTrend(trend.Candle);
                Trends[i] = trend;
            }
		}

		public void NotifyCandleUpdate(int timeframePeriod) {
            UpdateTrends();
            TimeframeChanged.Invoke(this, new TimeframeChangedEventArgs(timeframePeriod));
        }
        public override int GetHashCode() {
            return this.MarketSymbol.GetHashCode();
        }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var other = (ExtendedSymbol)obj;
            return this.MarketSymbol == other.MarketSymbol;
        }

    }
    public class Scanner
    {
        private Settings _settings;
        private ExchangeAPI _api;
        private List<ExtendedSymbol> _symbols = new List<ExtendedSymbol>();
        private SimpleWeightedAverage fourHourTrendAverage = new SimpleWeightedAverage();
        private SimpleWeightedAverage oneHourTrendAverage = new SimpleWeightedAverage();

        public Scanner(Settings settings)
        {
            _settings = settings;

            switch (_settings.Exchange.ToLowerInvariant())
            {
                case "bitfinex":
                    _api = new ExchangeBitfinexAPI();
                    break;

                case "bittrex":
                    _api = new ExchangeBittrexAPI();
                    break;

                case "binance":
                    _api = new ExchangeBinanceAPI();
                    break;

                case "kraken":
                    _api = new ExchangeKrakenAPI();
                    break;

                default:
                    Console.WriteLine($"Unknown exchange:{_settings.Exchange}");
                    return;
            }

            _api.RateLimit = new RateGate(800, TimeSpan.FromSeconds(60d));
        }

        public List<int> StrategyPeriodsMinutes { get; private set; }
        public List<int> PeriodsMinutes { get; private set; }
        public Dictionary<int, string> timeframeKlines;
        public const int MaxCandlesPerTimeframe = 150;

        public string GetKlineFromMinutes(int periodMinutes) {
            return timeframeKlines[periodMinutes];
		}

        public async Task GetInitialCandles(int periodMinutes, CancellationToken ct) {
            try {
                int maxCandles = MaxCandlesPerTimeframe;
                // we don't need 150 candles for the non-strategy symbols (1h and 4h trend)
                if (!StrategyPeriodsMinutes.Contains(periodMinutes)) {
                    maxCandles = 4;
                }
                foreach (ExtendedSymbol symbol in Symbols) {
                    if (ct.IsCancellationRequested) {
                        ct.ThrowIfCancellationRequested();
                    }
                    
                    Dictionary<int, List<MarketCandle>> marketCandles = symbol.TimeframeCandles;
                    lock (marketCandles) {
                        if (!marketCandles.ContainsKey(periodMinutes)) {
                            List<MarketCandle> candleQueue = new List<MarketCandle>(MaxCandlesPerTimeframe);
                            marketCandles.Add(periodMinutes, candleQueue);
                        }
                    }
                    if (_api == null) {
                        break;
					}
                    var candles = (await _api.GetCandlesAsync(symbol.Symbol.MarketSymbol, 60 * periodMinutes, DateTime.Now.AddMinutes(-periodMinutes * maxCandles), null, maxCandles)).Reverse().ToList();
                    candles = AddMissingCandles(candles, periodMinutes);
                    marketCandles[periodMinutes].AddRange(candles);
                    symbol.NotifyCandleUpdate(periodMinutes);
                    Trace.WriteLine($"Got {candles.Count} candles in {symbol.Symbol.MarketSymbol} for timeframe {periodMinutes}");
                }
            } catch (Exception ex) { Console.WriteLine($"[DayTrader] Exception caught: {ex}"); }
        }

        private void Setup() {
            StrategyPeriodsMinutes = new List<int>();
            timeframeKlines = new Dictionary<int, string>();

            foreach (string timeframe in _settings.TimeFrames) {
                int minutes = TimeframeToMinutes(timeframe);
                StrategyPeriodsMinutes.Add(minutes);
            }

            PeriodsMinutes = new List<int>(StrategyPeriodsMinutes);
            PeriodsMinutes.Add(60); // add hour trend
            PeriodsMinutes.Add(240); // add 4 hour trend
            PeriodsMinutes = PeriodsMinutes.OrderByDescending(x => x).ToList();

            foreach (int minutes in PeriodsMinutes) {
                string klineTimeframe = PeriodToKlineTimeframe(minutes);
                timeframeKlines.Add(minutes, klineTimeframe);
            }
        }

        public bool InitialCandlesFetched { get; set; }

        public async Task Initialize() {
            try {
                Setup();
                await FindCoinsWithEnoughVolume();
            } catch (Exception ex) {
                Console.WriteLine($"[DayTradeScanner] Exception found: {ex}");
			}
		}

        /// <summary>
        /// Downloads all symbols from the exchanges and filters out the coins with enough 24hr Volume
        /// </summary>
        /// <returns></returns>
        public async Task FindCoinsWithEnoughVolume()
        {
            _symbols = new List<ExtendedSymbol>();
            var allSymbolsMeta = await _api.GetMarketSymbolsMetadataAsync();
            var allTickers = await _api.GetTickersAsync();

            // for each symbol
            foreach (var metadata in allSymbolsMeta)
            {
                string symbol = metadata.MarketSymbol.ToUpperInvariant();
                if (!IsValidCurrency(symbol))
                {
                    // ignore, symbol has wrong currency
                    continue;
                }

                // check 24hr volume
                var ticker = allTickers.FirstOrDefault(e => e.Key == symbol).Value;
				var volume = ticker.Volume.QuoteCurrencyVolume;
				if (_api is ExchangeBittrexAPI)
				{
					// bittrex reports wrong volume :-(
					volume = ticker.Volume.BaseCurrencyVolume;
				}

				if (volume < _settings.Min24HrVolume)
                {
                    // ignore since 24hr volume is too low
                    continue;
                }

                if (ticker.Ask < _settings.MinPrice)
                {
                    Trace.WriteLine($"Ignoring because price is too low: {ticker.Ask}");
                    continue;
                }

                SymbolTrend fourHourTrend = new SymbolTrend(4);
                SymbolTrend oneHourTrend = new SymbolTrend(1);

                // add to list
                ExtendedSymbol extendedSymbol = new ExtendedSymbol(metadata, ticker, new SymbolTrend[2]);
                extendedSymbol.Trends[0] = fourHourTrend;
                extendedSymbol.Trends[1] = oneHourTrend;

                _symbols.Add(extendedSymbol);
            }
            _symbols = _symbols.OrderBy(e => e.Symbol.MarketSymbol).ToList();
        }


		private MarketCandle emptyCandle = new MarketCandle();

        private MarketCandle CalculateFourHourCandle(List<MarketCandle> candles)
        {
            MarketCandle lastCandle = candles[0];
            MarketCandle firstCandle = candles[candles.Count - 1];
            MarketCandle fourHourCandle = new MarketCandle();
            fourHourCandle.BaseCurrencyVolume = firstCandle.BaseCurrencyVolume;
            fourHourCandle.ExchangeName = firstCandle.ExchangeName;
            fourHourCandle.Name = firstCandle.Name;
            fourHourCandle.PeriodSeconds = (60 * 60) * 4;
            fourHourCandle.Timestamp = lastCandle.Timestamp;
            fourHourCandle.ClosePrice = lastCandle.ClosePrice;
            fourHourCandle.OpenPrice = firstCandle.OpenPrice;
            fourHourCandle.HighPrice = candles.Average(x => x.HighPrice);
            fourHourCandle.LowPrice = candles.Average(x => x.LowPrice);
            fourHourCandle.QuoteCurrencyVolume = candles.Average(x => x.QuoteCurrencyVolume);
            fourHourCandle.WeightedAverage = candles.Average(x => x.WeightedAverage);
            return fourHourCandle;
        }

            private async Task<List<MarketCandle>> GetTrendCandles(string marketSymbol)
        {
            var candles = (await _api.GetCandlesAsync(marketSymbol, 60 * 60, DateTime.UtcNow.AddHours(-4), null, 4)).Reverse().ToList();
            if (candles.Count() != 4)
            {
                Trace.WriteLine("Couldn't get 4 candles");
                return candles;
            }
            return candles;
        }


        /// <summary>
        /// returns whether currency is allowed
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private bool IsValidCurrency(string symbol)
        {
            if (_settings.ETH && symbol.ToStringLowerInvariant().Contains("eth")) return true;
            if (_settings.EUR && symbol.ToStringLowerInvariant().Contains("eur")) return true;
            if (_settings.USD && symbol.ToStringLowerInvariant().Contains("usd")) return true;
			if (_settings.BTC && symbol.ToStringLowerInvariant().Contains("btc")) return true;
            if (_settings.BTC && symbol.ToStringLowerInvariant().Contains("xbt")) return true;
            if (_settings.BNB && symbol.ToStringLowerInvariant().Contains("bnb")) return true;
            return false;
        }

        /// <summary>
        /// List of symbols
        /// </summary>
        /// <value>The symbols.</value>
        public List<ExtendedSymbol> Symbols {
			get {
				return _symbols;
			}
		}


        public string GetHyperTradeURI(ExchangeMarket symbol, int minutes) {
            string urlSymbol = $"{symbol.BaseCurrency}-{symbol.QuoteCurrency}";
            string exchange = _settings.Exchange.ToLowerInvariant();
            return $"hypertrader://{exchange}/{urlSymbol}/{minutes}";
        }

        public void Dispose()
        {
            if (_api != null)
            {
                _api.Dispose();
                _api = null;
            }
        }

        public decimal OneHourTrend { get; private set; }
        public decimal FourHourTrend { get; private set; }

        public void CalculateSymbolTrends() {
            oneHourTrendAverage.Clear();
            fourHourTrendAverage.Clear();

            foreach (ExtendedSymbol symbol in _symbols) {
				CalculateSymbolTrend(symbol);
                fourHourTrendAverage.Add(symbol.Trends[0].TrendRaw, (decimal)symbol.Trends[0].Candle.QuoteCurrencyVolume);
                oneHourTrendAverage.Add(symbol.Trends[1].TrendRaw, (decimal)symbol.Trends[1].Candle.QuoteCurrencyVolume);
			}

            OneHourTrend = oneHourTrendAverage.Result() / 100M;
            FourHourTrend = fourHourTrendAverage.Result() / 100M;
        }

		private void CalculateSymbolTrend(ExtendedSymbol symbol) {
			for (int i = 0; i < symbol.Trends.Length; i++) {
				decimal diff = symbol.Trends[i].Candle.ClosePrice - symbol.Trends[i].Candle.OpenPrice;
                decimal trendPercentage = (diff / symbol.Trends[i].Candle.OpenPrice) * 100M;
                symbol.Trends[i].TrendRaw = trendPercentage;
			}
		}

        /// <summary>
        /// Multiplies the signal timeframe it need to cool down before a new signal is thrown
        /// </summary>
        private const double SignalCooldownMultiplier = 2;
        

        private bool IsSignalWithinCooldown(ExtendedSymbol symbol, int minutes) {
            if (!symbol.SignalTimestamps.ContainsKey(minutes)) {
                return false;
			}
            TimeSpan timeSinceLastSignal = DateTime.UtcNow - symbol.SignalTimestamps[minutes];
            return timeSinceLastSignal.TotalSeconds < (minutes * SignalCooldownMultiplier) * 60;
        }

        private void RecordSignalTimestamp(ExtendedSymbol symbol, int minutes) {
            if (!symbol.SignalTimestamps.ContainsKey(minutes)) {
                symbol.SignalTimestamps.Add(minutes, DateTime.UtcNow);
            } else {
                symbol.SignalTimestamps[minutes] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Performs a scan for all filtered symbols
        /// </summary>
        /// <returns></returns>
        public async Task<Signal> ScanSymbolsAsync(ExtendedSymbol symbol, int minutes)
        {
            try
            {
                Dictionary<int, List<MarketCandle>> symbolCandles = symbol.TimeframeCandles;
                List<MarketCandle> candles = symbolCandles[minutes];
                
                // scan candles for buy/sell signal
                TradeType tradeType = TradeType.Long;

				var strategy = new DayTradingStrategy(symbol.Symbol.MarketSymbol, _settings);
                if (strategy.IsValidEntry(candles, 0, out tradeType, out decimal bandwitdh))
                {
                    // ignore signals for shorts when not allowed
                    if (tradeType == TradeType.Short && !_settings.AllowShorts) return null;

                    // ignore if this signal is within the cooldown period of the last thrown signal
                    if (IsSignalWithinCooldown(symbol, minutes)) return null;
                    RecordSignalTimestamp(symbol, minutes);


                    // got buy/sell signal.. write to console
                    int beepFrequency = symbol.Trends[1].Trend > 0 ? 1000 : 500;
                    Console.Beep(beepFrequency, 200);
                    
                    return new Signal() {
                        Symbol = symbol.Symbol.MarketSymbol,
                        Trade = tradeType.ToString(),
                        Date = $"{candles[0].Timestamp.ToLocalTime():dd-MM-yyyy HH:mm}",
                        TimeFrame = $"{minutes} min",
                        HyperTraderURI = GetHyperTradeURI(symbol.Symbol, minutes),
                        Volume = symbol.Ticker.Volume,
                        FourHourTrend = String.Format("{00:P2}", symbol.Trends[0].Trend),
                        OneHourTrend = String.Format("{00:P2}", symbol.Trends[1].Trend),
                        FourHourTrendObject = symbol.Trends[0],
                        OneHourTrendObject = symbol.Trends[1],
                        BBBandwidth = String.Format("{0:0.0#}", bandwitdh)
                    };
                }
            }
            catch (Exception ex)
            {
				System.Diagnostics.Trace.WriteLine(ex);
            }
            return null;
        }

		private List<MarketCandle> AddMissingCandles(List<MarketCandle> candles, int minutes)
		{
			if (candles.Count <= 0) return candles;

			var result = new List<MarketCandle>();
			result.Add(candles[0]);
			var timeStamp = candles[0].Timestamp;
			for (int i = 1; i < candles.Count;++i)
			{
				var nextCandle = candles[i];
				var mins = (timeStamp - nextCandle.Timestamp).TotalMinutes;
				while (mins > minutes)
				{
					result.Add(new MarketCandle()
					{
						OpenPrice = nextCandle.OpenPrice,
						ClosePrice = nextCandle.ClosePrice,
						HighPrice = nextCandle.HighPrice,
						LowPrice = nextCandle.LowPrice,
						Timestamp = timeStamp.AddMinutes(-minutes)
					});
					mins -= minutes;
				}
                result.Add(nextCandle);
				timeStamp = nextCandle.Timestamp;
            }

			return result;
		}


        public static int TimeframeToMinutes(string timeframe) {
            int minutes = 5;
            switch (timeframe.ToLowerInvariant()) { //_settings.TimeFrame
                case "1 min":
                    minutes = 1;
                    break;

                case "3 min":
                    minutes = 3;
                    break;

                case "5 min":
                    minutes = 5;
                    break;

                case "15 min":
                    minutes = 15;
                    break;

                case "30 min":
                    minutes = 30;
                    break;

                case "1 hr":
                    minutes = 60;
                    break;

                case "4 hr":
                    minutes = 4 * 60;
                    break;
            }
            return minutes;
        }

        public static string PeriodToKlineTimeframe(int minutes) {
            string klineTimeframe = "1m";
            switch (minutes) { //_settings.TimeFrame
                case 1:
                    klineTimeframe = "1m";
                    break;

                case 3:
                    klineTimeframe = "3m";
                    break;

                case 5:
                    klineTimeframe = "5m";
                    break;

                case 15:
                    klineTimeframe = "15m";
                    break;

                case 30:
                    klineTimeframe = "30m";
                    break;

                case 60:
                    klineTimeframe = "1h";
                    break;

                case 240:
                    klineTimeframe = "4h";
                    break;
            }
            return klineTimeframe;
        }

    }

}