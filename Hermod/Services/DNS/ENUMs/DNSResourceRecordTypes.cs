using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// DNS Resource Record Types
    /// </summary>
    public enum DNSResourceRecordTypes : int
    {
        A               = 1,
        NS              = 2,
        CNAME           = 5,
        SOA             = 6,
        MB              = 7,
        MG              = 8,
        MR              = 9,
        NULL            = 10,
        WKS             = 11,
        PTR             = 12,
        HINFO           = 13,
        MINFO           = 14,
        MX              = 15,
        TXT             = 16,
        AAAA            = 28,
        CERT            = 37,
        SSHFP           = 44,
        ANY             = 255
    }

}
