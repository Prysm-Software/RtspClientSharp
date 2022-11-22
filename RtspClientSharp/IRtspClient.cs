using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.RawFrames;
using RtspClientSharp.Rtsp;
using RtspClientSharp.Sdp;

namespace RtspClientSharp
{
    public interface IRtspClient : IDisposable
    {
        ConnectionParameters ConnectionParameters { get; }
        event EventHandler<RawFrame> FrameReceived;
        event EventHandler<byte[]> NaluReceived;
        string Sdp { get; }
        IEnumerable<RtspMediaTrackInfo> Tracks { get; }

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