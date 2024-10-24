using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp
{
    public interface IRtspClient : IDisposable
    {
        ConnectionParameters ConnectionParameters { get; }
        RtspClientDescription ClientDescription { get; }
        
        event EventHandler<RawFrame> FrameReceived;

        [Obsolete("Use NaluFrameReceived instead")]
        event EventHandler<byte[]> NaluReceived;

        /// <summary>
        /// Called every time a NALu h26x is received
        /// </summary>
        event EventHandler<RawNALuFrame> NaluFrameReceived;


        /// <summary>
        /// Connect to endpoint and start RTSP session
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="InvalidCredentialException"></exception>
        /// <exception cref="RtspClientException"></exception>
        Task ConnectAsync(RtspRequestParams connectionParams);

        /// <summary>
        /// Receive frames. 
        /// Should be called after successful connection to endpoint or <exception cref="InvalidOperationException"></exception> will be thrown
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="RtspClientException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        Task ReceiveAsync(CancellationToken token);
    }
}