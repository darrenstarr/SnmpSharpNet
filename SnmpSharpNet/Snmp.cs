using System;
using System.Collections.Generic;
using System.Text;

namespace SnmpSharpNet
{
	public class Snmp:UdpTransport
	{
		/// <summary>
		/// Internal event to send result of the async request to.
		/// </summary>
		protected event SnmpAsyncResponse _response;

		/// <summary>
		/// Internal storage for request target information.
		/// </summary>
		protected ITarget _target = null;

		#region Constructor(s)

        public Snmp() : base(false)
        {
        }

		/// <summary>
		/// Constructor
		/// </summary>
		public Snmp(bool useV6)
			:base(useV6)
		{
		}

		#endregion Constructor(s)
	}
}
