#nullable enable
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExchangeSharp.BinanceGroup;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp {
	internal sealed class CustomBinanceAPI : BinanceGroupCommon {
		public override string BaseUrl { get; set; } = "https://api.binance.com/api/v1";
		public override string BaseUrlWebSocket { get; set; } = "wss://stream.binance.com:9443";
		public override string BaseUrlPrivate { get; set; } = "https://api.binance.com/api/v3";
		public override string WithdrawalUrlPrivate { get; set; } = "https://api.binance.com/wapi/v3";
		public override string BaseWebUrl { get; set; } = "https://www.binance.com";

        private const int MaxWebsocketStreams = 1024;

        private void HandleWebsocketResponse(byte[] msg, Action<IReadOnlyCollection<KeyValuePair<string, MarketCandle>>> callback) {
            JToken token = JToken.Parse(msg.ToStringFromUTF8());
            List<KeyValuePair<string, MarketCandle>> tickerList = new List<KeyValuePair<string, MarketCandle>>();
            MarketCandle candle;
            if (!(token is JArray)) {
                candle = ParseKlineWebSocketAsync(token);
                if (candle != null) {
                    tickerList.Add(new KeyValuePair<string, MarketCandle>(candle.Name, candle));
                }
            } else {
                foreach (JToken childToken in token) {
                    candle = ParseKlineWebSocketAsync(childToken);
                    if (candle != null) {
                        tickerList.Add(new KeyValuePair<string, MarketCandle>(candle.Name, candle));
                    }
                }
            }
            if (tickerList.Count != 0) {
                callback(tickerList);
            }
        }

        public Task<IWebSocket> GetCandlesTimeFrameWebSocketAsync(Action<IReadOnlyCollection<KeyValuePair<string, MarketCandle>>> callback, params string[] symbolStreams) {
            if (symbolStreams.Length > MaxWebsocketStreams) {
                Trace.WriteLine($"[GetCandlesTimeFrameWebSocketAsync] Symbol streams ({symbolStreams.Length}) exceeds socket stream limit ({MaxWebsocketStreams})");
                Array.Resize(ref symbolStreams, MaxWebsocketStreams);
            }
            string url = $"/ws/{string.Join("/", symbolStreams)}";
            return ConnectWebSocketAsync(url, async (_socket, msg) => {
                await Task.Run(() => HandleWebsocketResponse(msg, callback));
			});
		}


		private MarketCandle ParseKlineWebSocketAsync(JToken token) {
			string marketSymbol = token["s"].ToStringInvariant();
            JToken kline = token["k"];
			return ParseKlineAsync(kline, marketSymbol, "i", "o", "c", "h", "l", "v", "q", "t", TimestampType.UnixMilliseconds, "x");
		}

        
        internal MarketCandle ParseKlineAsync(JToken token, string marketSymbol,
            object intervalKey, object openKey, object closeKey, object highKey, 
            object lowKey, object baseVolumeKey, object quoteVolumeKey,
            object timestampKey, TimestampType timestampType, object isClosedKey) {
            if (token == null || !token.HasValues) {
                return null;
            }

            decimal close = token[closeKey].ConvertInvariant<decimal>();

            // parse out volumes, handle cases where one or both do not exist
            token.ParseVolumes(baseVolumeKey, quoteVolumeKey, close, out decimal baseCurrencyVolume, out decimal quoteCurrencyVolume);

            // pull out timestamp
            DateTime timestamp = timestampKey == null
                ? CryptoUtility.UtcNow
                : CryptoUtility.ParseTimestamp(token[timestampKey], timestampType);
			JToken openValue = token[openKey];
            if (openValue is JArray) {
                openValue = openValue[0];
            }
			// create the ticker and return it
			decimal open = openValue.ConvertInvariant<decimal>();

			JToken highValue = token[highKey];
            if (highValue is JArray) {
                highValue = highValue[0];
            }
			decimal high = highValue.ConvertInvariant<decimal>();

			JToken lowValue = token[lowKey];
            if (lowValue is JArray) {
                lowValue = lowValue[0];
            }
			decimal low = lowValue.ConvertInvariant<decimal>();
			JToken intervalValue = token[intervalKey];
            if (intervalValue is JArray) {
                intervalValue = intervalValue[0];
            }
			string interval = intervalValue.ConvertInvariant<string>();
			int periodSeconds = IntervalToPeriodSeconds(interval);

            MarketCandle ticker = new MarketCandle {
                Timestamp = timestamp,
                Name = marketSymbol,
                PeriodSeconds = periodSeconds,
                BaseCurrencyVolume = (double)baseCurrencyVolume,
                QuoteCurrencyVolume = (double)quoteCurrencyVolume,
                ClosePrice = close,
                OpenPrice = open,
                HighPrice = high,
                LowPrice = low
            };
            return ticker;
        }


        private int IntervalToPeriodSeconds(string interval) {
            int multiplier = 0;
            char timeIndicator = interval[interval.Length-1];
            string numberString = interval[0..^1];
            int.TryParse(numberString, out int intervalNumber);

            switch(timeIndicator) {
                case 'm':
                    multiplier = 60;
                    break;
                case 'h':
                    multiplier = 60 * 60;
                    break;
                case 'd':
                    multiplier = 60 * 60 * 60;
                    break;
			}

            return intervalNumber * multiplier;
		}
    }

	public partial class ExchangeName { public const string Binance = "Binance"; }

    public static class CustomExchangeExtensions {

        /// <summary>
        /// Parse volume from JToken
        /// </summary>
        /// <param name="token">JToken</param>
        /// <param name="baseVolumeKey">Base currency volume key</param>
        /// <param name="quoteVolumeKey">Quote currency volume key</param>
        /// <param name="last">Last volume value</param>
        /// <param name="baseCurrencyVolume">Receive base currency volume</param>
        /// <param name="quoteCurrencyVolume">Receive quote currency volume</param>
        internal static void ParseVolumes(this JToken token, object baseVolumeKey, object? quoteVolumeKey, decimal last, out decimal baseCurrencyVolume, out decimal quoteCurrencyVolume) {
            // parse out volumes, handle cases where one or both do not exist
            if (baseVolumeKey == null) {
                if (quoteVolumeKey == null) {
                    baseCurrencyVolume = quoteCurrencyVolume = 0m;
                } else {
                    quoteCurrencyVolume = token[quoteVolumeKey].ConvertInvariant<decimal>();
                    baseCurrencyVolume = (last <= 0m ? 0m : quoteCurrencyVolume / last);
                }
            } else {
                baseCurrencyVolume = (token is JObject jObj
                        ? jObj.SelectToken((string)baseVolumeKey)
                        : token[baseVolumeKey]
                    ).ConvertInvariant<decimal>();
                if (quoteVolumeKey == null) {
                    quoteCurrencyVolume = baseCurrencyVolume * last;
                } else {
                    quoteCurrencyVolume = token[quoteVolumeKey].ConvertInvariant<decimal>();
                }
            }
        }
    }
}
