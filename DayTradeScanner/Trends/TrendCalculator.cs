using ExchangeSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DayTradeScanner
{	
	public static class TrendCalculator
	{
		public static decimal? CalculateTrendPercent(
			List<MarketCandle> candles,
			TimeSpan timeframe,
			int windowSize,
			DateTime utcNow)
		{
			if (candles.Count < 3)
			{
				return null;
			}

			// Simple calculation: (last close - first close) / first close * 100%				
			var lastClose = candles[1].ClosePrice;
			var previousLastClose = candles[2].ClosePrice;
			if (previousLastClose <= 0)
				return null;
			var trendPct = (lastClose - previousLastClose) / previousLastClose * 100.0m;
			return trendPct;
#if false
			// 1. Filter to CLOSED candles only
			var closedCandles = candles
				.Where(c => c.IsClosed)
				.ToList();

			if (closedCandles.Count < windowSize)
				return null;

			// 2. Take last N closed candles
			var window = closedCandles
				.Skip(closedCandles.Count - windowSize)
				.ToList();

#if false
			// 3. Extract close prices
			var prices = window.Select(c => (double)c.ClosePrice).ToArray();

			// 4. Linear regression: y = a + b * t
			int n = prices.Length;
			double sumT = 0, sumY = 0, sumTT = 0, sumTY = 0;

			for (int t = 0; t < n; t++)
			{
				sumT += t;
				sumY += prices[t];
				sumTT += t * t;
				sumTY += t * prices[t];
			}

			double slope = (n * sumTY - sumT * sumY) /
							(n * sumTT - sumT * sumT);

			double intercept = (sumY - slope * sumT) / n;

			// 5. Convert slope into percentage trend
			double startPrice = intercept;
			double endPrice = intercept + slope * (n - 1);
#else

			double startPrice = (double)window.First().ClosePrice;
			double endPrice = (double)window.Last().ClosePrice;
#endif

			if (startPrice <= 0)
				return null;

			double trendPct = (endPrice - startPrice) / startPrice * 100.0;

			return (decimal)trendPct;
#endif
		}
	}

}
