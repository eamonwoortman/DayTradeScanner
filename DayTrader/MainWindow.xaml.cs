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

namespace DayTrader
{
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
            Signals = new ObservableCollection<Signal>();

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
                            Signals.Insert(0, signal);
                        }));
                    }
                    if (!_running) break;
                }
                if (!_running) break;
            }
        }

        private async void DoScan()
		{
            var settings = SettingsStore.Load();

            SetStatusText($"initializing {settings.Exchange} with 24hr volume of {settings.Min24HrVolume} ...");
            _scanner = new Scanner(settings);
            int numTimeFrames = settings.TimeFrames.Length;
            string[] orderedTimeframes = settings.TimeFrames.OrderBy(x => Scanner.TimeframeToMinutes(x)).ToArray();
            while (_running) {

                if (_scanner.ShouldFetchTrendCandles()) {
                    SetStatusText($"Fetching trend candles.");
                    await _scanner.FetchTrendCandles();
                    _scanner.CalculateSymbolTrends();
                    Thread.Sleep(1000);
                } else {
                    await ScanSymbols(orderedTimeframes, settings);
                    _scanner.CalculateSymbolTrends();
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

        public ObservableCollection<Signal> Signals { get; set; }

 
		private void SignalButton_Clicked(object sender, RoutedEventArgs e) {
            Button cmd = (Button)sender;
            if (cmd.DataContext is Signal) {
                Signal signal = (Signal)cmd.DataContext;
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
}