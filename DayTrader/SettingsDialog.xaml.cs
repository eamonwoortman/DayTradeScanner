using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DayTradeScanner;
using System.Collections.ObjectModel;

namespace DayTrader
{
	public class SettingsDialog : Window
	{
		private DropDown _dropDownExchange;
		private DropDown _dropDownTimeFrame;
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
			TimeFrames.Add("1 min");

			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoaderPortableXaml.Load(this);
			this.AttachDevTools();
			_dropDownExchange = this.Find<DropDown>("dropExchange");
			_dropDownTimeFrame = this.Find<DropDown>("dropTimeFrame");

			var btnSave = this.Find<Button>("btnSave");
			btnSave.Click += BtnSave_Click;

			var btnReset = this.Find<Button>("btnReset");
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
			_dropDownTimeFrame.SelectedIndex = TimeFrames.IndexOf(settings.TimeFrame);
		}

		private void btnReset_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var settings = new Settings();
			Init(settings);
		}

		private void BtnSave_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
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
			settings.TimeFrame = TimeFrames[_dropDownTimeFrame.SelectedIndex];
			SettingsStore.Save(settings);
			this.Close();
		}

		public static readonly AvaloniaProperty<bool> CurrencyUSDProperty = AvaloniaProperty.Register<SettingsDialog, bool>("CurrencyUSD", inherits: true);

		public bool CurrencyUSD
		{
			get { return this.GetValue(CurrencyUSDProperty); }
			set { this.SetValue(CurrencyUSDProperty, value); }
		}

		public static readonly AvaloniaProperty<bool> CurrencyEURProperty = AvaloniaProperty.Register<SettingsDialog, bool>("CurrencyEUR", inherits: true);

		public bool CurrencyEUR
		{
			get { return this.GetValue(CurrencyEURProperty); }
			set { this.SetValue(CurrencyEURProperty, value); }
		}

		public static readonly AvaloniaProperty<bool> CurrencyETHProperty = AvaloniaProperty.Register<SettingsDialog, bool>("CurrencyETH", inherits: true);

		public bool CurrencyETH
		{
			get { return this.GetValue(CurrencyETHProperty); }
			set { this.SetValue(CurrencyETHProperty, value); }
		}

		public static readonly AvaloniaProperty<bool> CurrencyBNBProperty = AvaloniaProperty.Register<SettingsDialog, bool>("CurrencyBNB", inherits: true);

		public bool CurrencyBNB
		{
			get { return this.GetValue(CurrencyBNBProperty); }
			set { this.SetValue(CurrencyBNBProperty, value); }
		}

		public static readonly AvaloniaProperty<bool> CurrencyBTCProperty = AvaloniaProperty.Register<SettingsDialog, bool>("CurrencyBTC", inherits: true);

		public bool CurrencyBTC
		{
			get { return this.GetValue(CurrencyBTCProperty); }
			set { this.SetValue(CurrencyBTCProperty, value); }
		}

		public static readonly AvaloniaProperty<bool> AllowShortsProperty = AvaloniaProperty.Register<SettingsDialog, bool>("AllowShorts", inherits: true);

		public bool AllowShorts
		{
			get { return this.GetValue(AllowShortsProperty); }
			set { this.SetValue(AllowShortsProperty, value); }
		}

		public static readonly AvaloniaProperty<string> VolumeProperty = AvaloniaProperty.Register<SettingsDialog, string>("Volume", inherits: true);

		public string Volume
		{
			get { return this.GetValue(VolumeProperty); }
			set { this.SetValue(VolumeProperty, value); }
		}


		public static readonly AvaloniaProperty<string> BollingerBandWidthProperty = AvaloniaProperty.Register<SettingsDialog, string>("BollingerBandWidth", inherits: true);

		public string BollingerBandWidth
		{
			get { return this.GetValue(BollingerBandWidthProperty); }
			set { this.SetValue(BollingerBandWidthProperty, value); }
		}

		public static readonly AvaloniaProperty<string> MaxFlatCandlesProperty = AvaloniaProperty.Register<SettingsDialog, string>("MaxFlatCandles", inherits: true);

		public string MaxFlatCandles
		{
			get { return this.GetValue(MaxFlatCandlesProperty); }
			set { this.SetValue(MaxFlatCandlesProperty, value); }
		}


		public static readonly AvaloniaProperty<string> MaxFlatCandleCountProperty = AvaloniaProperty.Register<SettingsDialog, string>("MaxFlatCandleCount", inherits: true);

		public string MaxFlatCandleCount
		{
			get { return this.GetValue(MaxFlatCandleCountProperty); }
			set { this.SetValue(MaxFlatCandleCountProperty, value); }
		}

		public static readonly AvaloniaProperty<string> MaxPanicProperty = AvaloniaProperty.Register<SettingsDialog, string>("MaxPanic", inherits: true);

		public string MaxPanic
		{
			get { return this.GetValue(MaxPanicProperty); }
			set { this.SetValue(MaxPanicProperty, value); }
		}
	}
}