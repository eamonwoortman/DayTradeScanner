using System;

namespace DayTraderScanner
{
	public sealed class TrendDefinition
	{
		// Human-readable intent
		public string Name { get; init; }

		// Lookback expressed in time, not candles
		public TimeSpan LookbackDuration { get; init; }

		// Minimum candles required (safety for higher TFs)
		public int MinCandles { get; init; } = 5;

		// Optional cap to avoid huge windows (e.g. 1m data)
		public int? MaxCandles { get; init; }
	}

	public static class TrendDefinitions
	{
		public static readonly TrendDefinition Intraday =
			new TrendDefinition
			{
				Name = "Intraday",
				LookbackDuration = TimeSpan.FromHours(24),
				MinCandles = 20
			};

		public static readonly TrendDefinition Swing =
			new TrendDefinition
			{
				Name = "Swing",
				LookbackDuration = TimeSpan.FromDays(5),
				MinCandles = 10
			};

		public static readonly TrendDefinition Regime =
			new TrendDefinition
			{
				Name = "Regime",
				LookbackDuration = TimeSpan.FromDays(30),
				MinCandles = 20,
				MaxCandles = 500
			};
	}

}
