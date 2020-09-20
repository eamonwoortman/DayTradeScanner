using System.Windows;
using DayTradeScanner;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace DayTrader
{
	public partial class SettingsDialog : Window
	{
		private ComboBox _dropDownExchange;
		private ListBox _dropDownTimeFrame;
		public ObservableCollection<string> Exchanges { get; set; }
		public ObservableCollection<string> TimeFrames { get; set; }

		public SettingsDialog()
		{
			DataContext = this;
			Exchanges = new ObservableCollection<string>();
			Exchanges.Add("Bitfinex");
			Exchanges.Add("Bittrex");
			Exchanges.Add("Binance");
			Exchanges.Add("GDax");
			Exchanges.Add("HitBTC");
			Exchanges.Add("Kraken");


			TimeFrames = new ObservableCollection<string>();
			TimeFrames.Add("4 hr");
			TimeFrames.Add("1 hr");
			TimeFrames.Add("30 min");
			TimeFrames.Add("15 min");
			TimeFrames.Add("5 min");
			TimeFrames.Add("3 min");
			TimeFrames.Add("1 min");

			InitializeComponent();
			InitializeComponentCustom();
		}

		private void InitializeComponentCustom()
		{
			_dropDownExchange = (ComboBox)FindName("dropExchange");
			_dropDownTimeFrame = (ListBox)FindName("dropTimeFrame");

			var btnSave = (Button)FindName("btnSave");
			btnSave.Click += BtnSave_Click;

			var btnReset = (Button)FindName("btnReset");
			btnReset.Click += btnReset_Click;

			var settings = SettingsStore.Load();
			Init(settings);
		}

		private void Init(Settings settings)
		{
			CurrencyUSD = settings.USD;
			CurrencyETH = settings.ETH;
			CurrencyEUR = settings.EUR;
			CurrencyBNB = settings.BNB;
			CurrencyBTC = settings.BTC;
			AllowShorts = settings.AllowShorts;
			BollingerBandWidth = $"{settings.MinBollingerBandWidth:0.00}";
			MaxPanic = $"{settings.MaxPanic:0.00}";
			MaxFlatCandles = settings.MaxFlatCandles.ToString();
			MaxFlatCandleCount = settings.MaxFlatCandleCount.ToString();

			Volume = settings.Min24HrVolume.ToString();
			_dropDownExchange.SelectedIndex = Exchanges.IndexOf(settings.Exchange);
			//_dropDownTimeFrame.SelectedIndex = TimeFrames.IndexOf(settings.TimeFrame);
			if (settings.TimeFrames == null) {
				settings.TimeFrames = new string[] { "5 min" };
				SettingsStore.Save(settings);
			}
			SetSelectedItems(settings.TimeFrames);
		}


		private void SetSelectedItems(string[] timeframes) {
			_dropDownTimeFrame.SelectedItems.Clear();
			foreach (string timeframe in timeframes) {
				_dropDownTimeFrame.SelectedItems.Add(timeframe);
			}
		}
		

		private void btnReset_Click(object sender, RoutedEventArgs e)
		{
			var settings = new Settings();
			Init(settings);
		}

		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			var settings = SettingsStore.Load();
			settings.USD = CurrencyUSD;
			settings.ETH = CurrencyETH;
			settings.EUR = CurrencyEUR;
			settings.BNB = CurrencyBNB;
			settings.BTC = CurrencyBTC;
			settings.AllowShorts = AllowShorts;

			long l;
			if (long.TryParse(Volume, out l))
			{
				settings.Min24HrVolume = l;
			}

			int i;
			if (int.TryParse(MaxFlatCandles, out i))
			{
				settings.MaxFlatCandles = i;
			}
			if (int.TryParse(MaxFlatCandleCount, out i))
			{
				settings.MaxFlatCandleCount = i;
			}



			double d;
			if (double.TryParse(MaxPanic, out d))
			{
				settings.MaxPanic = d;
			}
			if (double.TryParse(BollingerBandWidth, out d))
			{
				settings.MinBollingerBandWidth = d;
			}
			settings.Exchange = Exchanges[_dropDownExchange.SelectedIndex];
			settings.TimeFrames = SelectedItemsAsString(_dropDownTimeFrame);
			SettingsStore.Save(settings);
			this.Close();
		}

		public static string[] SelectedItemsAsString(ListBox listBox) {
			List<string> results = new List<string>();
			for (int i = 0; i < listBox.SelectedItems.Count; ++i)
				results.Add((string)listBox.SelectedItems[i]);
			return results.ToArray();
		}

		public static readonly DependencyProperty CurrencyUSDProperty = DependencyProperty.Register("CurrencyUSD", typeof(bool), typeof(SettingsDialog));

		public bool CurrencyUSD
		{
			get { return (bool)this.GetValue(CurrencyUSDProperty); }
			set { this.SetValue(CurrencyUSDProperty, value); }
		}

		public static readonly DependencyProperty CurrencyEURProperty = DependencyProperty.Register("CurrencyEUR", typeof(bool), typeof(SettingsDialog));

		public bool CurrencyEUR
		{
			get { return (bool)this.GetValue(CurrencyEURProperty); }
			set { this.SetValue(CurrencyEURProperty, value); }
		}

		public static readonly DependencyProperty CurrencyETHProperty = DependencyProperty.Register("CurrencyETH", typeof(bool), typeof(SettingsDialog));

		public bool CurrencyETH
		{
			get { return (bool)this.GetValue(CurrencyETHProperty); }
			set { this.SetValue(CurrencyETHProperty, value); }
		}

		public static readonly DependencyProperty CurrencyBNBProperty = DependencyProperty.Register("CurrencyBNB", typeof(bool), typeof(SettingsDialog));

		public bool CurrencyBNB
		{
			get { return (bool)this.GetValue(CurrencyBNBProperty); }
			set { this.SetValue(CurrencyBNBProperty, value); }
		}

		public static readonly DependencyProperty CurrencyBTCProperty = DependencyProperty.Register("CurrencyBTC", typeof(bool), typeof(SettingsDialog));

		public bool CurrencyBTC
		{
			get { return (bool)this.GetValue(CurrencyBTCProperty); }
			set { this.SetValue(CurrencyBTCProperty, value); }
		}
		public static readonly DependencyProperty AllowShortsProperty = DependencyProperty.Register("AllowShorts", typeof(bool), typeof(SettingsDialog));

		public bool AllowShorts
		{
			get { return (bool)this.GetValue(AllowShortsProperty); }
			set { this.SetValue(AllowShortsProperty, value); }
		}
		public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register("Volume", typeof(string), typeof(SettingsDialog));

		public string Volume
		{
			get { return (string)this.GetValue(VolumeProperty); }
			set { this.SetValue(VolumeProperty, value); }
		}

		public static readonly DependencyProperty BollingerBandWidthProperty = DependencyProperty.Register("BollingerBandWidth", typeof(string), typeof(SettingsDialog));

		public string BollingerBandWidth
		{
			get { return (string)this.GetValue(BollingerBandWidthProperty); }
			set { this.SetValue(BollingerBandWidthProperty, value); }
		}
		public static readonly DependencyProperty MaxFlatCandlesProperty = DependencyProperty.Register("MaxFlatCandles", typeof(string), typeof(SettingsDialog));

		public string MaxFlatCandles
		{
			get { return (string)this.GetValue(MaxFlatCandlesProperty); }
			set { this.SetValue(MaxFlatCandlesProperty, value); }
		}

		public static readonly DependencyProperty MaxFlatCandleCountProperty = DependencyProperty.Register("MaxFlatCandleCount", typeof(string), typeof(SettingsDialog));

		public string MaxFlatCandleCount
		{
			get { return (string)this.GetValue(MaxFlatCandleCountProperty); }
			set { this.SetValue(MaxFlatCandleCountProperty, value); }
		}
		public static readonly DependencyProperty MaxPanicProperty = DependencyProperty.Register("MaxPanic", typeof(string), typeof(SettingsDialog));

		public string MaxPanic
		{
			get { return (string)this.GetValue(MaxPanicProperty); }
			set { this.SetValue(MaxPanicProperty, value); }
		}
	}
}