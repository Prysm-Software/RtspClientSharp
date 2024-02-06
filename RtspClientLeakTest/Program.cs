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
            new ConnectionParameters(new Uri("rtsp://192.168.40.111/0/onvif/profile2/media.smp"), new NetworkCredential("admin", "prysm-123"))
            {
                RequiredTracks = RequiredTracks.All,
                RtpTransport = RtpTransportProtocol.UDP,
                CancelTimeout = TimeSpan.FromMilliseconds(timeout),
                ReceiveTimeout = TimeSpan.FromMilliseconds(timeout),
                ConnectTimeout = TimeSpan.FromMilliseconds(timeout),
            },
            new ConnectionParameters(new Uri("rtsp://192.168.40.33/stream1"), new NetworkCredential("admin", "pass"))
            {
                RequiredTracks = RequiredTracks.All,
                RtpTransport = RtpTransportProtocol.UDP,
                CancelTimeout = TimeSpan.FromMilliseconds(timeout),
                ReceiveTimeout = TimeSpan.FromMilliseconds(timeout),
                ConnectTimeout = TimeSpan.FromMilliseconds(timeout),
            },
            new ConnectionParameters(new Uri("rtsp://192.168.40.34/Streaming/Channels/102?transportmode=unicast&profile=Profile_2"), new NetworkCredential("admin", "Prysm123"))
            {
                RequiredTracks = RequiredTracks.All,
                RtpTransport = RtpTransportProtocol.UDP,
                CancelTimeout = TimeSpan.FromMilliseconds(timeout),
                ReceiveTimeout = TimeSpan.FromMilliseconds(timeout),
                ConnectTimeout = TimeSpan.FromMilliseconds(timeout),
            }
        };

        static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += (s, e) => Console.WriteLine("TaskScheduler.UnobservedTaskException");

            for (int i = 0; i < 10; i++)
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
