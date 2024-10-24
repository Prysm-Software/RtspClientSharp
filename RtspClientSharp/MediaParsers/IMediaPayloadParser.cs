using System;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;

namespace RtspClientSharp.MediaParsers
{
    interface IMediaPayloadParser : IDisposable
    {
        DateTime BaseTime { get; set; }

        Action<RawFrame> FrameGenerated { get; set; }
        
        Action<RawNALuFrame> NaluReceived { get; set; }

        void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit);

        void ResetState();
    }
}