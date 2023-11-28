﻿using System;
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
            var serverUri = new Uri("rtsp://admin:pass@192.168.40.33/stream1"); // mobotix
            //var serverUri = new Uri("rtsp://root:pass@192.168.40.31/onvif-media/media.amp?profile=profile_2_h264"); // axis acceuil
            //var serverUri = new Uri("rtsp://192.168.40.22/LiveChannel2/media.smp"); // wisenet
            //var serverUri = new Uri("rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mp4");
            //var serverUri = new Uri("rtsp://192.168.40.31/onvif-media/media.amp?profile=profile_2_h264");
            //var credentials = new NetworkCredential("root", "pass");

            var connectionParameters = new ConnectionParameters(serverUri)
            {
                ReceiveTimeout = TimeSpan.FromSeconds(5),
                RtpTransport = RtpTransportProtocol.UDP
            };
            var cancellationTokenSource = new CancellationTokenSource();

            Task connectTask = ConnectAsync(connectionParameters, cancellationTokenSource.Token);

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

            cancellationTokenSource.Cancel();

            Console.WriteLine("Canceling");
            connectTask.Wait(CancellationToken.None);
        }

        private static async Task ConnectAsync(ConnectionParameters connectionParameters, CancellationToken token)
        {
            try
            {
                TimeSpan delay = TimeSpan.FromSeconds(5);

                using (_rtspClient = new RtspClient(connectionParameters))
                {
                    //rtspClient.NaluReceived += (s, data) => Console.WriteLine($"nalu {data.Length}");
                    //_rtspClient.RtpReceived += (s, frame) =>
                    //{
                    //    Console.WriteLine($"rtp on channel {frame.Channel} {BitConverter.ToString(frame.Data.Take(10).ToArray())}");
                    //};
                    _rtspClient.FrameReceived += _rtspClient_FrameReceived;

                    //while (true)
                    {
                        Console.WriteLine("Connecting...");

                        try
                        {
                            await _rtspClient.ConnectAsync(new RtspRequestParams { Token = token });
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
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void _rtspClient_FrameReceived(object sender, RtspClientSharp.RawFrames.RawFrame e)
        {
            System.Diagnostics.Debug.WriteLine("GOT FRAME " + e.FrameSegment.Count);
        }
    }
}