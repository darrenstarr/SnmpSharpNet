namespace SnmpSharpNet
{
    using System;
    using System.Net;
    using System.Threading.Tasks;

    public static class Helpers
    {
        public static AsnType Get(IPAddress agent, string community, string oid)
        {
            // Define agent parameters class
            AgentParameters agentParameters = new AgentParameters(SnmpVersion.Ver2, new OctetString(community));

            // Construct target
            UdpTarget target = new UdpTarget(agent, 161, 2000, 1);

            // Pdu class used for all requests
            Pdu pdu = new Pdu(PduType.Get);
            pdu.VbList.Add(oid); //sysDescr

            // Make SNMP request
            var snmpResponse = target.Request(pdu, agentParameters) as SnmpV2Packet;

            AsnType result = null;

            // If result is null then agent didn't reply or we couldn't parse the reply.
            if (snmpResponse != null)
            {
                // ErrorStatus other then 0 is an error returned by 
                // the Agent - see SnmpConstants for error definitions
                if (snmpResponse.Pdu.ErrorStatus != 0)
                {
                    // agent reported an error with the request
                    throw new Exception($"Error in SNMP reply. Error {snmpResponse.Pdu.ErrorStatus} index {snmpResponse.Pdu.ErrorIndex}");
                }
                else
                {
                    result = snmpResponse.Pdu.VbList[0].Value;
                }
            }

            target.Close();
            return result;
        }

        public async static Task<AsnType> GetAsync(IPAddress agent, string community, string oid)
        {
            // Define agent parameters class
            AgentParameters agentParameters = new AgentParameters(SnmpVersion.Ver2, new OctetString(community));

            // Construct target
            UdpTarget target = new UdpTarget(agent, 161, 2000, 1);

            // Pdu class used for all requests
            Pdu pdu = new Pdu(PduType.Get);
            pdu.VbList.Add(oid); //sysDescr

            // Make SNMP request
            var snmpResponse = await target.RequestAsync(pdu, agentParameters) as SnmpV2Packet;

            AsnType result = null;

            // If result is null then agent didn't reply or we couldn't parse the reply.
            if (snmpResponse != null)
            {
                // ErrorStatus other then 0 is an error returned by 
                // the Agent - see SnmpConstants for error definitions
                if (snmpResponse.Pdu.ErrorStatus != 0)
                {
                    // agent reported an error with the request
                    throw new Exception($"Error in SNMP reply. Error {snmpResponse.Pdu.ErrorStatus} index {snmpResponse.Pdu.ErrorIndex}");
                }
                else
                {
                    result = snmpResponse.Pdu.VbList[0].Value;
                }
            }

            target.Close();
            return result;
        }

        public static bool Set(IPAddress agent, string community, string oid, int value, Func<Oid, string, bool> handleValue)
        {
            // Prepare target
            UdpTarget target = new UdpTarget(agent);
            // Create a SET PDU
            Pdu pdu = new Pdu(PduType.Set);

            var oidValue = new Oid(oid);

            // Set a value to integer
            pdu.VbList.Add(oidValue, new Integer32(value));
            // Set Agent security parameters
            AgentParameters aparam = new AgentParameters(SnmpVersion.Ver2, new OctetString(community));
            // Response packet
            SnmpV2Packet response;
            try
            {
                // Send request and wait for response
                response = target.Request(pdu, aparam) as SnmpV2Packet;
            }
            catch (Exception)
            {
                // If exception happens, it will be returned here
                //System.Diagnostics.Debug.WriteLine(String.Format("Request failed with exception: {0}", ex.Message));
                target.Close();
                return false;
            }
            // Make sure we received a response
            if (response == null)
            {
                //System.Diagnostics.Debug.WriteLine("Error in sending SNMP request.");
            }
            else
            {
                // Check if we received an SNMP error from the agent
                if (response.Pdu.ErrorStatus != 0)
                {
                    //System.Diagnostics.Debug.WriteLine(String.Format("SNMP agent returned ErrorStatus {0} on index {1}",
                    //    response.Pdu.ErrorStatus, response.Pdu.ErrorIndex));
                }
                else
                {
                    // Everything is ok. Agent will return the new value for the OID we changed
                    //System.Diagnostics.Debug.WriteLine(String.Format("Agent response {0}: {1}",
                    //    response.Pdu[0].Oid.ToString(), response.Pdu[0].Value.ToString()));

                    handleValue(oidValue, response.Pdu[0].Value.ToString());
                }
            }

            return true;
        }

        public async static Task<bool> SetAsync(IPAddress agent, string community, string oid, int value, Func<Oid, string, bool> handleValue)
        {
            // Prepare target
            UdpTarget target = new UdpTarget(agent);
            // Create a SET PDU
            Pdu pdu = new Pdu(PduType.Set);

            var oidValue = new Oid(oid);

            // Set a value to integer
            pdu.VbList.Add(oidValue, new Integer32(value));
            // Set Agent security parameters
            AgentParameters aparam = new AgentParameters(SnmpVersion.Ver2, new OctetString(community));
            // Response packet
            SnmpV2Packet response;
            try
            {
                // Send request and wait for response
                response = await target.RequestAsync(pdu, aparam) as SnmpV2Packet;
            }
            catch (Exception)
            {
                // If exception happens, it will be returned here
                //System.Diagnostics.Debug.WriteLine(String.Format("Request failed with exception: {0}", ex.Message));
                target.Close();
                return false;
            }
            // Make sure we received a response
            if (response == null)
            {
                //System.Diagnostics.Debug.WriteLine("Error in sending SNMP request.");
            }
            else
            {
                // Check if we received an SNMP error from the agent
                if (response.Pdu.ErrorStatus != 0)
                {
                    //System.Diagnostics.Debug.WriteLine(String.Format("SNMP agent returned ErrorStatus {0} on index {1}",
                    //    response.Pdu.ErrorStatus, response.Pdu.ErrorIndex));
                }
                else
                {
                    // Everything is ok. Agent will return the new value for the OID we changed
                    //System.Diagnostics.Debug.WriteLine(String.Format("Agent response {0}: {1}",
                    //    response.Pdu[0].Oid.ToString(), response.Pdu[0].Value.ToString()));

                    handleValue(oidValue, response.Pdu[0].Value.ToString());
                }
            }

            return true;
        }

        public static bool Walk(IPAddress agent, string community, string oid, Func<Vb, bool> handleValue, int version = 2)
        {
            if (version == 1)
                return WalkGetNext(agent, community, oid, handleValue);

            return WalkGetBulk(agent, community, oid, handleValue);
        }

        public static async Task<bool> WalkAsync(IPAddress agent, string community, string oid, Func<Vb, bool> handleValue, int version = 2)
        {
            if (version == 1)
                return WalkGetNext(agent, community, oid, handleValue);

            return await WalkGetBulkAsync(agent, community, oid, handleValue);
        }

        private static bool WalkGetNext(IPAddress agent, string community, string oid, Func<Vb, bool> handleValue)
        {
            // SNMP community name
            OctetString communityString = new OctetString(community);

            // Define agent parameters class
            AgentParameters param = new AgentParameters(communityString)
            {
                // Set SNMP version to 1
                Version = SnmpVersion.Ver1
            };

            // Construct target
            UdpTarget target = new UdpTarget(agent, 161, 2000, 1);

            // Define Oid that is the root of the MIB
            //  tree you wish to retrieve
            Oid rootOid = new Oid(oid); // ifDescr

            // This Oid represents last Oid returned by
            //  the SNMP agent
            Oid lastOid = (Oid)rootOid.Clone();

            // Pdu class used for all requests
            Pdu pdu = new Pdu(PduType.GetNext);

            // Loop through results
            while (lastOid != null)
            {
                // When Pdu class is first constructed, RequestId is set to a random value
                // that needs to be incremented on subsequent requests made using the
                // same instance of the Pdu class.
                if (pdu.RequestId != 0)
                {
                    pdu.RequestId += 1;
                }
                // Clear Oids from the Pdu class.
                pdu.VbList.Clear();
                // Initialize request PDU with the last retrieved Oid
                pdu.VbList.Add(lastOid);
                // Make SNMP request
                SnmpV1Packet result = (SnmpV1Packet)target.Request(pdu, param);
                // You should catch exceptions in the Request if using in real application.

                // If result is null then agent didn't reply or we couldn't parse the reply.
                if (result != null)
                {
                    // ErrorStatus other then 0 is an error returned by 
                    // the Agent - see SnmpConstants for error definitions
                    if (result.Pdu.ErrorStatus != 0)
                    {
                        // agent reported an error with the request
                        //System.Diagnostics.Debug.WriteLine("Error in SNMP reply. Error {0} index {1}",
                        //    result.Pdu.ErrorStatus,
                        //    result.Pdu.ErrorIndex);
                        lastOid = null;
                        break;
                    }
                    else
                    {
                        // Walk through returned variable bindings
                        foreach (Vb v in result.Pdu.VbList)
                        {
                            // Check that retrieved Oid is "child" of the root OID
                            if (rootOid.IsRootOf(v.Oid))
                            {
                                //System.Diagnostics.Debug.WriteLine("{0} ({1}): {2}",
                                //    v.Oid.ToString(),
                                //    SnmpConstants.GetTypeName(v.Value.Type),
                                //    v.Value.ToString());

                                handleValue(v);

                                lastOid = v.Oid;
                            }
                            else
                            {
                                // we have reached the end of the requested
                                // MIB tree. Set lastOid to null and exit loop
                                lastOid = null;
                            }
                        }
                    }
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine("No response received from SNMP agent.");
                }
            }
            target.Close();
            return true;
        }

        private static bool WalkGetBulk(IPAddress agent, string community, string oid, Func<Vb, bool> handleValue)
        {
            OctetString communityString = new OctetString(community);

            // Define agent parameters class
            AgentParameters param = new AgentParameters(communityString);
            // Set SNMP version to 2 (GET-BULK only works with SNMP ver 2 and 3)
            param.Version = SnmpVersion.Ver2;

            // Construct target
            UdpTarget target = new UdpTarget(agent, 161, 2000, 1);

            // Define Oid that is the root of the MIB
            //  tree you wish to retrieve
            Oid rootOid = new Oid(oid); // ifDescr

            // This Oid represents last Oid returned by
            //  the SNMP agent
            Oid lastOid = (Oid)rootOid.Clone();

            // Pdu class used for all requests
            Pdu pdu = new Pdu(PduType.GetBulk)
            {
                // In this example, set NonRepeaters value to 0
                NonRepeaters = 0,
                // MaxRepetitions tells the agent how many Oid/Value pairs to return
                // in the response.
                MaxRepetitions = 5
            };

            // Loop through results
            while (lastOid != null)
            {
                // When Pdu class is first constructed, RequestId is set to 0
                // and during encoding id will be set to the random value
                // for subsequent requests, id will be set to a value that
                // needs to be incremented to have unique request ids for each
                // packet
                if (pdu.RequestId != 0)
                {
                    pdu.RequestId += 1;
                }
                // Clear Oids from the Pdu class.
                pdu.VbList.Clear();
                // Initialize request PDU with the last retrieved Oid
                pdu.VbList.Add(lastOid);
                // Make SNMP request
                //var result = (SnmpV1Packet)target.Request(pdu, param);
                SnmpV2Packet result = (SnmpV2Packet)target.Request(pdu, param);
                // You should catch exceptions in the Request if using in real application.

                // If result is null then agent didn't reply or we couldn't parse the reply.
                if (result != null)
                {
                    // ErrorStatus other then 0 is an error returned by 
                    // the Agent - see SnmpConstants for error definitions
                    if (result.Pdu.ErrorStatus != 0)
                    {
                        // agent reported an error with the request
                        //System.Diagnostics.Debug.WriteLine("Error in SNMP reply. Error {0} index {1}",
                        //    result.Pdu.ErrorStatus,
                        //    result.Pdu.ErrorIndex);
                        lastOid = null;
                        break;
                    }
                    else
                    {
                        // Walk through returned variable bindings
                        foreach (Vb v in result.Pdu.VbList)
                        {
                            // Check that retrieved Oid is "child" of the root OID
                            if (rootOid.IsRootOf(v.Oid))
                            {
                                //System.Diagnostics.Debug.WriteLine("{0} ({1}): {2}",
                                //    v.Oid.ToString(),
                                //    SnmpConstants.GetTypeName(v.Value.Type),
                                //    v.Value.ToString());

                                handleValue(v);

                                if (v.Value.Type == SnmpConstants.SMI_ENDOFMIBVIEW)
                                    lastOid = null;
                                else
                                    lastOid = v.Oid;
                            }
                            else
                            {
                                // we have reached the end of the requested
                                // MIB tree. Set lastOid to null and exit loop
                                lastOid = null;
                            }
                        }
                    }
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine("No response received from SNMP agent.");
                }
            }
            target.Close();
            return true;
        }

        public const int SIO_UDP_CONNRESET = -1744830452;

        private static async Task<bool> WalkGetBulkAsync(IPAddress agent, string community, string oid, Func<Vb, bool> handleValue)
        {
            OctetString communityString = new OctetString(community);

            // Define agent parameters class
            AgentParameters param = new AgentParameters(communityString);
            // Set SNMP version to 2 (GET-BULK only works with SNMP ver 2 and 3)
            param.Version = SnmpVersion.Ver2;

            // Construct target
            UdpTarget target = new UdpTarget(agent, 161, 2000, 1);

            // Define Oid that is the root of the MIB
            //  tree you wish to retrieve
            Oid rootOid = new Oid(oid); // ifDescr

            // This Oid represents last Oid returned by
            //  the SNMP agent
            Oid lastOid = (Oid)rootOid.Clone();

            // Pdu class used for all requests
            Pdu pdu = new Pdu(PduType.GetBulk)
            {
                // In this example, set NonRepeaters value to 0
                NonRepeaters = 0,
                // MaxRepetitions tells the agent how many Oid/Value pairs to return
                // in the response.
                MaxRepetitions = 5
            };

            // Loop through results
            while (lastOid != null)
            {
                // When Pdu class is first constructed, RequestId is set to 0
                // and during encoding id will be set to the random value
                // for subsequent requests, id will be set to a value that
                // needs to be incremented to have unique request ids for each
                // packet
                if (pdu.RequestId != 0)
                {
                    pdu.RequestId += 1;
                }
                // Clear Oids from the Pdu class.
                pdu.VbList.Clear();
                // Initialize request PDU with the last retrieved Oid
                pdu.VbList.Add(lastOid);
                // Make SNMP request
                //var result = (SnmpV1Packet)target.Request(pdu, param);
                SnmpV2Packet result = (SnmpV2Packet)(await target.RequestAsync(pdu, param));

                // You should catch exceptions in the Request if using in real application.

                // If result is null then agent didn't reply or we couldn't parse the reply.
                if (result != null)
                {
                    // ErrorStatus other then 0 is an error returned by 
                    // the Agent - see SnmpConstants for error definitions
                    if (result.Pdu.ErrorStatus != 0)
                    {
                        // agent reported an error with the request
                        //System.Diagnostics.Debug.WriteLine("Error in SNMP reply. Error {0} index {1}",
                        //    result.Pdu.ErrorStatus,
                        //    result.Pdu.ErrorIndex);
                        lastOid = null;
                        break;
                    }
                    else
                    {
                        // Walk through returned variable bindings
                        foreach (Vb v in result.Pdu.VbList)
                        {
                            // Check that retrieved Oid is "child" of the root OID
                            if (rootOid.IsRootOf(v.Oid))
                            {
                                //System.Diagnostics.Debug.WriteLine("{0} ({1}): {2}",
                                //    v.Oid.ToString(),
                                //    SnmpConstants.GetTypeName(v.Value.Type),
                                //    v.Value.ToString());

                                handleValue(v);

                                if (v.Value.Type == SnmpConstants.SMI_ENDOFMIBVIEW)
                                    lastOid = null;
                                else
                                    lastOid = v.Oid;
                            }
                            else
                            {
                                // we have reached the end of the requested
                                // MIB tree. Set lastOid to null and exit loop
                                lastOid = null;
                            }
                        }
                    }
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine("No response received from SNMP agent.");
                }
            }
            target.Close();
            return true;
        }
    }
}
