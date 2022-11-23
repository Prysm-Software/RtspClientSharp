using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.Rtp
{
    public interface IRtpFrame
    {
        byte[] Data { get; }
    }
}
