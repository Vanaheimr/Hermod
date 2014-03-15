using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// MailExchange Resource Record
    /// </summary>
    public class MX : ADNSResourceRecord
    {

        public Int32    Preference;
        public String   Exchange;

        public MX(string _Name, DNSResourceRecordTypes _Type, DNSQueryClasses _Class, TimeSpan _TimeToLive, int _Preference, string _Exchange)
            : base(_Name, _Type, _Class, _TimeToLive)
        {
            Preference = _Preference;
            Exchange = _Exchange; 
        }

    }

}
