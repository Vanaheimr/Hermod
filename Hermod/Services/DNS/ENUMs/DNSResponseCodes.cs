using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// Query Result/Response Codes
    /// </summary>
    public enum DNSResponseCodes : int
    {
        NoError         = 0,
        FormatError     = 1,
        ServerFailure   = 2,
        NameError       = 3,
        NotImplemented  = 4,
        Refused         = 5,
        Reserved        = 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15
    }

}
