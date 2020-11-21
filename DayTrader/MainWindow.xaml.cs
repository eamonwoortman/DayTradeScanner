using System.Windows;
using DayTradeScanner;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.ComponentModel;
using ExchangeSharp;
using SharpDX;
using Newtonsoft.Json.Linq;

namespace DayTrader
{
    public static class ColorHelper
    {
        public static Color Lerp(Color from, Color to, decimal weight)
        {
            Color startColor = from;
            Color endColor = to;

            return Color.FromRgb(
                (byte)Math.Round(startColor.R * (1 - weight) + endColor.R * weight),
                (byte)Math.Round(startColor.G * (1 - weight) + endColor.G * weight),
                (byte)Math.Round(startColor.B * (1 - weight) + endColor.B * weight));

        }
    }
    public class PercentageToBrushConverter : IValueConverter
    {
        static readonly Color RedColor = Color.FromRgb(244, 67, 54);
        static readonly Color GreenColor = Color.FromRgb(0, 200, 83);
        static readonly Color Black = Color.FromRgb(0, 0, 0);
        static readonly Color White = Color.FromRgb(255, 255, 255);

        public static SolidColorBrush GetBrushFromPercentage(decimal percentage)
        {
            decimal strength = Math.Min(Math.Abs(percentage) / 2m, 1);
            Color brushColor = new Color();
            if (percentage < 0)
            {
                brushColor = RedColor;
            }
            else
            {
                brushColor = GreenColor;
            }
            Color lerpedColor = ColorHelper.Lerp(White, brushColor, strength);
            return new SolidColorBrush(lerpedColor);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string percentageString = (string)value;
            if (string.IsNullOrEmpty(percentageString))
            {
                return SystemColors.AppWorkspaceColor;
            }

            string decimalString = percentageString.Split("%")[0];
            decimal decimalValue = System.Convert.ToDecimal(decimalString);
            return GetBrushFromPercentage(decimalValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new InvalidOperationException("NumberToBooleanConverter can only be used OneWay.");
        }
    }


    public partial class MainWindow : Window {

        private Scanner _scanner;
        private Button _btnStart;
        private Button _btnDonate;
        private MenuItem _menuItemQuit;
        private MenuItem _menuItemSettings;
        private Thread _thread;
        private bool _running;
        
        public MainWindow() {
            DataContext = this;
            StartButton = "Start scanning";
            TestWsButton = "Start WS";
            StatusText = "stopped...";
            Signals = new ObservableCollection<SignalView>();
            Symbols = new ObservableCollection<SymbolView>();

            InitializeComponent();
            InitializeComponentCustom();
        }

        private void InitializeComponentCustom()
        {

            // this.AttachDevTools();
            _btnStart = (Button)FindName("btnStart");
            _btnStart.Click += btnStart_Click;

            _menuItemQuit = (MenuItem)FindName("menuItemQuit");
            _menuItemQuit.Click += _btnQuit_Click;

            _menuItemSettings = (MenuItem)FindName("menuItemSettings");
            _menuItemSettings.Click += menuItemSettings_Click;

            _btnDonate = (Button)FindName("btnDonate");
            _btnDonate.Click += btnDonate_Click;
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            var url = "http://github.com/erwin-beckers/DayTradeScanner";
            try
            {
                Process.Start(url);
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
        }

        private void _btnQuit_Click(object sender, RoutedEventArgs e)
		{
			StopScanning();
            this.Close();
        }

        private void menuItemSettings_Click(object sender, RoutedEventArgs e)
		{
			StopScanning();
            var dlg = new SettingsDialog();
            dlg.Show();
        }


		private void StopScanning()
		{            
			if (_thread != null)
            {
                cancellationSource.Cancel();
                
                _running = false;
                _thread.Join();
                _thread = null;
                StartButton = "Start scanning";
                StatusText = "stopped...";

                cancellationSource.Dispose();
                cancellationSource = null;
            }
		}

		private void StartScanning()
		{
			StopScanning();
            StartButton = "Stop scanning";
            StatusText = "initializing...";
            _running = true;
            cancellationSource = new CancellationTokenSource();
            _thread = new Thread(new ThreadStart(DoScan));
            _thread.Start();
			
		}

        private void btnStart_Click(object sender, RoutedEventArgs e)
		{
            if (_thread != null)
            {
				StopScanning();
            }
            else
            {
				StartScanning();
            }
        }

        private async Task ScanSymbols(Settings settings) {
            
            foreach (int minutes in _scanner.StrategyPeriodsMinutes) {
                int idx = 0;
                string timeframe = _scanner.GetKlineFromMinutes(minutes);
                foreach (var symbol in _scanner.Symbols) {
                    idx++;
                    SetStatusText($"{settings.Exchange} scanning {symbol.Symbol.MarketSymbol} ({idx}/{_scanner.Symbols.Count}) on {timeframe}...");
                    var signal = await _scanner.ScanSymbolsAsync(symbol, minutes);
                    if (!_running) {
                        return;
                    }
                    if (signal != null) {
                        await Dispatcher.BeginInvoke((Action)(() => {
                            Signals.Insert(0, new SignalView(signal));
                        }));
                    }
                    if (!_running) break;
                }
                await Task.Delay(200);
                if (!_running) break;
            }
        }


        private Dictionary<string, ExtendedSymbol> symbolLookup;
        private async Task FillTrendSymbols()
        {
            symbolLookup = new Dictionary<string, ExtendedSymbol>();

            await Dispatcher.BeginInvoke((Action)(() => {
                Symbols.Clear();
                foreach(ExtendedSymbol extendedSymbol in _scanner.Symbols)
                {
                    symbolLookup.Add(extendedSymbol.MarketSymbol, extendedSymbol);
                    Symbols.Add(new SymbolView(extendedSymbol));
                }
            }));
        }


        private CancellationTokenSource cancellationSource;
        private async Task FetchInitialCandles(Settings settings) {
            int idx = 0;
            Task[] tasks = new Task[_scanner.PeriodsMinutes.Count];
            foreach (int periodMinutes in _scanner.PeriodsMinutes) {
                Task task = Task.Run(async () => {
                    if (!_running) {
                        return;
                    }

                    string klineTimeframe = _scanner.GetKlineFromMinutes(periodMinutes);
                    SetStatusText($"{settings.Exchange} fetching initial candles for timeframe: {klineTimeframe}...");
                    await _scanner.GetInitialCandles(periodMinutes, cancellationSource.Token);
                    
                });

                tasks[idx] = task;
                idx++;
                if (!_running) break;
            }
            await Task.WhenAll(tasks);
        }


        private async Task InitializeWebsocket() {
            SetStatusText($"Starting websocket...");
            wsApi = new CustomBinanceAPI();
            string[] symbols = GetKlineSymbols();
            tickerWebSocket = await wsApi.GetCandlesTimeFrameWebSocketAsync((IReadOnlyCollection<KeyValuePair<string, MarketCandle>> result) => {
                foreach (KeyValuePair<string, MarketCandle> pair in result) {
                    int minutes = pair.Value.PeriodSeconds / 60;
                    MarketCandle candle = pair.Value;
                    // either update the close price or insert a new candle
                    if (symbolLookup[pair.Key].TimeframeCandles[minutes][0].Timestamp == candle.Timestamp) {
                        symbolLookup[pair.Key].TimeframeCandles[minutes][0].ClosePrice = candle.ClosePrice;
                    } else {
                        symbolLookup[pair.Key].TimeframeCandles[minutes].Insert(0, candle);

                        if (symbolLookup[pair.Key].TimeframeCandles[minutes].Count > Scanner.MaxCandlesPerTimeframe) {
                            symbolLookup[pair.Key].TimeframeCandles[minutes].RemoveAt(symbolLookup[pair.Key].TimeframeCandles[minutes].Count - 1);
                        }
                    }
                    symbolLookup[pair.Key].NotifyCandleUpdate(minutes);
                }
            }, symbols);
            //tickerWebSocket.Connected += TickerWebSocket_Connected;
            tickerWebSocket.Disconnected += TickerWebSocket_Disconnected;
        }


        private async void DoScan() {
            var settings = SettingsStore.Load();

            SetStatusText($"initializing {settings.Exchange} with 24hr volume of {settings.Min24HrVolume} ...");
            _scanner = new Scanner(settings);

            try {
                await _scanner.Initialize();
                await FillTrendSymbols();
                await FetchInitialCandles(settings);
                await InitializeWebsocket();
            } catch (Exception ex) {
                Console.WriteLine($"[DoScan] caught error: {ex}");
			}

            while (_running) {
                await ScanSymbols(settings);

                if (!_running) break;
                SetStatusText($"sleeping.");
				Thread.Sleep(1000);
                
				if (!_running) break;
				SetStatusText($"sleeping..");
				Thread.Sleep(1000);

				if (!_running) break;
				SetStatusText($"sleeping...");
                Thread.Sleep(1000);
			}

            DisposeScanner();
            DisposeWebsocket();
        }

        private void SetStatusText(string statusTxt)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                StatusText = statusTxt;
            }));
        }

        public static readonly DependencyProperty StartButtonProperty = DependencyProperty.Register("StartButton", typeof(string), typeof(MainWindow));


        public string StartButton
        {
            get { return (string)this.GetValue(StartButtonProperty); }
            set { this.SetValue(StartButtonProperty, value); }
        }

        public static readonly DependencyProperty TestButtonProperty = DependencyProperty.Register("TestWsButton", typeof(string), typeof(MainWindow));
        public string TestWsButton {
            get { return (string)this.GetValue(TestButtonProperty); }
            set { this.SetValue(TestButtonProperty, value); }
        }

        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register("StatusText", typeof(string), typeof(MainWindow));
        public string StatusText
        {
            get { return (string)this.GetValue(StatusProperty); }
            set { this.SetValue(StatusProperty, value); }
        }

        public ObservableCollection<SignalView> Signals { get; set; }
            
        public ObservableCollection<SymbolView> Symbols { get; set; }

        public static readonly DependencyProperty FourHourTrendProperty = DependencyProperty.Register("FourHourTrendText", typeof(decimal), typeof(MainWindow));
        public decimal FourHourTrendText
        {
            get { return (decimal)this.GetValue(FourHourTrendProperty); }
            set { this.SetValue(FourHourTrendProperty, value); }
        }

        public static readonly DependencyProperty OneHourTrendProperty = DependencyProperty.Register("OneHourTrendText", typeof(decimal), typeof(MainWindow));
        public decimal OneHourTrendText
        {
            get { return (decimal)this.GetValue(OneHourTrendProperty); }
            set { this.SetValue(OneHourTrendProperty, value); }
        }


        private void SignalButton_Clicked(object sender, RoutedEventArgs e) {
            Button cmd = (Button)sender;
            if (cmd.DataContext is SignalView) {
                SignalView signal = (SignalView)cmd.DataContext;
                string hypertraderURI = signal.HyperTraderURI;
                StartHyperTrader(hypertraderURI);
            }
        }

        private void StartHyperTrader(string uri) {
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = uri,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

       
        protected override void OnClosing(CancelEventArgs e)
        {
            StopScanning();
            Dispose();
            base.OnClosing(e);
        }

        private void DisposeScanner() {
            if (_scanner != null) {
                _scanner.Dispose();
                _scanner = null;
            }
        }
        private void Dispose() {
            DisposeScanner();
            DisposeWebsocket();
        }

        private CustomBinanceAPI wsApi; 
        private IWebSocket tickerWebSocket;


        private string[] GetKlineSymbols() {
            if (_scanner == null)
                return new string[] { "bnbbtc@kline_1m", "bnbbtc@kline_3m" };

            List<string> klineTimeframes = new List<string>();
            foreach(KeyValuePair<int, string> pair in _scanner.timeframeKlines) {
                string kline = $"kline_{pair.Value}";
                foreach (ExtendedSymbol symbol in _scanner.Symbols) {
                    string symbolKline = $"{symbol.MarketSymbol.ToLowerInvariant()}@{kline}";
                    klineTimeframes.Add(symbolKline);
                }
			}
            return klineTimeframes.ToArray();
        }

		private Task TickerWebSocket_Disconnected(IWebSocket socket) {
            Trace.WriteLine($"Websocket disconnected");
            return null;
        }

        private Task TickerWebSocket_Connected(IWebSocket socket) {
            Trace.WriteLine($"Websocket connected");
            return null;
        }

		private void DisposeWebsocket() {
            
            if (tickerWebSocket != null) {
                tickerWebSocket.Dispose();
                tickerWebSocket = null;
            }

            if (wsApi != null) {
                wsApi.Dispose();
                wsApi = null;
			}
		}
	}

	public class TrendView
    {
        public SolidColorBrush TrendBrush { get; private set; }
        public string TrendString { get; private set; }

        public TrendView(SymbolTrend trend)
        {
            TrendBrush = PercentageToBrushConverter.GetBrushFromPercentage(trend.TrendRaw);
            TrendString = string.Format("{00:P2}", trend.Trend);
        }
    }


    public class SignalView
    {
        public Signal Signal { get; set; }
        public TrendView FourHourTrendView { get; private set; }
        public TrendView OneHourTrendView { get; private set; }
        public SignalView(Signal signal)
        {
            Signal = signal;
            OneHourTrendView = new TrendView(signal.OneHourTrendObject);
            FourHourTrendView = new TrendView(signal.FourHourTrendObject);
        }


        public SolidColorBrush FourHourBrush { get { return PercentageToBrushConverter.GetBrushFromPercentage(Signal.FourHourTrendObject.TrendRaw); } }
        public SolidColorBrush OneHourBrush { get { return PercentageToBrushConverter.GetBrushFromPercentage(Signal.OneHourTrendObject.TrendRaw); } }
        public string FourHourTrend { get { return string.Format("{00:P2}", Signal.FourHourTrendObject.Trend); } }
        public string OneHourTrend { get { return string.Format("{00:P2}", Signal.OneHourTrendObject.Trend); } }

        public string Symbol { get { return Signal.Symbol; } }
        public string Trade { get { return Signal.Trade; } }
        public string Date { get { return Signal.Date; } }
        public string TimeFrame { get { return Signal.TimeFrame; } }
        public string HyperTraderURI { get { return Signal.HyperTraderURI; } }
        public ExchangeSharp.ExchangeVolume Volume { get { return Signal.Volume; } }
        public string BBBandwidth { get { return Signal.BBBandwidth; } }

    }
   

    public class SymbolView : INotifyPropertyChanged
    {
        public ExtendedSymbol Symbol { get; set; }
        public TrendView FourHourTrendView { get; private set; }
        public SymbolView (ExtendedSymbol symbol)
        {
            Symbol = symbol;
            FourHourTrendView = new TrendView(symbol.Trends[0]);
			symbol.TimeframeChanged += Symbol_TimeframeChanged; ;
        }

		private void Symbol_TimeframeChanged(object sender, TimeframeChangedEventArgs e) {
            int timeframe = e.TimeframePeriod;
            if (timeframe == 60) {
                updateProperty("OneHourTrend");
                updateProperty("OneHourBrush");
            } else if (timeframe == 240) {
                updateProperty("FourHourTrend");
                updateProperty("FourHourBrush");
            } else {
                updateProperty("timeframe_" + e.TimeframePeriod);
            }
        }

		public string MarketSymbol { get { return Symbol.MarketSymbol; } }

        public SolidColorBrush FourHourBrush { get { return PercentageToBrushConverter.GetBrushFromPercentage(Symbol.Trends[0].TrendRaw); } }
        public SolidColorBrush OneHourBrush { get { return PercentageToBrushConverter.GetBrushFromPercentage(Symbol.Trends[1].TrendRaw); } }
        public string FourHourTrend { get { return string.Format("{00:P2}", Symbol.Trends[0].Trend); } }
        public string OneHourTrend { get { return string.Format("{00:P2}", Symbol.Trends[1].Trend); } }



        //the vaiable must set to ture when update in this calss is ion progress
        private bool sourceUpdating;
        public bool SourceUpdating {
            get { return sourceUpdating; }
            set {
                sourceUpdating = value; updateProperty("SourceUpdating");
            }
        }

        public void updateProperty(string name) {
            if (name == "SourceUpdating") {
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
            } else {
                SourceUpdating = true;
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
                SourceUpdating = false;
            }
        }

		public event PropertyChangedEventHandler PropertyChanged;
	}
}