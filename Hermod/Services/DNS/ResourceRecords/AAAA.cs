using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// AAAA - DNS Resource Record
    /// </summary>
    public class AAAA : ADNSResourceRecord
    {

        #region Properties

        private readonly IPv6Address _IPv6Address;

        public IPv6Address IPv6Address
        {
            get
            {
                return _IPv6Address;
            }
        }

        #endregion

        #region Constructor

        public AAAA(String           Name,
                    DNSQueryClasses  Class,
                    TimeSpan         TimeToLive,
                    IPv6Address      IPv6Address)

            : base(Name, DNSResourceRecordTypes.A, Class, TimeToLive, IPv6Address.ToString())

        {
            this._IPv6Address = IPv6Address;
        }

        #endregion

    }

}
