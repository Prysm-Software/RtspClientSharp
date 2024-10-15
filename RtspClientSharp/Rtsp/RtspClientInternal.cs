using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.Codecs.Data;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames;
using RtspClientSharp.Rtcp;
using RtspClientSharp.Rtp;
using RtspClientSharp.Sdp;
using RtspClientSharp.Tpkt;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtsp
{
    sealed class RtspClientInternal : IDisposable
    {
        private const int RtcpReportIntervalBaseMs = 5000;
        private static readonly char[] TransportAttributesSeparator = { ';' };

        private readonly ConnectionParameters _connectionParameters;
        private readonly Func<IRtspTransportClient> _transportClientProvider;
        private readonly RtspRequestMessageFactory _requestMessageFactory;

        private List<IMediaPayloadParser> _mediaPayloadParser = new List<IMediaPayloadParser>();

        private readonly Dictionary<int, ITransportStream> _streamsMap = new Dictionary<int, ITransportStream>();
        private readonly ConcurrentDictionary<int, Socket> _udpClientsMap = new ConcurrentDictionary<int, Socket>();
        private readonly ConcurrentDictionary<int, int> _udpRtp2RtcpMap = new ConcurrentDictionary<int, int>();

        private readonly Dictionary<int, RtcpReceiverReportsProvider> _reportProvidersMap =
            new Dictionary<int, RtcpReceiverReportsProvider>();

        private TpktStream _tpktStream;

        private readonly SimpleHybridLock _hybridLock = new SimpleHybridLock();
        private readonly Random _random = RandomGeneratorFactory.CreateGenerator();
        private IRtspTransportClient _rtspTransportClient;

        private int _rtspKeepAliveTimeoutMs;

        private readonly CancellationTokenSource _serverCancellationTokenSource = new CancellationTokenSource();
        private bool _isServerSupportsGetParameterRequest;
        private int _disposed;

        public Action<RawFrame> FrameReceived;
        public Action<byte[]> NaluReceived;
        public RtspClientDescription ClientDescription { get; private set; }


        public RtspClientInternal(ConnectionParameters connectionParameters,
            Func<IRtspTransportClient> transportClientProvider = null)
        {
            _connectionParameters = connectionParameters ?? throw new ArgumentNullException(nameof(connectionParameters));
            _transportClientProvider = transportClientProvider ?? CreateTransportClient;

            Uri fixedRtspUri = connectionParameters.GetFixedRtspUri();
            _requestMessageFactory = new RtspRequestMessageFactory(fixedRtspUri, connectionParameters.UserAgent);
        }

        public async Task ConnectAsync(RtspRequestParams requestParams)
        {
            if (requestParams == null)
                throw new RtspClientException("Request parameters can't be null");

            IRtspTransportClient rtspTransportClient = _transportClientProvider();
            Volatile.Write(ref _rtspTransportClient, rtspTransportClient);

            await _rtspTransportClient.ConnectAsync(requestParams.Token);

            RtspRequestMessage optionsRequest = _requestMessageFactory.CreateOptionsRequest();
            RtspResponseMessage optionsResponse = await _rtspTransportClient.ExecuteRequest(optionsRequest, requestParams.Token);

            if (optionsResponse.StatusCode == RtspStatusCode.Ok)
                ParsePublicHeader(optionsResponse.Headers[WellKnownHeaders.Public]);

            RtspRequestMessage describeRequest = _requestMessageFactory.CreateDescribeRequest();
            RtspResponseMessage describeResponse =
                await _rtspTransportClient.EnsureExecuteRequest(describeRequest, requestParams.Token);

            string contentBaseHeader = describeResponse.Headers[WellKnownHeaders.ContentBase];

            if (!string.IsNullOrEmpty(contentBaseHeader))
                _requestMessageFactory.ContentBase = new Uri(contentBaseHeader);

            var parser = new SdpParser();
            IEnumerable<RtspTrackInfo> tracks = parser.Parse(describeResponse.ResponseBody);

            bool anyTrackRequested = false;

            var mediaTracks = new List<RtspMediaTrackInfo>();
            foreach (RtspMediaTrackInfo track in GetTracksToSetup(tracks)
            {
                await SetupTrackAsync(requestParams.InitialTimestamp, track, requestParams.Token);
                anyTrackRequested = true;

                mediaTracks.Add(track);
            }

            ClientDescription = new RtspClientDescription(parser.Sdp, mediaTracks);

            if (!anyTrackRequested)
                throw new RtspClientException("Any suitable track is not found");

            // TODO: Seems like some timestamps are being returned with 2 different timezones and/or some difference between the requested datetime and the returned one.
            //RtspRequestMessage playRequest = requestParams.IsSetTimestampInClock ? _requestMessageFactory.CreatePlayRequest(requestParams) : _requestMessageFactory.CreatePlayRequest();

            RtspRequestMessage playRequest = _requestMessageFactory.CreatePlayRequest(requestParams);
            RtspResponseMessage playResponse = await _rtspTransportClient.EnsureExecuteRequest(playRequest, requestParams.Token, 1);

            //// TODO : Create a specific parse to convert the clock values
            //Regex clockRegex = new Regex(@"clock=(?<startTime>\d{8}T\d{6}Z)\-(?<endTime>\d{8}T\d{6}Z)", RegexOptions.Singleline);
            //foreach (string playResponseHeader in playResponse.Headers.GetValues("Range"))
            //{
            //    Match clockMatches = clockRegex.Match(playResponseHeader);
            //    if (clockMatches.Success)
            //        _mediaPayloadParser.BaseTime = DateTime.ParseExact(clockMatches.Groups["startTime"].Value, "yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture, DateTimeStyles.None);
            //}
        }

        public async Task ReceiveAsync(CancellationToken token)
        {
            if (_rtspTransportClient == null)
                throw new InvalidOperationException("Client should be connected first");

            using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_serverCancellationTokenSource.Token, token))
            {
                CancellationToken linkedToken = linkedTokenSource.Token;
                Exception keepAliveError = null;

                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        TimeSpan nextRtspKeepAliveInterval = GetNextRtspKeepAliveInterval();

                        while (true)
                        {
                            await Task.Delay(nextRtspKeepAliveInterval, linkedToken);

                            nextRtspKeepAliveInterval = GetNextRtspKeepAliveInterval();
                            await SendRtspKeepAliveAsync(linkedToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        keepAliveError = ex;
                        try
                        {
                            linkedTokenSource.Cancel();
                        }
                        catch { }
                    }
                });

                if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                    await ReceiveOverTcpAsync(_rtspTransportClient.GetStream(), linkedToken);
                else
                    await ReceiveOverUdpAsync(linkedToken);

                if (keepAliveError != null)
                    throw keepAliveError;

                // Ancien fonctionnement.
                //Task receiveTask = _connectionParameters.RtpTransport == RtpTransportProtocol.TCP
                //    ? ReceiveOverTcpAsync(_rtspTransportClient.GetStream(), linkedToken)
                //    : ReceiveOverUdpAsync(linkedToken);

                //if (!_isServerSupportsGetParameterRequest)
                //    await receiveTask;
                //else
                //{
                //    Task rtspKeepAliveDelayTask = Task.Delay(nextRtspKeepAliveInterval, linkedToken);

                //    while (true)
                //    {
                //        Task result = await Task.WhenAny(receiveTask, rtspKeepAliveDelayTask);

                //        Le fait de sortir du await peut créer une unobserved task exception sur le receiveTask throw à ce moment là.
                //        if (result == receiveTask || result.IsCanceled)
                //        {
                //            await receiveTask;
                //            break;
                //        }

                //        nextRtspKeepAliveInterval = GetNextRtspKeepAliveInterval();
                //        rtspKeepAliveDelayTask = Task.Delay(nextRtspKeepAliveInterval, linkedToken);

                //        await SendRtspKeepAliveAsync(linkedToken);
                //    }
                //}

                if (linkedToken.IsCancellationRequested)
                {
                    // If the connexion is canceled by the user, we send a TEARDOWN request.
                    try
                    {
                        await CloseRtspSessionAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // We don't care if exception are thrown as the connexion is over
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (_udpClientsMap != null)
                foreach (Socket client in _udpClientsMap.Values)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    client.Dispose();
                }

            IRtspTransportClient rtspTransportClient = Volatile.Read(ref _rtspTransportClient);

            if (rtspTransportClient != null)
                _rtspTransportClient.Dispose();

            foreach (var item in _mediaPayloadParser)
                item.Dispose();
        }

        public async Task SendPlayRequest(RtspRequestParams requestParams)
        {
            RtspRequestMessage request = requestParams.IsSetTimestampInClock
                ? _requestMessageFactory.CreatePlayRequest(requestParams)
                : _requestMessageFactory.CreatePlayRequest();

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                await _rtspTransportClient.SendRequestAsync(request, requestParams.Token);
            else
                await _rtspTransportClient.EnsureExecuteRequest(request, requestParams.Token);
        }

        public async Task SendPauseRequest(RtspRequestParams requestParams)
        {
            RtspRequestMessage request = _requestMessageFactory.CreatePauseRequest();
            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                await _rtspTransportClient.SendRequestAsync(request, requestParams.Token);
            else
                await _rtspTransportClient.EnsureExecuteRequest(request, requestParams.Token);
        }


        private IRtspTransportClient CreateTransportClient()
        {
            if (_connectionParameters.ConnectionUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.InvariantCultureIgnoreCase))
                return new RtspHttpTransportClient(_connectionParameters);

            return new RtspTcpTransportClient(_connectionParameters);
        }

        private TimeSpan GetNextRtspKeepAliveInterval()
        {
            return TimeSpan.FromMilliseconds(_random.Next(_rtspKeepAliveTimeoutMs / 2,
                _rtspKeepAliveTimeoutMs * 3 / 4));
        }

        private int GetNextRtcpReportIntervalMs()
        {
            return RtcpReportIntervalBaseMs + _random.Next(0, 11) * 100;
        }

        private async Task SetupTrackAsync(DateTime? initialTimeStamp, RtspMediaTrackInfo track, CancellationToken token)
        {
            RtspRequestMessage setupRequest;
            RtspResponseMessage setupResponse;

            int rtpChannelNumber;
            int rtcpChannelNumber;
            Socket rtpClient = null;
            Socket rtcpClient = null;

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
            {
                rtpClient = NetworkClientFactory.CreateUdpClient();
                rtcpClient = NetworkClientFactory.CreateUdpClient();

                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                    rtpClient.Bind(endPoint);

                    int rtpPort = ((IPEndPoint)rtpClient.LocalEndPoint).Port;

                    endPoint = new IPEndPoint(IPAddress.Any, rtpPort + 1);

                    try
                    {
                        rtcpClient.Bind(endPoint);
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        endPoint = new IPEndPoint(IPAddress.Any, 0);
                        rtcpClient.Bind(endPoint);
                    }

                    int rtcpPort = ((IPEndPoint)rtcpClient.LocalEndPoint).Port;

                    setupRequest = _requestMessageFactory.CreateSetupUdpUnicastRequest(track.TrackName, rtpPort, rtcpPort);
                    setupResponse = await _rtspTransportClient.EnsureExecuteRequest(setupRequest, token);
                }
                catch
                {
                    rtpClient.Close();
                    rtcpClient.Close();
                    throw;
                }
            }
            else
            {
                int channelCounter = _streamsMap.Count;
                rtpChannelNumber = channelCounter;
                rtcpChannelNumber = ++channelCounter;

                setupRequest = _requestMessageFactory.CreateSetupTcpInterleavedRequest(track.TrackName, rtpChannelNumber, rtcpChannelNumber);
                setupResponse = await _rtspTransportClient.EnsureExecuteRequest(setupRequest, token);
            }

            string transportHeader = setupResponse.Headers[WellKnownHeaders.Transport];

            if (string.IsNullOrEmpty(transportHeader))
                throw new RtspBadResponseException("Transport header is not found");

            string portsAttributeName = _connectionParameters.RtpTransport == RtpTransportProtocol.UDP
                ? "server_port"
                : "interleaved";

            string[] transportAttributes = transportHeader.Split(TransportAttributesSeparator, StringSplitOptions.RemoveEmptyEntries);
            string portsAttribute = transportAttributes.FirstOrDefault(a => a.StartsWith(portsAttributeName, StringComparison.InvariantCultureIgnoreCase));

            if (portsAttribute == null || !TryParseSeverPorts(portsAttribute, out rtpChannelNumber, out rtcpChannelNumber))
                throw new RtspBadResponseException("Server ports are not found");

            // If the server send audio and video data from the same port, we do not handle that case
            // => Happen with BOSCH cameras
            if (_streamsMap.ContainsKey(rtpChannelNumber))
            {
                rtpClient.Close();
                rtcpClient.Close();
                return;
            }

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
            {
                string sourceAttribute = transportAttributes.FirstOrDefault(a => a.StartsWith("source", StringComparison.InvariantCultureIgnoreCase));
                int equalSignIndex;

                IPAddress sourceAddress;

                if (sourceAttribute != null && (equalSignIndex = sourceAttribute.IndexOf("=", StringComparison.CurrentCultureIgnoreCase)) != -1)
                    sourceAddress = IPAddress.Parse(sourceAttribute.Substring(++equalSignIndex).Trim());
                else
                    sourceAddress = ((IPEndPoint)_rtspTransportClient.RemoteEndPoint).Address;

                Debug.Assert(rtpClient != null, nameof(rtpClient) + " != null");
                rtpClient.Connect(new IPEndPoint(sourceAddress, rtpChannelNumber));
                Debug.Assert(rtcpClient != null, nameof(rtcpClient) + " != null");
                rtcpClient.Connect(new IPEndPoint(sourceAddress, rtcpChannelNumber));

                //var udpHolePunchingPacketSegment = new ArraySegment<byte>(Array.Empty<byte>());
                var udpHolePunchingPacket = new byte[0];

                rtpClient.Send(udpHolePunchingPacket, SocketFlags.None);
                rtcpClient.Send(udpHolePunchingPacket, SocketFlags.None);

                _udpClientsMap[rtpChannelNumber] = rtpClient;
                _udpClientsMap[rtcpChannelNumber] = rtcpClient;
                _udpRtp2RtcpMap[rtpChannelNumber] = rtcpChannelNumber;
            }

            ParseSessionHeader(setupResponse.Headers[WellKnownHeaders.Session]);

            var mediaPayloadParser = MediaPayloadParser.CreateFrom(track.Codec, OnNaluReceived);
            mediaPayloadParser.BaseTime = initialTimeStamp != null ? initialTimeStamp.Value : default;

            IRtpSequenceAssembler rtpSequenceAssembler;

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
            {
                rtpSequenceAssembler = null;
                mediaPayloadParser.FrameGenerated = OnFrameGeneratedLockfree;
            }
            else
            {
                rtpSequenceAssembler = new RtpSequenceAssembler(Constants.UdpReceiveBufferSize, 256);
                mediaPayloadParser.FrameGenerated = OnFrameGeneratedThreadSafe;
            }

            _mediaPayloadParser.Add(mediaPayloadParser);

            var rtpStream = new RtpStream(mediaPayloadParser, track.SamplesFrequency, rtpSequenceAssembler);
            _streamsMap.Add(rtpChannelNumber, rtpStream);

            var rtcpStream = new RtcpStream();
            rtcpStream.SessionShutdown += (sender, args) => _serverCancellationTokenSource.Cancel();
            _streamsMap.Add(rtcpChannelNumber, rtcpStream);

            uint senderSyncSourceId = (uint)_random.Next();

            var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStream, rtcpStream, senderSyncSourceId);
            _reportProvidersMap.Add(rtpChannelNumber, rtcpReportsProvider);
        }

        private void OnNaluReceived(byte[] nalu)
        {
            NaluReceived?.Invoke(nalu);
        }

        private async Task SendRtspKeepAliveAsync(CancellationToken token)
        {
            RtspRequestMessage request = _isServerSupportsGetParameterRequest 
                ? _requestMessageFactory.CreateGetParameterRequest()
                : _requestMessageFactory.CreateOptionsRequest();

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                await _rtspTransportClient.SendRequestAsync(request, token);
            else
                await _rtspTransportClient.EnsureExecuteRequest(request, token);
        }

        private async Task CloseRtspSessionAsync(CancellationToken token)
        {
            RtspRequestMessage teardownRequest = _requestMessageFactory.CreateTeardownRequest();

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                await _rtspTransportClient.SendRequestAsync(teardownRequest, token);
            else
                await _rtspTransportClient.EnsureExecuteRequest(teardownRequest, token);
        }

        private IEnumerable<RtspMediaTrackInfo> GetTracksToSetup(IEnumerable<RtspTrackInfo> tracks)
        {
            foreach (RtspMediaTrackInfo track in tracks.OfType<RtspMediaTrackInfo>())
            {
                if (track.Codec is VideoCodecInfo && (_connectionParameters.RequiredTracks & RequiredTracks.Video) != 0)
                    yield return track;
                else if (track.Codec is AudioCodecInfo && (_connectionParameters.RequiredTracks & RequiredTracks.Audio) != 0)
                    yield return track;
                else if (track.Codec is DataCodecInfo && (_connectionParameters.RequiredTracks & RequiredTracks.Data) != 0)
                    yield return track;
            }
        }

        private void ParsePublicHeader(string publicHeader)
        {
            if (!string.IsNullOrEmpty(publicHeader))
            {
                string getParameterName = RtspMethod.GET_PARAMETER.ToString();

                if (publicHeader.IndexOf(getParameterName, StringComparison.InvariantCulture) != -1)
                    _isServerSupportsGetParameterRequest = true;
            }
        }

        private void ParseSessionHeader(string sessionHeader)
        {
            uint timeout = 0;

            if (!string.IsNullOrEmpty(sessionHeader))
            {
                int delimiter = sessionHeader.IndexOf(';');

                if (delimiter != -1)
                {
                    TryParseTimeoutParameter(sessionHeader, out timeout);
                    _requestMessageFactory.SessionId = sessionHeader.Substring(0, delimiter);
                }
                else
                    _requestMessageFactory.SessionId = sessionHeader;
            }

            if (timeout == 0)
                timeout = 60;

            _rtspKeepAliveTimeoutMs = (int)(timeout * 1000);
        }

        private bool TryParseSeverPorts(string portsAttribute, out int rtpPort, out int rtcpPort)
        {
            rtpPort = 0;
            rtcpPort = 0;

            int equalSignIndex = portsAttribute.IndexOf('=');

            if (equalSignIndex == -1)
                return false;

            int rtpPortStartIndex = ++equalSignIndex;

            if (rtpPortStartIndex == portsAttribute.Length)
                return false;

            while (portsAttribute[rtpPortStartIndex] == ' ')
                if (++rtpPortStartIndex == portsAttribute.Length)
                    return false;

            int hyphenIndex = portsAttribute.IndexOf('-', equalSignIndex);

            if (hyphenIndex == -1)
                return false;

            string rtpPortValue = portsAttribute.Substring(rtpPortStartIndex, hyphenIndex - rtpPortStartIndex);

            if (!int.TryParse(rtpPortValue, out rtpPort))
                return false;

            int rtcpPortStartIndex = ++hyphenIndex;

            if (rtcpPortStartIndex == portsAttribute.Length)
                return false;

            int rtcpPortEndIndex = rtcpPortStartIndex;

            while (portsAttribute[rtcpPortEndIndex] != ';')
                if (++rtcpPortEndIndex == portsAttribute.Length)
                    break;

            string rtcpPortValue = portsAttribute.Substring(rtcpPortStartIndex, rtcpPortEndIndex - rtcpPortStartIndex);

            return int.TryParse(rtcpPortValue, out rtcpPort);
        }

        private static void TryParseTimeoutParameter(string sessionHeader, out uint timeout)
        {
            const string timeoutParameterName = "timeout";

            timeout = 0;

            int delimiter = sessionHeader.IndexOf(';');

            if (delimiter == -1)
                return;

            int timeoutIndex = sessionHeader.IndexOf(timeoutParameterName, ++delimiter,
                StringComparison.InvariantCultureIgnoreCase);

            if (timeoutIndex == -1)
                return;

            timeoutIndex += timeoutParameterName.Length;

            int equalsSignIndex = sessionHeader.IndexOf('=', timeoutIndex);

            if (equalsSignIndex == -1)
                return;

            int valueStartPos = ++equalsSignIndex;

            if (valueStartPos == sessionHeader.Length)
                return;

            while (sessionHeader[valueStartPos] == ' ' || sessionHeader[valueStartPos] == '\"')
                if (++valueStartPos == sessionHeader.Length)
                    return;

            int valueEndPos = valueStartPos;

            while (sessionHeader[valueEndPos] >= '0' && sessionHeader[valueEndPos] <= '9')
                if (++valueEndPos == sessionHeader.Length)
                    break;

            string value = sessionHeader.Substring(valueStartPos, valueEndPos - valueStartPos);

            uint.TryParse(value, out timeout);
        }

        private void OnFrameGeneratedLockfree(RawFrame frame)
        {
            FrameReceived?.Invoke(frame);
        }

        private void OnFrameGeneratedThreadSafe(RawFrame frame)
        {
            if (FrameReceived == null)
                return;

            _hybridLock.Enter();

            try
            {
                FrameReceived.Invoke(frame);
            }
            finally
            {
                _hybridLock.Leave();
            }
        }

        private async Task ReceiveOverTcpAsync(Stream rtspStream, CancellationToken token)
        {
            _tpktStream = new TpktStream(rtspStream);
            int nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
            int lastTimeRtcpReportsSent = Environment.TickCount;

            using (var bufferStream = new MemoryStream())
            {
                while (!token.IsCancellationRequested)
                {
                    TpktPayload payload = await _tpktStream.ReadAsync(token);

                    if (_streamsMap.TryGetValue(payload.Channel, out ITransportStream stream))
                        stream.Process(payload.PayloadSegment);

                    int ticksNow = Environment.TickCount;

                    RebaseOnRtcpTime(stream);

                    if (!TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
                        continue;

                    lastTimeRtcpReportsSent = ticksNow;
                    nextRtcpReportInterval = GetNextRtcpReportIntervalMs();

                    foreach (KeyValuePair<int, RtcpReceiverReportsProvider> pair in _reportProvidersMap)
                    {
                        IEnumerable<RtcpPacket> packets = pair.Value.GetReportSdesPackets();
                        ArraySegment<byte> byteSegment = SerializeRtcpPackets(packets, bufferStream);
                        int rtcpChannel = pair.Key + 1;

                        await _tpktStream.WriteAsync(rtcpChannel, byteSegment, token);
                    }
                }
            }
        }

        private Task ReceiveOverUdpAsync(CancellationToken token)
        {
            var waitList = new List<Task>();

            foreach (KeyValuePair<int, Socket> pair in _udpClientsMap)
            {
                int channelNumber = pair.Key;
                Socket client = pair.Value;

                ITransportStream transportStream = _streamsMap[channelNumber];

                Task receiveTask;

                if (transportStream is RtpStream rtpStream)
                {
                    if (!_udpClientsMap.TryGetValue(_udpRtp2RtcpMap[channelNumber], out Socket clientRtcp))
                        throw new RtspClientException("RTP connection without RTCP");

                    RtcpReceiverReportsProvider receiverReportsProvider = _reportProvidersMap[channelNumber];
                    receiveTask = ReceiveRtpFromUdpAsync(client, clientRtcp, rtpStream, receiverReportsProvider, token);
                }
                else
                {
                    receiveTask = ReceiveRtcpFromUdpAsync(client, transportStream, token);
                }

                waitList.Add(receiveTask);
            }

            return Task.WhenAll(waitList);
        }

        private Task ReceiveRtpFromUdpAsync(Socket client, Socket clientRtcp, RtpStream rtpStream, RtcpReceiverReportsProvider reportsProvider, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var readBuffer = new byte[Constants.UdpReceiveBufferSize];
                var bufferSegment = new ArraySegment<byte>(readBuffer);
                int nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
                int lastTimeRtcpReportsSent = Environment.TickCount;
                IEnumerable<RtcpPacket> packets;
                ArraySegment<byte> byteSegment;

                using (var bufferStream = new MemoryStream())
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            //int read = await client.ReceiveAsync(bufferSegment, SocketFlags.None); // fuite mémoire avec le receiveasync
                            int read = client.Receive(readBuffer, SocketFlags.None);
                            var payloadSegment = new ArraySegment<byte>(readBuffer, 0, read);
                            rtpStream.Process(payloadSegment);

                            int ticksNow = Environment.TickCount;
                            if (!TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
                                continue;

                            lastTimeRtcpReportsSent = ticksNow;
                            nextRtcpReportInterval = GetNextRtcpReportIntervalMs();

                            packets = reportsProvider.GetReportSdesPackets();
                            byteSegment = SerializeRtcpPackets(packets, bufferStream);

                            clientRtcp.Send(byteSegment.Array, byteSegment.Count, SocketFlags.None);
                            //await client.SendAsync(byteSegment, SocketFlags.None);
                        }
                        catch (Exception ex)
                        {
                            if (!token.IsCancellationRequested)
                                throw ex;
                        }
                    }

                    try
                    {
                        packets = reportsProvider.GetReportByePackets();
                        byteSegment = SerializeRtcpPackets(packets, bufferStream);
                        clientRtcp.Send(byteSegment.Array, byteSegment.Count, SocketFlags.None);
                    }
                    catch { }
                }
            });
        }

        private Task ReceiveRtcpFromUdpAsync(Socket client, ITransportStream stream, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var readBuffer = new byte[Constants.UdpReceiveBufferSize];
                var bufferSegment = new ArraySegment<byte>(readBuffer);

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // All socket async method cause memoryleak in .NetFramework (But not in .NetCore).
                        //int read = await client.ReceiveAsync(bufferSegment, SocketFlags.None); 

                        int read = client.Receive(readBuffer, SocketFlags.None);
                        var payloadSegment = new ArraySegment<byte>(readBuffer, 0, read);
                        stream.Process(payloadSegment);

                        RebaseOnRtcpTime(stream);
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                            throw ex;
                    }
                }
            });
        }

        private ArraySegment<byte> SerializeRtcpPackets(IEnumerable<RtcpPacket> packets, MemoryStream bufferStream)
        {
            bufferStream.Position = 0;

            foreach (ISerializablePacket report in packets.Cast<ISerializablePacket>())
                report.Serialize(bufferStream);

            byte[] streamBuffer = bufferStream.GetBuffer();
            return new ArraySegment<byte>(streamBuffer, 0, (int)bufferStream.Position);
        }

        private void RebaseOnRtcpTime(ITransportStream stream)
        {
            var rtcpStream = stream as RtcpStream;
            if (rtcpStream?.LastNtpDateTimeReportReceived == null)
                return;

            foreach (var item in _mediaPayloadParser)
                item.BaseTime = rtcpStream.LastNtpDateTimeReportReceived.Value;
            foreach (var item in _streamsMap.Where(s => s.Value is RtpStream).Select(s => s.Value as RtpStream))
                item.ResetTimestamp();
        }
    }
}