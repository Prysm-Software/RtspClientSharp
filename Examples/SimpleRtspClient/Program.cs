using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp;
using RtspClientSharp.Rtp;
using RtspClientSharp.Rtsp;

namespace SimpleRtspClient
{
    class Program
    {
        static void Main()
        {
            var serverUri = new Uri("rtsp://hello:world@192.168.50.1/Profile.C8.S0.unicast"); // axis acceuil
            //var serverUri = new Uri("rtsp://192.168.40.22/LiveChannel2/media.smp"); // wisenet
            //var serverUri = new Uri("rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4");
            //var serverUri = new Uri("rtsp://192.168.40.31/onvif-media/media.amp?profile=profile_2_h264");
            //var credentials = new NetworkCredential("root", "pass");

            var connectionParameters = new ConnectionParameters(serverUri);
            var cancellationTokenSource = new CancellationTokenSource();

            Task connectTask = ConnectAsync(connectionParameters, cancellationTokenSource.Token);

            Console.WriteLine("Press any key to cancel");
            Console.ReadLine();

            cancellationTokenSource.Cancel();

            Console.WriteLine("Canceling");
            connectTask.Wait(CancellationToken.None);
        }

        private static async Task ConnectAsync(ConnectionParameters connectionParameters, CancellationToken token)
        {
            try
            {
                TimeSpan delay = TimeSpan.FromSeconds(5);

                using (var rtspClient = new RtspClient(connectionParameters))
                {
                    //rtspClient.FrameReceived += (sender, frame) => Console.WriteLine($"New frame {frame.Timestamp}: {frame.GetType().Name}");
                    rtspClient.NaluReceived += (s, data) => Console.WriteLine($"nalu {data.Length}");
                   
                    //while (true)
                    {
                        Console.WriteLine("Connecting...");

                        try
                        {
                            await rtspClient.ConnectAsync(new RtspRequestParams { Token = token });
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (RtspClientException e)
                        {
                            Console.WriteLine(e.ToString());
                            //await Task.Delay(delay, token);
                            return;
                        }

                        Console.WriteLine("Connected.");
                        Console.WriteLine("Got SDP :");
                        Console.WriteLine(rtspClient.ClientDescription.SdpDocument);

                        try
                        {
                            await rtspClient.ReceiveAsync(token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (RtspClientException e)
                        {
                            Console.WriteLine(e.ToString());
                            await Task.Delay(delay, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}