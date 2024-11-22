namespace RtspClientSharp.Codecs.Video
{
    public abstract class VideoCodecInfo : CodecInfo
    {
        public double Framerate { get; set; }
        public string Framesize { get; set; }
    }
}