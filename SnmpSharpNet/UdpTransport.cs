// This file is part of SNMP#NET.
// 
// SNMP#NET is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SNMP#NET is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SNMP#NET.  If not, see <http://www.gnu.org/licenses/>.
// 
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SnmpSharpNet
{
    /// <summary>
    /// IP/UDP transport class.
    /// </summary>
    public class UdpTransport : IDisposable
    {
        /// <summary>
        /// Socket
        /// </summary>
        protected Socket _socket;
        /// <summary>
        /// Flag showing if class is using IPv6 or IPv4
        /// </summary>
        protected bool _isIPv6;
        /// <summary>
        /// Internal variable used to disable host IP address/port number check on received SNMP reply packets. If this option is disabled (default)
        /// only replies from the IP address/port number combination to which the request was sent will be accepted as valid packets.
        /// 
        /// This value is set in the AgentParameters class and is only valid for SNMP v1 and v2c requests.
        /// </summary>
        protected bool _noSourceCheck;

        /// <summary>
        /// Constructor. Initializes and binds the Socket class
        /// </summary>
        /// <param name="useV6">Set to true if you wish to initialize the transport for IPv6</param>
        public UdpTransport(bool useV6)
        {
            _isIPv6 = useV6;
            _socket = null;
            initSocket(_isIPv6);
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~UdpTransport()
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }
        /// <summary>
        /// Flag used to determine if class is using IP version 6 (true) or IP version 4 (false)
        /// </summary>
        public bool IsIPv6
        {
            get { return _isIPv6; }
        }
        /// <summary>
        /// Initialize class socket
        /// </summary>
        /// <param name="useV6">Should socket be initialized for IPv6 (true) of IPv4 (false)</param>
        protected void initSocket(bool useV6)
        {
            if (_socket != null)
            {
                Close();
            }
            if (useV6)
            {
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            }
            else
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            IPEndPoint ipEndPoint = new IPEndPoint(_socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            EndPoint ep = (EndPoint)ipEndPoint;
            _socket.Bind(ep);
        }

        /// <summary>
        /// Make sync request using IP/UDP with request timeouts and retries.
        /// </summary>
        /// <param name="peer">SNMP agent IP address</param>
        /// <param name="port">SNMP agent port number</param>
        /// <param name="buffer">Data to send to the agent</param>
        /// <param name="bufferLength">Data length in the buffer</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="retries">Maximum number of retries. 0 = make a single request with no retry attempts</param>
        /// <returns>Byte array returned by the agent. Null on error</returns>
        /// <exception cref="SnmpException">Thrown on request timed out. SnmpException.ErrorCode is set to
        /// SnmpException.RequestTimedOut constant.</exception>
        /// <exception cref="SnmpException">Thrown when IPv4 address is passed to the v6 socket or vice versa</exception>
        public byte[] Request(IPAddress peer, int port, byte[] buffer, int bufferLength, int timeout, int retries)
        {
            if (_socket == null)
            {
                return null; // socket has been closed. no new operations are possible.
            }
            if (_socket.AddressFamily != peer.AddressFamily)
                throw new SnmpException("Invalid address protocol version.");

            IPEndPoint netPeer = new IPEndPoint(peer, port);

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout);
            int recv = 0;
            int retry = 0;
            byte[] inbuffer = new byte[64 * 1024];
            EndPoint remote = (EndPoint)new IPEndPoint(peer.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            while (true)
            {
                try
                {
                    _socket.SendTo(buffer, bufferLength, SocketFlags.None, (EndPoint)netPeer);
                    recv = _socket.ReceiveFrom(inbuffer, ref remote);
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == 10040)
                    {
                        recv = 0; // Packet too large
                    }
                    else if (ex.ErrorCode == 10050)
                    {
                        throw new SnmpNetworkException(ex, "Network error: Destination network is down.");
                    }
                    else if (ex.ErrorCode == 10051)
                    {
                        throw new SnmpNetworkException(ex, "Network error: destination network is unreachable.");
                    }
                    else if (ex.ErrorCode == 10054)
                    {
                        throw new SnmpNetworkException(ex, "Network error: connection reset by peer.");
                    }
                    else if (ex.ErrorCode == 10064)
                    {
                        throw new SnmpNetworkException(ex, "Network error: remote host is down.");
                    }
                    else if (ex.ErrorCode == 10065)
                    {
                        throw new SnmpNetworkException(ex, "Network error: remote host is unreachable.");
                    }
                    else if (ex.ErrorCode == 10061)
                    {
                        throw new SnmpNetworkException(ex, "Network error: connection refused.");
                    }
                    else if (ex.ErrorCode == 10060)
                    {
                        recv = 0; // Connection attempt timed out. Fall through to retry
                    }
                    else
                    {
                        // Assume it is a timeout
                    }
                }
                if (recv > 0)
                {
                    IPEndPoint remEP = remote as IPEndPoint;
                    if (!_noSourceCheck && !remEP.Equals(netPeer))
                    {
                        if (remEP.Address != netPeer.Address)
                        {
                            Console.WriteLine("Address miss-match {0} != {1}", remEP.Address, netPeer.Address);
                        }
                        if (remEP.Port != netPeer.Port)
                        {
                            Console.WriteLine("Port # miss-match {0} != {1}", remEP.Port, netPeer.Port);
                        }
                        /* Not good, we got a response from somebody other then who we requested a response from */
                        retry++;
                        if (retry > retries)
                        {
                            throw new SnmpException(SnmpException.RequestTimedOut, "Request has reached maximum retries.");
                            // return null;
                        }
                    }
                    else
                    {
                        MutableByte buf = new MutableByte(inbuffer, recv);
                        return buf;
                    }
                }
                else
                {
                    retry++;
                    if (retry > retries)
                    {
                        throw new SnmpException(SnmpException.RequestTimedOut, "Request has reached maximum retries.");
                    }
                }
            }
        }

        /// <summary>
        /// Make sync request using IP/UDP with request timeouts and retries.
        /// </summary>
        /// <param name="peer">SNMP agent IP address</param>
        /// <param name="port">SNMP agent port number</param>
        /// <param name="buffer">Data to send to the agent</param>
        /// <param name="bufferLength">Data length in the buffer</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        /// <param name="retries">Maximum number of retries. 0 = make a single request with no retry attempts</param>
        /// <returns>Byte array returned by the agent. Null on error</returns>
        /// <exception cref="SnmpException">Thrown on request timed out. SnmpException.ErrorCode is set to
        /// SnmpException.RequestTimedOut constant.</exception>
        /// <exception cref="SnmpException">Thrown when IPv4 address is passed to the v6 socket or vice versa</exception>
        public async Task<byte[]> RequestAsync(IPAddress peer, int port, byte[] buffer, int bufferLength, int timeout, int retries)
        {
            if (_socket == null)
            {
                return null; // socket has been closed. no new operations are possible.
            }
            if (_socket.AddressFamily != peer.AddressFamily)
                throw new SnmpException("Invalid address protocol version.");

            IPEndPoint netPeer = new IPEndPoint(peer, port);

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout);
            int recv = 0;
            int retry = 0;
            byte[] inbuffer = new byte[64 * 1024];
            EndPoint remote = (EndPoint)new IPEndPoint(peer.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            while (true)
            {
                try
                {
                    await _socket.SendToAsync(buffer, 0, bufferLength, SocketFlags.None, (EndPoint)netPeer);

                    var receiveResult = await _socket.ReceiveFromAsync(inbuffer, 0, inbuffer.Length);
                    remote = receiveResult.RemoteEndpoint;
                    recv = receiveResult.BytesTransferred;
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == 10040)
                    {
                        recv = 0; // Packet too large
                    }
                    else if (ex.ErrorCode == 10050)
                    {
                        throw new SnmpNetworkException(ex, "Network error: Destination network is down.");
                    }
                    else if (ex.ErrorCode == 10051)
                    {
                        throw new SnmpNetworkException(ex, "Network error: destination network is unreachable.");
                    }
                    else if (ex.ErrorCode == 10054)
                    {
                        throw new SnmpNetworkException(ex, "Network error: connection reset by peer.");
                    }
                    else if (ex.ErrorCode == 10064)
                    {
                        throw new SnmpNetworkException(ex, "Network error: remote host is down.");
                    }
                    else if (ex.ErrorCode == 10065)
                    {
                        throw new SnmpNetworkException(ex, "Network error: remote host is unreachable.");
                    }
                    else if (ex.ErrorCode == 10061)
                    {
                        throw new SnmpNetworkException(ex, "Network error: connection refused.");
                    }
                    else if (ex.ErrorCode == 10060)
                    {
                        recv = 0; // Connection attempt timed out. Fall through to retry
                    }
                    else
                    {
                        // Assume it is a timeout
                    }
                }
                if (recv > 0)
                {
                    IPEndPoint remEP = remote as IPEndPoint;
                    if (!_noSourceCheck && !remEP.Equals(netPeer))
                    {
                        if (remEP.Address != netPeer.Address)
                        {
                            Console.WriteLine("Address miss-match {0} != {1}", remEP.Address, netPeer.Address);
                        }
                        if (remEP.Port != netPeer.Port)
                        {
                            Console.WriteLine("Port # miss-match {0} != {1}", remEP.Port, netPeer.Port);
                        }
                        /* Not good, we got a response from somebody other then who we requested a response from */
                        retry++;
                        if (retry > retries)
                        {
                            throw new SnmpException(SnmpException.RequestTimedOut, "Request has reached maximum retries.");
                            // return null;
                        }
                    }
                    else
                    {
                        MutableByte buf = new MutableByte(inbuffer, recv);
                        return buf;
                    }
                }
                else
                {
                    retry++;
                    if (retry > retries)
                    {
                        throw new SnmpException(SnmpException.RequestTimedOut, "Request has reached maximum retries.");
                    }
                }
            }
        }

        /// <summary>
        /// Dispose of the class.
        /// </summary>
        public void Dispose()
        {
            Close();
        }
        /// <summary>
        /// Close network socket
        /// </summary>
        public void Close()
        {
            if (_socket != null)
            {
                try
                {
                    _socket.Close();
                }
                catch
                {
                }
                _socket = null;
            }
        }
    }
}
