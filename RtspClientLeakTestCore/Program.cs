using System.Net;
using System.Threading;
using System;
using RtspClientSharp;


int timeout = 5000;
ConnectionParameters[] _uris =
{
    new ConnectionParameters(new Uri("rtsp://192.168.40.33/stream1"), new NetworkCredential("admin", "pass"))
    {
        RequiredTracks = RequiredTracks.All,
        RtpTransport = RtpTransportProtocol.UDP,
        CancelTimeout = TimeSpan.FromMilliseconds(timeout),
        ReceiveTimeout = TimeSpan.FromMilliseconds(timeout),
        ConnectTimeout = TimeSpan.FromMilliseconds(timeout),
    },
};

async void Loop()
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
            cts.CancelAfter(10000);

            //rtsp.FrameReceived += (s, f) => Console.WriteLine("New packet " + f);

            await rtsp.ConnectAsync(cts.Token);
            Console.WriteLine("Connected to " + uri.ConnectionUri + " with " + string.Join(", ", rtsp.ClientDescription.MediaTracks.Select(t => t.Codec)));
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

for (int i = 0; i < 10; i++)
    Loop();

Console.ReadLine();