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
    /// <summary>Transport class for IPv4 using UDP</summary>
    /// <remarks>
    /// InternetProtocol version 4 User Datagram Protocol (IP/UDP) transport protocol
    /// implementation for use with SNMP versions 1, 2 and 3.
    /// </remarks>
    public class UdpTarget : UdpTransport, IDisposable
    {
        /// <summary>
        /// SNMP request target host IP address
        /// </summary>
        protected IPAddress _address;
        /// <summary>
        /// Maximum number of retries. Value of 0 (zero) will result in a single request without
        /// retries.
        /// </summary>
        protected int _retry;
        /// <summary>
        /// SNMP target UDP port number
        /// </summary>
        protected int _port;
        /// <summary>
        /// SNMP request timeout value in milliseconds
        /// </summary>
        protected int _timeout;
        /// <summary>
        /// Get/Set Udp agent IP address
        /// </summary>
        public System.Net.IPAddress Address
        {
            get { return _address; }
            set
            {
                _address = value;
                if (_address.AddressFamily == AddressFamily.InterNetworkV6 && !IsIPv6)
                    initSocket(true);
                else if (_address.AddressFamily == AddressFamily.InterNetwork && IsIPv6)
                    initSocket(false);
            }
        }
        /// <summary>
        /// Get/Set Udp agent port number
        /// </summary>
        public int Port
        {
            get { return _port; }
            set { _port = value; }
        }
        /// <summary>
        /// Get/Set Udp agent timeout value in milliseconds
        /// </summary>
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }
        /// <summary>
        /// Get/Set Udp agent maximum retry value. Value of 0 (zero) will result in a single request
        /// being sent without further retry attempts.
        /// </summary>
        public int Retry
        {
            get { return _retry; }
            set { _retry = value; }
        }
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="peer">SNMP peer IP address</param>
        /// <param name="port">SNMP peer UDP port number</param>
        /// <param name="timeout">SNMP peer timeout in milliseconds</param>
        /// <param name="retry">SNMP peer maximum retires setting. Value of 0 will result in a single request with no retries.</param>
        public UdpTarget(IPAddress peer, int port, int timeout, int retry)
            : base(peer.AddressFamily == AddressFamily.InterNetworkV6)
        {
            _address = peer;
            _port = port;
            _timeout = timeout;
            _retry = retry;
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>
        /// Initializes the class with defaults for timeout (2000ms = 2 seconds), retry (two) and agent UDP port
        /// number (161).
        /// </remarks>
        /// <param name="peer">Agent IP address</param>
        public UdpTarget(IPAddress peer)
            : base(peer.AddressFamily == AddressFamily.InterNetworkV6)
        {
            _address = peer;
            _port = 161;
            _timeout = 2000;
            _retry = 2;
        }

        /// <summary>Make SNMP Request</summary>
        /// <remarks>
        /// Make SNMP request. With this method you can make blocked SNMP version 1, 2 and 3 requests of type GET,
        /// GET-NEXT, GET-BULK, SET and REPORT (request types have to compatible with the SNMP protocol version you
        /// are using).
        /// 
        /// This method will pass through any exceptions thrown by parsing classes/methods so see individual packet
        /// classes, ASN.1 type classes, authentication, privacy, etc. classes for exceptions thrown.
        /// </remarks>
        /// <param name="pdu">Pdu class (do not pass ScopedPdu)</param>
        /// <param name="agentParameters">Security information for the request. Use <see cref="AgentParameters"/>
        /// for SNMP versions 1 and 2 requests. Use <see cref="SecureAgentParameters"/> for SNMP version 3
        /// requests.</param>
        /// <returns>Appropriate SNMP packet class for the reply received (<see cref="SnmpV1Packet"/>, 
        /// <see cref="SnmpV2Packet"/>, or <see cref="SnmpV3Packet"/>. Null value if there was an error
        /// with the request.</returns>
        /// <exception cref="SnmpAuthenticationException">Thrown on SNMPv3 requests when authentication password
        /// is not specified on authNoPriv or authPriv requests in SecureAgentParameters or if incoming packet 
        /// authentication check failed.
        /// 
        /// With SNMP ver1 and ver2c, authentication check fails when invalid community name is parsed in the reply.</exception>
        /// <exception cref="SnmpPrivacyException">Thrown on SNMPv3 requests when privacy password is not
        /// specified in SecureAgentParameters on authPriv requests.</exception>
        /// <exception cref="SnmpException">Thrown in following cases:
        /// 
        /// * IAgentParameters.Valid() returned false. SnmpException.ErrorCode is set to SnmpException.InvalidIAgentParameters
        /// * No data received on request. SnmpException.ErrorCode is set to SnmpException.NoDataReceived
        /// * Invalid RequestId in reply. SnmpException.ErrorCode is set to SnmpException.InvalidRequestId
        /// </exception>
        public SnmpPacket Request(Pdu pdu, IAgentParameters agentParameters)
        {
            byte[] outPacket;
            if (agentParameters.Version == SnmpVersion.Ver3)
            {
                SecureAgentParameters secparams = (SecureAgentParameters)agentParameters;
                if (secparams.Authentication != AuthenticationDigests.None && secparams.AuthenticationSecret.Length <= 0)
                    throw new SnmpAuthenticationException("Authentication password not specified.");
                if (secparams.Privacy != PrivacyProtocols.None && secparams.PrivacySecret.Length <= 0)
                    throw new SnmpPrivacyException("Privacy password not specified.");
                _noSourceCheck = false; // this option is not valid for SNMP v3 requests
                ScopedPdu outPdu = new ScopedPdu(pdu);
                SnmpV3Packet packet = new SnmpV3Packet(outPdu);
                secparams.InitializePacket(packet);
                if (secparams.HasCachedKeys)
                    outPacket = packet.encode(secparams.AuthenticationKey, secparams.PrivacyKey);
                else
                    outPacket = packet.encode();
            }
            else if (agentParameters.Version == SnmpVersion.Ver1)
            {
                AgentParameters param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters, "Invalid AgentParameters. Unable to process request.");
                SnmpV1Packet packet = new SnmpV1Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                outPacket = packet.encode();
                _noSourceCheck = param.DisableReplySourceCheck;
            }
            else if (agentParameters.Version == SnmpVersion.Ver2)
            {
                AgentParameters param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters, "Invalid AgentParameters. Unable to process request.");
                SnmpV2Packet packet = new SnmpV2Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                _noSourceCheck = param.DisableReplySourceCheck;
                outPacket = packet.encode();
            }
            else
            {
                throw new SnmpInvalidVersionException("Unsupported SNMP version.");
            }

            byte[] inBuffer = Request(_address, _port, outPacket, outPacket.Length, _timeout, _retry);

            if (inBuffer == null || inBuffer.Length <= 0)
            {
                throw new SnmpException(SnmpException.NoDataReceived, "No data received on request.");
            }
            // verify packet
            if (agentParameters.Version == SnmpVersion.Ver1)
            {
                SnmpV1Packet packet = new SnmpV1Packet();
                AgentParameters param = (AgentParameters)agentParameters;
                packet.decode(inBuffer, inBuffer.Length);
                if (packet.Community != param.Community)
                {
                    // invalid community name received. Ignore the rest of the packet
                    throw new SnmpAuthenticationException("Invalid community name in reply.");
                }
                if (packet.Pdu.RequestId != pdu.RequestId)
                {
                    // invalid request id. unmatched response ignored
                    throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
                }
                return packet;
            }
            else if (agentParameters.Version == SnmpVersion.Ver2)
            {
                SnmpV2Packet packet = new SnmpV2Packet();
                AgentParameters param = (AgentParameters)agentParameters;
                packet.decode(inBuffer, inBuffer.Length);
                if (packet.Community != param.Community)
                {
                    // invalid community name received. Ignore the rest of the packet
                    throw new SnmpAuthenticationException("Invalid community name in reply.");
                }
                if (packet.Pdu.RequestId != pdu.RequestId)
                {
                    // invalid request id. unmatched response ignored
                    throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
                }
                return packet;
            }
            else if (agentParameters.Version == SnmpVersion.Ver3)
            {
                SnmpV3Packet packet = new SnmpV3Packet();
                SecureAgentParameters secparams = (SecureAgentParameters)agentParameters;
                secparams.InitializePacket(packet);
                if (secparams.HasCachedKeys)
                    packet.decode(inBuffer, inBuffer.Length, secparams.AuthenticationKey, secparams.PrivacyKey);
                else
                    packet.decode(inBuffer, inBuffer.Length);
                // first check if packet is a discovery response and process it
                if (packet.Pdu.Type == PduType.Report && packet.Pdu.VbCount > 0 && packet.Pdu.VbList[0].Oid.Equals(SnmpConstants.usmStatsUnknownEngineIDs))
                {
                    secparams.UpdateDiscoveryValues(packet);
                    return packet;
                }
                else
                {
                    if (!secparams.ValidateIncomingPacket(packet))
                    {
                        return null;
                    }
                    else
                    {
                        secparams.UpdateDiscoveryValues(packet); // update time, etc. values
                        return packet;
                    }
                }
            }
            return null;
        }

        /// <summary>Make SNMP Request</summary>
		/// <remarks>
		/// Make SNMP request. With this method you can make blocked SNMP version 1, 2 and 3 requests of type GET,
		/// GET-NEXT, GET-BULK, SET and REPORT (request types have to compatible with the SNMP protocol version you
		/// are using).
		/// 
		/// This method will pass through any exceptions thrown by parsing classes/methods so see individual packet
		/// classes, ASN.1 type classes, authentication, privacy, etc. classes for exceptions thrown.
		/// </remarks>
		/// <param name="pdu">Pdu class (do not pass ScopedPdu)</param>
		/// <param name="agentParameters">Security information for the request. Use <see cref="AgentParameters"/>
		/// for SNMP versions 1 and 2 requests. Use <see cref="SecureAgentParameters"/> for SNMP version 3
		/// requests.</param>
		/// <returns>Appropriate SNMP packet class for the reply received (<see cref="SnmpV1Packet"/>, 
		/// <see cref="SnmpV2Packet"/>, or <see cref="SnmpV3Packet"/>. Null value if there was an error
		/// with the request.</returns>
		/// <exception cref="SnmpAuthenticationException">Thrown on SNMPv3 requests when authentication password
		/// is not specified on authNoPriv or authPriv requests in SecureAgentParameters or if incoming packet 
		/// authentication check failed.
		/// 
		/// With SNMP ver1 and ver2c, authentication check fails when invalid community name is parsed in the reply.</exception>
		/// <exception cref="SnmpPrivacyException">Thrown on SNMPv3 requests when privacy password is not
		/// specified in SecureAgentParameters on authPriv requests.</exception>
		/// <exception cref="SnmpException">Thrown in following cases:
		/// 
		/// * IAgentParameters.Valid() returned false. SnmpException.ErrorCode is set to SnmpException.InvalidIAgentParameters
		/// * No data received on request. SnmpException.ErrorCode is set to SnmpException.NoDataReceived
		/// * Invalid RequestId in reply. SnmpException.ErrorCode is set to SnmpException.InvalidRequestId
		/// </exception>
		public async Task<SnmpPacket> RequestAsync(Pdu pdu, IAgentParameters agentParameters)
        {
            byte[] outPacket;
            if (agentParameters.Version == SnmpVersion.Ver3)
            {
                SecureAgentParameters secparams = (SecureAgentParameters)agentParameters;
                if (secparams.Authentication != AuthenticationDigests.None && secparams.AuthenticationSecret.Length <= 0)
                    throw new SnmpAuthenticationException("Authentication password not specified.");
                if (secparams.Privacy != PrivacyProtocols.None && secparams.PrivacySecret.Length <= 0)
                    throw new SnmpPrivacyException("Privacy password not specified.");
                _noSourceCheck = false; // this option is not valid for SNMP v3 requests
                ScopedPdu outPdu = new ScopedPdu(pdu);
                SnmpV3Packet packet = new SnmpV3Packet(outPdu);
                secparams.InitializePacket(packet);
                if (secparams.HasCachedKeys)
                    outPacket = packet.encode(secparams.AuthenticationKey, secparams.PrivacyKey);
                else
                    outPacket = packet.encode();
            }
            else if (agentParameters.Version == SnmpVersion.Ver1)
            {
                AgentParameters param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters, "Invalid AgentParameters. Unable to process request.");
                SnmpV1Packet packet = new SnmpV1Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                outPacket = packet.encode();
                _noSourceCheck = param.DisableReplySourceCheck;
            }
            else if (agentParameters.Version == SnmpVersion.Ver2)
            {
                AgentParameters param = (AgentParameters)agentParameters;
                if (!param.Valid())
                    throw new SnmpException(SnmpException.InvalidIAgentParameters, "Invalid AgentParameters. Unable to process request.");
                SnmpV2Packet packet = new SnmpV2Packet();
                packet.Pdu.Set(pdu);
                packet.Community.Set(param.Community);
                _noSourceCheck = param.DisableReplySourceCheck;
                outPacket = packet.encode();
            }
            else
            {
                throw new SnmpInvalidVersionException("Unsupported SNMP version.");
            }

            byte[] inBuffer = await RequestAsync(_address, _port, outPacket, outPacket.Length, _timeout, _retry);

            if (inBuffer == null || inBuffer.Length <= 0)
            {
                throw new SnmpException(SnmpException.NoDataReceived, "No data received on request.");
            }
            // verify packet
            if (agentParameters.Version == SnmpVersion.Ver1)
            {
                SnmpV1Packet packet = new SnmpV1Packet();
                AgentParameters param = (AgentParameters)agentParameters;
                packet.decode(inBuffer, inBuffer.Length);
                if (packet.Community != param.Community)
                {
                    // invalid community name received. Ignore the rest of the packet
                    throw new SnmpAuthenticationException("Invalid community name in reply.");
                }
                if (packet.Pdu.RequestId != pdu.RequestId)
                {
                    // invalid request id. unmatched response ignored
                    throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
                }
                return packet;
            }
            else if (agentParameters.Version == SnmpVersion.Ver2)
            {
                SnmpV2Packet packet = new SnmpV2Packet();
                AgentParameters param = (AgentParameters)agentParameters;
                packet.decode(inBuffer, inBuffer.Length);
                if (packet.Community != param.Community)
                {
                    // invalid community name received. Ignore the rest of the packet
                    throw new SnmpAuthenticationException("Invalid community name in reply.");
                }
                if (packet.Pdu.RequestId != pdu.RequestId)
                {
                    // invalid request id. unmatched response ignored
                    throw new SnmpException(SnmpException.InvalidRequestId, "Invalid request id in reply.");
                }
                return packet;
            }
            else if (agentParameters.Version == SnmpVersion.Ver3)
            {
                SnmpV3Packet packet = new SnmpV3Packet();
                SecureAgentParameters secparams = (SecureAgentParameters)agentParameters;
                secparams.InitializePacket(packet);
                if (secparams.HasCachedKeys)
                    packet.decode(inBuffer, inBuffer.Length, secparams.AuthenticationKey, secparams.PrivacyKey);
                else
                    packet.decode(inBuffer, inBuffer.Length);
                // first check if packet is a discovery response and process it
                if (packet.Pdu.Type == PduType.Report && packet.Pdu.VbCount > 0 && packet.Pdu.VbList[0].Oid.Equals(SnmpConstants.usmStatsUnknownEngineIDs))
                {
                    secparams.UpdateDiscoveryValues(packet);
                    return packet;
                }
                else
                {
                    if (!secparams.ValidateIncomingPacket(packet))
                    {
                        return null;
                    }
                    else
                    {
                        secparams.UpdateDiscoveryValues(packet); // update time, etc. values
                        return packet;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Perform SNMP version 3 discovery operation. This is the first operation that needs to be
        /// performed on a newly accessed agent to retrieve agentId, agentBoots and agentTime values, critical
        /// for further authentication and privacy operations.
        /// </summary>
        /// <param name="param"><see cref="SecureAgentParameters"/> class instance that will be updated
        /// with discovered agent values. This class with be reset to its defaults prior to agent
        /// discovered values so do not store any critical information in it prior to calling the
        /// discovery method</param>
        /// <returns>True if discovery operation was a success, otherwise false</returns>
        public bool Discovery(SecureAgentParameters param)
        {
            param.Reset();
            param.SecurityName.Set("");
            param.Reportable = true;
            Pdu pdu = new Pdu(); // just leave everything at default.
            SnmpV3Packet inpkt = (SnmpV3Packet)Request(pdu, param);
            if (inpkt != null)
            {
                if (inpkt.USM.EngineBoots == 0 && inpkt.USM.EngineTime == 0)
                {
                    inpkt = (SnmpV3Packet)Request(pdu, param);
                    if (inpkt != null)
                        return true;
                }
                else
                    return true;
            }
            return false;
        }
    }
}
