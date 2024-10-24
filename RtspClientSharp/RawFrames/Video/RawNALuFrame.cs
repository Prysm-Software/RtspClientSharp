using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.RawFrames.Video
{
    public class RawNALuFrame : RawVideoFrame
    {
        public RawNALuFrame(DateTime timestamp, ArraySegment<byte> frameSegment)
            : base(timestamp, frameSegment)
        {
        }
    }
}
