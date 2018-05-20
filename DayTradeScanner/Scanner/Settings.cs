using MessagePack;

namespace DayTradeScanner
{
	[MessagePackObject]
	public class Settings
	{
		[Key(0)]
		public string Exchange { get; set; }

		[Key(1)]
		public bool USD { get; set; }

		[Key(2)]
		public bool EUR { get; set; }

		[Key(3)]
		public bool ETH { get; set; }

		[Key(4)]
		public bool BNB { get; set; }

		[Key(5)]
		public bool BTC { get; set; }

		[Key(6)]
		public long Min24HrVolume { get; set; }

		[Key(7)]
		public bool AllowShorts { get; set; }

		[Key(8)]
		public double MinBollingerBandWidth { get; set; }

		[Key(9)]
		public int MaxFlatCandles { get; set; }

		[Key(10)]
		public int MaxFlatCandleCount { get; set; }

		[Key(11)]
		public double MaxPanic { get; set; }



		public Settings()
		{
			Exchange = "Bitfinex";
			BTC = true;
			Min24HrVolume = 1000;
			MinBollingerBandWidth = 2;
			AllowShorts = false;
			MaxFlatCandles = 5;
			MaxFlatCandleCount = 15;
			MaxPanic = 5;         
		}
	}
}