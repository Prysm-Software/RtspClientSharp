using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static RtspClient _rtspClient;

        static void Main()
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            //var serverUri = new Uri("rtsp://onvif:Prysm-123@192.168.50.17:554/live/bf4f8cb1-f4bf-4fda-aeef-9e6fd5ffc03f"); // milestone MOBOTIX
            //var serverUri = new Uri("rtsp://admin:Prysm123@192.168.40.34:554/Streaming/Channels/102?transportmode=unicast&profile=Profile_2"); // HIK h265
            //var serverUri = new Uri("rtsp://admin:pass@192.168.40.33/stream1"); // mobotix
            //var serverUri = new Uri("rtsp://root:pass@192.168.40.31/onvif-media/media.amp?profile=profile_2_h264"); // axis acceuil
            //var serverUri = new Uri("rtsp://admin:prysm-123@192.168.40.111/0/onvif/profile1/media.smp"); // wisenet
            //var serverUri = new Uri("rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4");
            //var serverUri = new Uri("rtsp://hello:world@192.168.50.1/Profile.C0.S0.unicast");
            var serverUri = new Uri("rtsp://onvif:Prysm-123@192.168.50.17:554/vod/bf4f8cb1-f4bf-4fda-aeef-9e6fd5ffc03f");

            var connectionParameters = new ConnectionParameters(serverUri)
            {
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                RtpTransport = RtpTransportProtocol.UDP,
            };
            var cancellationTokenSource = new CancellationTokenSource();

            Task.Run(async () =>
            {
                await ConnectAsync(connectionParameters, cancellationTokenSource.Token);
                Console.WriteLine("Exiting connect task");
            });

            Console.WriteLine("Press pause play or cancel");

            string key;
            do
            {
                key = Console.ReadLine();
                switch (key)
                {
                    case "play":
                        Console.WriteLine("sending play");
                        _ = _rtspClient.PlayAsync(new RtspRequestParams());
                        break;
                    case "pause":
                        Console.WriteLine("sending pause");
                        _ = _rtspClient.PauseAsync(new RtspRequestParams());
                        break;
                    default:
                        break;
                }
            } while (key != "cancel");


            Console.WriteLine("Canceling");
            cancellationTokenSource.Cancel();
            //connectTask.Wait(CancellationToken.None);

            Console.WriteLine("Press any key to quit...");
            Console.ReadLine();
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine("TaskScheduler_UnobservedTaskException");
            Console.WriteLine(e.ToString());
        }

        private static async Task ConnectAsync(ConnectionParameters connectionParameters, CancellationToken token)
        {
            try
            {
                TimeSpan delay = TimeSpan.FromSeconds(5);

                using (_rtspClient = new RtspClient(connectionParameters))
                {
                    //_rtspClient.NaluReceived += NaluReceived;
                    //_rtspClient.FrameReceived += FrameReceived;

                    Console.WriteLine("Connecting...");

                    try
                    {
                        await _rtspClient.ConnectAsync(new RtspRequestParams
                        {
                            Token = token,
                            
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (RtspClientException e)
                    {
                        Console.WriteLine(e.ToString());
                        return;
                        //await Task.Delay(delay, token);
                        //continue;
                    }

                    Console.WriteLine("Connected.");
                    Console.WriteLine("Got SDP :");
                    Console.WriteLine(_rtspClient.ClientDescription.SdpDocument);
                    Console.WriteLine("Receiving packet...");

                    try
                    {
                        await _rtspClient.ReceiveAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (RtspClientException e)
                    {
                        Console.WriteLine(e.ToString());
                        //await Task.Delay(delay, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void NaluReceived(object sender, byte[] data)
        {
            Debug.WriteLine($"NALu {BitConverter.ToString(data, 0, data.Length > 20 ? 20 : data.Length)}");
        }

        private static void FrameReceived(object sender, RtspClientSharp.RawFrames.RawFrame e)
        {
            Console.WriteLine("FRAME " + e.GetType().ToString().Split('.').LastOrDefault());
        }
    }
}