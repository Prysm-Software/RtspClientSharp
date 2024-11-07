using System;

namespace RtspClientSharp.Rtsp
{
    class RtspRequestMessageFactory
    {
        private static readonly Version ProtocolVersion = new Version(1, 0);

        private uint _cSeq;
        private readonly Uri _rtspUri;
        private readonly string _userAgent;

        public Uri ContentBase { get; set; }
        public string SessionId { get; set; }

        public RtspRequestMessageFactory(Uri rtspUri, string userAgent)
        {
            _rtspUri = rtspUri ?? throw new ArgumentNullException(nameof(rtspUri));
            _userAgent = userAgent;
        }

        public RtspRequestMessage CreateOptionsRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.OPTIONS, _rtspUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateDescribeRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.DESCRIBE, _rtspUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Accept", "application/sdp");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateSetupTcpInterleavedRequest(string trackName, int rtpChannel, int rtcpChannel)
        {
            Uri trackUri = GetTrackUri(trackName);

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/TCP;unicast;interleaved={rtpChannel}-{rtcpChannel}");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateSetupUdpUnicastRequest(string trackName, int rtpPort, int rtcpPort)
        {
            Uri trackUri = GetTrackUri(trackName);

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/UDP;unicast;client_port={rtpPort}-{rtcpPort}");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreatePlayRequest(RtspRequestParams requestParam, bool includeDefaultNptRange = true)
        {
            var uri = GetContentBasedUri();
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.PLAY, uri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);

            // Seems like some timestamps are being returned with 2 different timezones and/or some difference between the requested datetime and the returned one.
            if (requestParam?.InitialTimestamp?.Kind == DateTimeKind.Utc)
                rtspRequestMessage.Headers.Add("Range", $"clock={requestParam.InitialTimestamp.Value:yyyyMMddTHHmmssZ}-");
            else if (requestParam?.InitialTimestamp != null)
                rtspRequestMessage.Headers.Add("Range", $"clock={DateTime.SpecifyKind(requestParam.InitialTimestamp.Value, DateTimeKind.Utc):yyyyMMddTHHmmssZ}-");
            else if (includeDefaultNptRange)
                rtspRequestMessage.Headers.Add("Range", "npt=0.000-");

            if (requestParam?.Headers != null)
                foreach (var item in requestParam.Headers)
                    rtspRequestMessage.Headers.Add(item.Key, item.Value);

            return rtspRequestMessage;
        }

        public RtspRequestMessage CreatePauseRequest(RtspRequestParams connectionParams = null)
        {
            Uri uri = GetContentBasedUri();

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.PAUSE, uri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
            if (connectionParams?.Headers != null)
                foreach (var item in connectionParams.Headers)
                    rtspRequestMessage.Headers.Add(item.Key, item.Value);

            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateTeardownRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.TEARDOWN, _rtspUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateGetParameterRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.GET_PARAMETER, _rtspUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        private Uri GetContentBasedUri()
        {
            if (ContentBase != null)
                return ContentBase;

            return _rtspUri;
        }

        private uint NextCSeqProvider()
        {
            return ++_cSeq;
        }

        private Uri GetTrackUri(string trackName)
        {
            Uri trackUri;

            if (!Uri.IsWellFormedUriString(trackName, UriKind.Absolute))
            {
                var uriBuilder = new UriBuilder(GetContentBasedUri());

                bool trackNameStartsWithSlash = trackName.StartsWith("/");

                if (uriBuilder.Path.EndsWith("/"))
                    uriBuilder.Path += trackNameStartsWithSlash ? trackName.Substring(1) : trackName;
                else
                    uriBuilder.Path += trackNameStartsWithSlash ? trackName : "/" + trackName;

                trackUri = uriBuilder.Uri;
            }
            else
                trackUri = new Uri(trackName, UriKind.Absolute);

            return trackUri;
        }
    }
}