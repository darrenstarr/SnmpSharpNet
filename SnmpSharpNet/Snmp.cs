namespace SnmpSharpNet
{
    public class Snmp:UdpTransport
    {
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
