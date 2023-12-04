using System.Net.Sockets;

namespace RtspClientSharp.Utils
{
    static class NetworkClientFactory
    {
        private const int TcpReceiveBufferDefaultSize = 64 * 1024;
        private const int UdpReceiveBufferDefaultSize = 1024 * 1024; // 1Mo instead of 128ko. Some packets were missed with 128ko.
        private const int SIO_UDP_CONNRESET = -1744830452;
        private static readonly byte[] EmptyOptionInValue = { 0, 0, 0, 0 };

        public static Socket CreateTcpClient()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveBufferSize = TcpReceiveBufferDefaultSize,
                DualMode = true,
                NoDelay = true
            };
            return socket;
        }

        public static Socket CreateUdpClient()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveBufferSize = UdpReceiveBufferDefaultSize,
                DualMode = true
            };
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, EmptyOptionInValue, null);
            return socket;
        }
    }
}