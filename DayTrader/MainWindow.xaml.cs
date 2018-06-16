using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DayTradeScanner;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace DayTrader
{
    public class MainWindow : Window
    {
        private Scanner _scanner;
        private Button _btnStart;
        private Button _btnDonate;
        private MenuItem _menuItemQuit;
        private MenuItem _menuItemSettings;
        private Thread _thread;
        private bool _running;

        public MainWindow()
        {
            DataContext = this;
            StartButton = "Start scanning";
            StatusText = "stopped...";
            Signals = new ObservableCollection<Signal>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoaderPortableXaml.Load(this);
            // this.AttachDevTools();
            _btnStart = this.Find<Button>("btnStart");
            _btnStart.Click += btnStart_Click;

            _menuItemQuit = this.Find<MenuItem>("menuItemQuit");
            _menuItemQuit.Click += _btnQuit_Click;

            _menuItemSettings = this.Find<MenuItem>("menuItemSettings");
            _menuItemSettings.Click += menuItemSettings_Click;

            _btnDonate = this.Find<Button>("btnDonate");
            _btnDonate.Click += btnDonate_Click;
        }

        private void btnDonate_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
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

        private void _btnQuit_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			StopScanning();
            this.Close();
        }

        private void menuItemSettings_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
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

        private void btnStart_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
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

        private async void DoScan()
		{
            var settings = SettingsStore.Load();

            SetStatusText($"initializing {settings.Exchange} with 24hr volume of {settings.Min24HrVolume} ...");
            _scanner = new Scanner(settings);
            while (_running)
            {
                int idx = 0;
                foreach (var symbol in _scanner.Symbols)
                {
                    idx++;
					SetStatusText($"{settings.Exchange} scanning {symbol} ({idx}/{_scanner.Symbols.Count})...");
                    var signal = await _scanner.ScanAsync(symbol);
                    if (signal != null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Signals.Insert(0, signal);
                        });
                    }
	                if (!_running) break;
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
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = statusTxt;
            });
        }

        public static readonly AvaloniaProperty<string> StartButtonProperty =
        AvaloniaProperty.Register<MainWindow, string>("StartButton", inherits: true);

        public string StartButton
        {
            get { return this.GetValue(StartButtonProperty); }
            set { this.SetValue(StartButtonProperty, value); }
        }

        public static readonly AvaloniaProperty<string> StatusProperty =
        AvaloniaProperty.Register<MainWindow, string>("StatusText", inherits: true);

        public string StatusText
        {
            get { return this.GetValue(StatusProperty); }
            set { this.SetValue(StatusProperty, value); }
        }

        public ObservableCollection<Signal> Signals { get; set; }
    }
}