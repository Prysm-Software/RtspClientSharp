using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.Rtp
{
    public class RtpFrameOverUdp : IRtpFrame
    {
        public int Channel { get; }
        public byte[] Data { get; }

        public RtpFrameOverUdp(byte[] data, int channel)
        {
            Data = data;
            Channel = channel;
        }
    }
}
