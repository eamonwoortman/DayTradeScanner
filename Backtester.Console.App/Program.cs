using DayTradeScanner;
using DayTradeScanner.Backtest;
using DayTradeScanner.Bot.Implementation;
using ExchangeSharp;
using System;
using System.Runtime;

namespace Backtester.Console.App
{
    internal class Program
    {
        private static void Main(string[] args)
        {
			var startTime = new DateTime(2018, 5, 14, 0, 0, 0);
			var settings = new Settings();
				
            // create a virtual trade manager
            var virtualTradeManager = new VirtualTradeManager();

            // create day trading strategy
            var strategy = new DayTradingStrategy("ETHBTC", settings);

            // create new backtester
            var tester = new BackTester();

			// test the strategy on bitfinex
			var exchangeApi = ExchangeAPI.GetExchangeAPIAsync("Bitfinex").Result;
			tester.Test(exchangeApi, strategy, startTime);
        }
    }
}