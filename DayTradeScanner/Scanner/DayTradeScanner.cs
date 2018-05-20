using DayTradeScanner.Bot.Implementation;
using ExchangeSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DayTradeScanner
{
    public class Scanner
    {
        private Settings _settings;
        private ExchangeAPI _api;
        private List<string> _symbols = new List<string>();
		private int _minutes;

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

                case "gdax":
                    _api = new ExchangeGdaxAPI();
                    break;

                case "hitbtc":
                    _api = new ExchangeHitbtcAPI();
                    break;

                default:
                    Console.WriteLine($"Unknown exchange:{_settings.Exchange}");
                    return;
            }

			switch(_settings.TimeFrame.ToLowerInvariant())
			{
				case "1 min":
					_minutes = 1;
					break;

                case "5 min":
                    _minutes = 5;
					break;

                case "15 min":
                    _minutes = 15;
					break;

                case "30 min":
                    _minutes = 30;
					break;

                case "1 hr":
                    _minutes = 60;
					break;

                case "4 hr":
                    _minutes = 4*60;
                    break;
			}

            FindCoinsWithEnoughVolume();
        }

        /// <summary>
        /// Downloads all symbols from the exchanges and filters out the coins with enough 24hr Volume
        /// </summary>
        /// <returns></returns>
        private void FindCoinsWithEnoughVolume()
        {
            _symbols = new List<string>();
            var allSymbols = _api.GetSymbols();

            var allTickers = _api.GetTickers();

            // for each symbol
            foreach (var symbol in allSymbols)
            {
                if (!IsValidCurrency(symbol))
                {
                    // ignore, symbol has wrong currency
                    continue;
                }

                // check 24hr volume
                var ticker = allTickers.FirstOrDefault(e => e.Key == symbol).Value;
				var volume = ticker.Volume.ConvertedVolume;
				if (_api is ExchangeBittrexAPI)
				{
					// bittrex reports wrong volume :-(
					volume = ticker.Volume.BaseVolume;
				}

				if (volume < _settings.Min24HrVolume)
                {
                    // ignore since 24hr volume is too low
                    continue;
                }

                // add to list
                _symbols.Add(symbol);
            }
            _symbols = _symbols.OrderBy(e => e).ToList();
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
        public List<string> Symbols
        {
            get
            {
                return _symbols;
            }
        }

        /// <summary>
        /// Performs a scan for all filtered symbols
        /// </summary>
        /// <returns></returns>
        public async Task<Signal> ScanAsync(string symbol)
        {
            try
            {
                // download the new candles
				var candles = (await _api.GetCandlesAsync(symbol, 60 * _minutes, DateTime.Now.AddMinutes(-5 * 50))).Reverse().ToList();
				candles = AddMissingCandles(candles);
                // scan candles for buy/sell signal
                TradeType tradeType = TradeType.Long;
				var strategy = new DayTradingStrategy(symbol, _settings);
                if (strategy.IsValidEntry(candles, 0, out tradeType))
                {
                    // ignore signals for shorts when not allowed
                    if (tradeType == TradeType.Short && !_settings.AllowShorts) return null;

					// got buy/sell signal.. write to console
					Console.Beep();
                    return new Signal()
                    {
                        Symbol = symbol,
                        Trade = tradeType.ToString(),
                        Date = $"{candles[0].Timestamp.AddHours(2):dd-MM-yyyy HH:mm}"
                    };
                }
            }
            catch (Exception )
            {
            }
            return null;
        }

		private List<MarketCandle> AddMissingCandles(List<MarketCandle> candles)
		{
			if (candles.Count <= 0) return candles;

			var result = new List<MarketCandle>();
			result.Add(candles[0]);
			var timeStamp = candles[0].Timestamp;
			for (int i = 1; i < candles.Count;++i)
			{
				var nextCandle = candles[i];
				var mins = (timeStamp - nextCandle.Timestamp).TotalMinutes;
				while (mins > _minutes)
				{
					result.Add(new MarketCandle()
					{
						OpenPrice = nextCandle.OpenPrice,
						ClosePrice = nextCandle.ClosePrice,
						HighPrice = nextCandle.HighPrice,
						LowPrice = nextCandle.LowPrice,
						Timestamp = timeStamp.AddMinutes(-_minutes)
					});
					mins -= _minutes;
				}
                result.Add(nextCandle);
				timeStamp = nextCandle.Timestamp;
            }

			return result;
		}
	}
}