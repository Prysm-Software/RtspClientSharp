using System;
using System.Collections.Generic;
using System.Threading;

namespace RtspClientSharp.Rtsp
{
    public class RtspRequestParams
    {
        /// <summary>
        /// For setting the Range header of an RTSP PLAY request
        /// </summary>
        public DateTime? InitialTimestamp { get; set; }

        [Obsolete("Not used anymore")]
        public bool IsSetTimestampInClock{ get; set; }
        
        public CancellationToken Token { get; set; }
        
        public Dictionary<string, string> Headers { get; set; }
    }
}
