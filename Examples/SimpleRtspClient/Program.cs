using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
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

            //var serverUri = "rtsp://onvif:Prysm-123@192.168.50.17:554/live/bf4f8cb1-f4bf-4fda-aeef-9e6fd5ffc03f"; // milestone MOBOTIX
            //var serverUri = "rtsp://admin:Prysm123@192.168.40.34:554/Streaming/Channels/103?transportmode=unicast&profile=Profile_3"; // HIK h265
            //var serverUri = "rtsp://admin:pass@192.168.40.33/stream1"; // mobotix
            //var serverUri = "rtsp://admin:prysm-123@192.168.0.201/0/onvif/profile1/media.smp"; // wisenet
            //var serverUri = "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4";
            //var serverUri = "rtsp://hello:world@192.168.50.1/Profile.C0.S0.unicast";
            //var serverUri = "rtsp://hello:world@192.168.50.1/R.D0.C8.S0";
            //var serverUri = "rtsp://onvif:Prysm-123@192.168.50.17:554/live/1fefea38-c5e2-4e4b-9b43-bcb75365ed16"; // Milestone
            //var serverUri = "rtsp://admin:Prysm123@192.168.50.11/ISAPI/streaming/tracks/401?starttime=20240318T080652Z&endtime=20240318T081506Z"; // HIK
            //var serverUri = "rtsp://appvision:Prysm123@192.168.20.44:7001/799f1fd2-0a68-1fab-7d12-5b184c8d7409?speed=1&pos=1709265600000";
            //var serverUri = "rtsp://admin:@192.168.30.5/Interface/Cameras/Media?Camera=Mobotix&Profile=Visualization"; // digifort
            //var serverUri = "rtsp://appvision:prysm123@192.168.50.18/rtsp/Camera35"; // Cossilys
            //var serverUri = "rtsp://service:Ccrlyon69!@192.168.40.24/rtsp_tunnel?p=0&h26x=4&aon=1&aud=1&vcd=2"; // BOSCH
            var serverUri = "rtsp://root:pass@192.168.40.31/onvif-media/media.amp?profile=profile_1_h265"; // Axis acceuil
            //var serverUri = "rtsp://root:pass@192.168.40.10/onvif-media/media.amp"; // Axis F41
            //var serverUri = "rtsp://root:pass@192.168.40.10/onvif-media/media.amp?profile=profile_1_jpeg"; // Axis F41 MJPEG


            var connectionParameters = new ConnectionParameters(new Uri(serverUri))
            {
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                RtpTransport = RtpTransportProtocol.UDP,
                RequiredTracks = RequiredTracks.All,
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
                    _rtspClient.NaluFrameReceived += NaluReceived;
                    _rtspClient.FrameReceived += FrameReceived;

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
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void NaluReceived(object sender, RawNALuFrame data)
        {
            Debug.WriteLine($"{data.Timestamp}  NALu {BitConverter.ToString(data.FrameSegment.Array, 0, data.FrameSegment.Count > 20 ? 20 : data.FrameSegment.Count)}" );
        }

        private static void FrameReceived(object sender, RawFrame e)
        {
            Console.WriteLine($"{e.Timestamp}  FRAME " + e.GetType().ToString().Split('.').LastOrDefault());
        }
    }
}