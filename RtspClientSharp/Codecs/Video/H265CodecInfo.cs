using System;

namespace RtspClientSharp.Codecs.Video
{
    public class H265CodecInfo : VideoCodecInfo
    {
        public int PacketizationMode { get; set; }

        public bool HasDonlField { get; set; }
        
        public byte[] VpsBytes { get; set; } = Array.Empty<byte>();

        public byte[] SpsBytes { get; set; } = Array.Empty<byte>();

        public byte[] PpsBytes { get; set; } = Array.Empty<byte>();
    }
}
