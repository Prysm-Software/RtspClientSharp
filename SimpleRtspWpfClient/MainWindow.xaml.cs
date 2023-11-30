using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.Rtsp;
using SimpleRtspWpfClient.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SimpleRtspWpfClient
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private CancellationTokenSource _cancellationTokenSource;
        public event PropertyChangedEventHandler PropertyChanged;
        public int FrameReceived { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            var d = new DispatcherTimer();
            d.Tick += (s, e) =>
            {
                Notify(nameof(FrameReceived));
            };
            d.Interval = TimeSpan.FromSeconds(1);
            d.Start();
        }

        private void ButtonStopClick(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ButtonPlayClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();

            string url = ComboBoxUrl.Text;

            _cancellationTokenSource = new CancellationTokenSource();
            Start(url, _cancellationTokenSource.Token);
        }

        private async void ButtonLoopClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();

            string url = ComboBoxUrl.Text;
            _cancellationTokenSource = new CancellationTokenSource();

            do
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);

                _ = Task.Run(() => Start(url, cts.Token));

                await Task.Delay(5000);
                cts.Cancel();

                //Debug.WriteLine("Waiting dispose");
                //await Task.Delay(5000);

            } while (!_cancellationTokenSource.IsCancellationRequested);
        }

        private async void Start(string uri, CancellationToken token)
        {
            FrameReceived = 0;

            var connectionParameters = new ConnectionParameters(new Uri(uri))
            {
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                RtpTransport = RtpTransportProtocol.UDP
            };
            var cancellationTokenSource = new CancellationTokenSource();

            var rtspClient = new RtspClient(connectionParameters);
            rtspClient.FrameReceived += OnFrameReceived;

            Debug.WriteLine("Connecting to " + uri);

            try
            {
                await rtspClient.ConnectAsync(new RtspRequestParams { Token = token });
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Connect canceled");
                return;
            }
            catch (RtspClientException ex)
            {
                Debug.WriteLine("Connect error " + ex.ToString());
                return;
            }

            Debug.WriteLine("Connected.");

            try
            {
                await rtspClient.ReceiveAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (RtspClientException ex)
            {
                Debug.WriteLine("RtspClientException " + ex.ToString());
            }
            finally
            {
                Debug.WriteLine("Disposing...");
                rtspClient.FrameReceived -= OnFrameReceived;
                rtspClient.Dispose();
            }
        }

        private void OnFrameReceived(object sender, RawFrame e)
        {
            FrameReceived++;
            //Debug.WriteLine("GOT FRAME " + e.FrameSegment.Count);
        }

        private void Notify([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
