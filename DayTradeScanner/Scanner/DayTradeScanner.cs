using DayTradeScanner.Bot.Implementation;
using ExchangeSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DayTradeScanner
{
    public class ExtendedSymbol {
        public ExchangeMarket Symbol;
        public ExchangeTicker Ticker; 
    }
    public class Scanner
    {
        private Settings _settings;
        private ExchangeAPI _api;
        private List<ExtendedSymbol> _symbols = new List<ExtendedSymbol>();

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

                // add to list
                ExtendedSymbol extendedSymbol = new ExtendedSymbol() {
                    Symbol = metadata,
                    Ticker = ticker
                };
                _symbols.Add(extendedSymbol);
            }
            _symbols = _symbols.OrderBy(e => e.Symbol.MarketSymbol).ToList();
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
                if (strategy.IsValidEntry(candles, 0, out tradeType))
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
                        Volume = volume
                    };
                }
            }
            catch (Exception )
            {
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