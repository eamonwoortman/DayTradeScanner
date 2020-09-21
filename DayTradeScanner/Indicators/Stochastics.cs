﻿using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeSharp;

namespace DayTradeScanner
{
	public class Stochastics
	{
		public Stochastics(List<MarketCandle> candles, int candle, int length = 14)
		{
			K = GetK(candles, candle + 0, length);
			var K2 = GetK(candles, candle + 1, length);
			var K3 = GetK(candles, candle + 2, length);

			D = (K + K2 + K3) / 3.0m;
		}

		private decimal GetK(List<MarketCandle> candles, int start, int length)
		{
			var prices = candles.Skip(start).Take(length).ToList();
			var lowest = prices.Min(e => e.LowPrice);
			var highest = prices.Max(e => e.HighPrice);
			var denominator = (highest - lowest);
			if (denominator == 0) {
				return 0m;
			} 
			var closePrice = prices[0].ClosePrice;
			return 100m * ((closePrice - lowest) / denominator);
		}

		public decimal K { get; internal set; }
		public decimal D { get; internal set; }
	}
}
