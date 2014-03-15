using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// A - DNS Resource Record
    /// </summary>
    public class A : ADNSResourceRecord
    {

        #region Properties

        private readonly IPv4Address _IPv4Address;

        public IPv4Address IPv4Address
        {
            get
            {
                return _IPv4Address;
            }
        }

        #endregion

        #region Constructor

        public A(String           Name,
                 DNSQueryClasses  Class,
                 TimeSpan         TimeToLive,
                 IPv4Address      IPv4Address)

            : base(Name, DNSResourceRecordTypes.A, Class, TimeToLive, IPv4Address.ToString())

        {
            this._IPv4Address = IPv4Address;
        }

        #endregion

    }

}
