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
                _running = false;
                _thread.Join();
                _thread = null;
                StartButton = "Start scanning";
                StatusText = "stopped...";
            }
		}

		private void StartScanning()
		{
			StopScanning();
            StartButton = "Stop scanning";
            StatusText = "initializing...";
            _running = true;
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

        private async Task ScanSymbols(string[] orderedTimeframes, Settings settings) {
            foreach (string timeframe in orderedTimeframes) {
                int idx = 0;
                foreach (var symbol in _scanner.Symbols) {
                    idx++;
                    int minutes = Scanner.TimeframeToMinutes(timeframe);
                    SetStatusText($"{settings.Exchange} scanning {symbol.Symbol.MarketSymbol} ({idx}/{_scanner.Symbols.Count}) on {timeframe}...");
                    var signal = await _scanner.ScanAsync(symbol, minutes);
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
                if (!_running) break;
            }
        }

        private async Task<bool> FetchTrendCandles(Settings settings)
        {
            int idx = 0;
            Task[] tasks = new Task[_scanner.Symbols.Count];
            bool wasSuccesful = true;
            foreach (var symbol in _scanner.Symbols)
            {
                Task task = Task.Run(async () => { 
                    SetStatusText($"{settings.Exchange} scanning trend candles for: {symbol.Symbol.MarketSymbol} ({idx + 1}/{_scanner.Symbols.Count})...");

                    bool gotCandles = await _scanner.GetSymbolTrendCandles(symbol);
                    if (!gotCandles) // retry one more time
                    {
                        gotCandles = await _scanner.GetSymbolTrendCandles(symbol);
                    }
                    if (!gotCandles)
                    {
                        wasSuccesful = false;
                    }
                });
                tasks[idx] = task;
                idx++;
                if (!_running) break;
            }
            await Task.WhenAll(tasks);
            _scanner.FinalizeTrendCandlesLookup(wasSuccesful);
            return wasSuccesful;
        }

        private async Task FillTrendSymbols()
        {
            await Dispatcher.BeginInvoke((Action)(() => {
                Symbols.Clear();
                foreach(ExtendedSymbol extendedSymbol in _scanner.Symbols)
                {
                    Symbols.Add(new SymbolView(extendedSymbol));
                }
            }));
        }

        private void UpdateTrendControls()
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                trendDataGrid.Items.Refresh();
                OneHourTrendText = _scanner.OneHourTrend;
                FourHourTrendText = _scanner.FourHourTrend;
            }));
        }

        private async void DoScan()
		{
            var settings = SettingsStore.Load();

            SetStatusText($"initializing {settings.Exchange} with 24hr volume of {settings.Min24HrVolume} ...");
            _scanner = new Scanner(settings);
            await FillTrendSymbols();

            int numTimeFrames = settings.TimeFrames.Length;
            string[] orderedTimeframes = settings.TimeFrames.OrderBy(x => Scanner.TimeframeToMinutes(x)).ToArray();
            while (_running) {

                if (_scanner.ShouldFetchTrendCandles()) {
                    SetStatusText($"Fetching trend candles.");
                    bool gotCandles = await FetchTrendCandles(settings);
                    if (gotCandles)
                    {
                        _scanner.CalculateSymbolTrends();
                        UpdateTrendControls();
                    }
                    Thread.Sleep(1000);
                } else {
                    await ScanSymbols(orderedTimeframes, settings);
                }

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

    public class OldTrendView
    {
        public SolidColorBrush FourHourBrush { get; private set; }
        public SolidColorBrush OneHourBrush { get; private set; }
        public string FourHourTrend { get; private set; }
        public string OneHourTrend { get; private set; }

        public OldTrendView(SymbolTrend[] trends)
        {
            FourHourBrush = PercentageToBrushConverter.GetBrushFromPercentage(trends[0].TrendRaw);
            OneHourBrush = PercentageToBrushConverter.GetBrushFromPercentage(trends[1].TrendRaw);
            FourHourTrend = string.Format("{00:P2}", trends[0].Trend);
            OneHourTrend = string.Format("{00:P2}", trends[1].Trend);
        }
    }

    public class SignalView
    {
        public Signal Signal { get; set; }
        public TrendView FourHourTrendView { get; private set; }
        public SignalView(Signal signal)
        {
            Signal = signal;
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

    public class SymbolView
    {
        public ExtendedSymbol Symbol { get; set; }
        public TrendView FourHourTrendView { get; private set; }
        public SymbolView (ExtendedSymbol symbol)
        {
            Symbol = symbol;
            FourHourTrendView = new TrendView(symbol.Trends[0]);
        }
        public string MarketSymbol { get { return Symbol.MarketSymbol; } }

        public SolidColorBrush FourHourBrush { get { return PercentageToBrushConverter.GetBrushFromPercentage(Symbol.Trends[0].TrendRaw); } }
        public SolidColorBrush OneHourBrush { get { return PercentageToBrushConverter.GetBrushFromPercentage(Symbol.Trends[1].TrendRaw); } }
        public string FourHourTrend { get { return string.Format("{00:P2}", Symbol.Trends[0].Trend); } }
        public string OneHourTrend { get { return string.Format("{00:P2}", Symbol.Trends[1].Trend); } }
    }
}