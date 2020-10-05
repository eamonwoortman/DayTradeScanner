using Avalonia.Controls;
using DayTradeScanner.Bot.Implementation;
using ExchangeSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    public class ExtendedSymbol {
        public ExchangeMarket Symbol { get; set; }
        public string MarketSymbol { get { return Symbol.MarketSymbol; } }
        public ExchangeTicker Ticker { get; set; }
        public SymbolTrend[] Trends { get; set; }
    }
    public class Scanner
    {
        private Settings _settings;
        private ExchangeAPI _api;
        private List<ExtendedSymbol> _symbols = new List<ExtendedSymbol>();
        private SimpleWeightedAverage fourHourTrendAverage = new SimpleWeightedAverage();
        private SimpleWeightedAverage oneHourTrendAverage = new SimpleWeightedAverage();

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

            FindCoinsWithEnoughVolume();
        }

        /// <summary>
        /// Downloads all symbols from the exchanges and filters out the coins with enough 24hr Volume
        /// </summary>
        /// <returns></returns>
        private void FindCoinsWithEnoughVolume()
        {
            _symbols = new List<ExtendedSymbol>();
            var allSymbolsMeta = _api.GetMarketSymbolsMetadataAsync().Result;
            var allTickers = _api.GetTickersAsync().Result;

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

                SymbolTrend fourHourTrend = new SymbolTrend(4);
                SymbolTrend oneHourTrend = new SymbolTrend(1); 

                // add to list
                ExtendedSymbol extendedSymbol = new ExtendedSymbol() {
                    Symbol = metadata,
                    Ticker = ticker,
                    Trends = new SymbolTrend[2]
                };

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


        public async Task<bool> GetSymbolTrendCandles(ExtendedSymbol symbol) {
            try
            {
                List<MarketCandle> candles = await GetTrendCandles(symbol.Symbol.MarketSymbol);
                symbol.Trends[0].Candle = CalculateFourHourCandle(candles);
                symbol.Trends[1].Candle = candles[0];
            } catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
            return symbol.Trends[0].Candle != null && symbol.Trends[1].Candle != null;

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

        private DateTime nextFetchCandlesTime;
        public bool ShouldFetchTrendCandles() {
            if (DateTime.UtcNow > nextFetchCandlesTime) {
                return true;
			}
            return false;
		}


        public void FinalizeTrendCandlesLookup(bool wasSuccesful)
        {
            // schedule the next time
            if (wasSuccesful)
            {
                SymbolTrend firstTrend = _symbols[0].Trends[0];
                MarketCandle candle = firstTrend.Candle;
                nextFetchCandlesTime = candle.Timestamp.AddHours(firstTrend.TimeframeInHours);
            } else
            {
                nextFetchCandlesTime = DateTime.UtcNow.AddMinutes(5);
            }
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
		/// Performs a scan for all filtered symbols
		/// </summary>
		/// <returns></returns>
		public async Task<Signal> ScanAsync(ExtendedSymbol symbol, int minutes)
        {
            try
            {
                // download the new candles
                var candles = (await _api.GetCandlesAsync(symbol.Symbol.MarketSymbol, 60 * minutes, DateTime.Now.AddMinutes(-5 * 50))).Reverse().ToList();
                candles = AddMissingCandles(candles, minutes);
                // scan candles for buy/sell signal
                TradeType tradeType = TradeType.Long;

				var strategy = new DayTradingStrategy(symbol.Symbol.MarketSymbol, _settings);
                if (strategy.IsValidEntry(candles, 0, out tradeType, out decimal bandwitdh))
                {
                    // ignore signals for shorts when not allowed
                    if (tradeType == TradeType.Short && !_settings.AllowShorts) return null;

					// got buy/sell signal.. write to console
					Console.Beep();

                    ExchangeVolume volume = symbol.Ticker.Volume;

                    return new Signal() {
                        Symbol = symbol.Symbol.MarketSymbol,
                        Trade = tradeType.ToString(),
                        Date = $"{candles[0].Timestamp.AddHours(2):dd-MM-yyyy HH:mm}",
                        TimeFrame = $"{minutes} min",
                        HyperTraderURI = GetHyperTradeURI(symbol.Symbol, minutes),
                        Volume = volume,
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
	}
}