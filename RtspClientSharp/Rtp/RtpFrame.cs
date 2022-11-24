using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.Rtp
{
    public class RtpFrame
    {
        public int Channel { get; }
        public byte[] Data { get; }

        public RtpFrame(byte[] data, int channel)
        {
            Data = data;
            Channel = channel;
        }
    }
}
