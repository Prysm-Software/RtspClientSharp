using System;

namespace RtspClientSharp.Codecs.Video
{
    public class H264CodecInfo : VideoCodecInfo
    {
        public string ProfileLevelId { get; set; }

        public int PacketizationMode { get; set; }

        public byte[] SpsPpsBytes { get; set; } = Array.Empty<byte>();
    }
}