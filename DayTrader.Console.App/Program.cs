using DayTradeScanner;
using System;
using System.Threading;

namespace DayTrader.ConsoleApp
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var settings = new Settings();
			settings.USD = true;
			settings.Exchange = "Bitfinex";
			var scanner = new Scanner(settings);
			while (true)
			{
				Console.WriteLine("scanning...");
				foreach (var symbol in scanner.Symbols)
				{
					var task = scanner.ScanAsync(symbol);
					task.Wait();
					var signal = task.Result;
					if (signal != null)
					{
						Console.WriteLine($"{signal.Symbol} {signal.Trade}");
					}
				}
				Console.WriteLine("wait...");
				Thread.Sleep(5000);
			}
		}
	}
}