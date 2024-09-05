using RtspClientSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RtspClientLeakTest
{
    internal class Program
    {
        private static readonly int timeout = 5000;
        private static readonly ConnectionParameters[] _uris =
        {
            new ConnectionParameters(new Uri("rtsp://192.168.40.1/onvif-media/media.amp?profile=profile_1_h264"), new NetworkCredential("root", "pass")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.2/onvif-media/media.amp?profile=profile_1_h264"), new NetworkCredential("root", "pass")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.4/onvif-media/media.amp?profile=profile_1_h264"), new NetworkCredential("root", "pass")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.10/onvif-media/media.amp?profile=profile_1_h264"), new NetworkCredential("root", "pass")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.30/onvif-media/media.amp?profile=profile_1_h264"), new NetworkCredential("root", "pass")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.31/onvif-media/media.amp?profile=profile_1_h264"), new NetworkCredential("root", "pass")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.34/Streaming/Channels/102"), new NetworkCredential("admin", "Prysm123")),
            new ConnectionParameters(new Uri("rtsp://192.168.40.34/Streaming/Channels/103"), new NetworkCredential("admin", "Prysm123"))
        };

        static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (s, e) => Console.WriteLine("TaskScheduler.UnobservedTaskException");

            foreach (var uris in _uris)
            {
                uris.RequiredTracks = RequiredTracks.All;
                uris.RtpTransport = RtpTransportProtocol.UDP;
                uris.CancelTimeout = TimeSpan.FromMilliseconds(timeout);
                uris.ReceiveTimeout = TimeSpan.FromMilliseconds(timeout);
                uris.ConnectTimeout = TimeSpan.FromMilliseconds(timeout);
            }

            for (int i = 0; i < 20; i++)
                Loop();

            Console.ReadLine();
        }

        static async void Loop()
        {
            var count = 0;

            while (true)
            {
                RtspClient rtsp = null;
                var cts = new CancellationTokenSource();

                try
                {
                    var uri = _uris[count++ % _uris.Length];

                    Console.WriteLine("Connecting to " + uri.ConnectionUri);

                    rtsp = new RtspClient(uri);
                    cts.CancelAfter(10_000);

                   // rtsp.FrameReceived += (s, f) => Console.WriteLine("New packet " + f);

                    await rtsp.ConnectAsync(cts.Token);
                    Console.WriteLine("Connected to " + uri.ConnectionUri + " with " + string.Join(", ", rtsp.ClientDescription.MediaTracks.Select(t => t.Codec.GetType().Name.Split('.').LastOrDefault())));
                    await rtsp.ReceiveAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("End. Reason: " + ex.Message);
                }
                finally
                {
                    try
                    {
                        rtsp?.Dispose();
                    }
                    catch { }

                    try
                    {
                        cts.Dispose();
                    }
                    catch { }
                }
            }
        }
    }
}
