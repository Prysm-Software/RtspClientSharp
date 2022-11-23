using RtspClientSharp.Sdp;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp
{
    public class RtspClientDescription
    {
        public string SdpDocument { get; }
        public IEnumerable<RtspMediaTrackInfo> MediaTracks { get; }

        public RtspClientDescription(string sdpDocument, IEnumerable<RtspMediaTrackInfo> mediaTracks)
        {
            SdpDocument = sdpDocument;
            MediaTracks = mediaTracks;
        }   
    }
}
