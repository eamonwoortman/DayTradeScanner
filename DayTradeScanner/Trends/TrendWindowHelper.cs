using System;

namespace DayTraderScanner
{
	public static class TrendWindowHelper
	{
		public static int CalculateWindowSize(
			TrendDefinition definition,
			TimeSpan timeframe)
		{
			if (timeframe <= TimeSpan.Zero)
				throw new ArgumentException("Invalid timeframe");

			int rawWindow =
				(int)Math.Floor(
					definition.LookbackDuration.TotalSeconds /
					timeframe.TotalSeconds);

			if (rawWindow < definition.MinCandles)
				rawWindow = definition.MinCandles;

			if (definition.MaxCandles.HasValue &&
				rawWindow > definition.MaxCandles.Value)
				rawWindow = definition.MaxCandles.Value;

			return rawWindow;
		}
	}

}
