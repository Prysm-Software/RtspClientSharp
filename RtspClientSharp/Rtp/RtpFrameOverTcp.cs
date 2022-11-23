using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.Rtp
{
    public class RtpFrameOverTcp : IRtpFrame
    {
        public byte[] Data { get; }

        public RtpFrameOverTcp(byte[] data)
        {
            Data = data;
        }
    }
}
