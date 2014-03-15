using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// Query Class or Scope
    /// </summary>
    public enum DNSQueryClasses : int
    {
        IN              = 1,
        CS              = 2,
        CH              = 3,
        HS              = 4,
        ANY             = 255
    }

}
