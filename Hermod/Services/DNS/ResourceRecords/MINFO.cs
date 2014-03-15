using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// MINFO - DNS Resource Record
    /// </summary>
    public class MINFO : ADNSResourceRecord
    {

        #region Properties

        #region Text

        private readonly String _Text;

        public String Text
        {
            get 
            {
                return _Text;
            }
        }

        #endregion

        #endregion

        #region Constructor

        public MINFO(String           Name,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     String           RText)

            : base(Name, DNSResourceRecordTypes.MINFO, Class, TimeToLive, RText)

        {
            this._Text = RText;
        }

        #endregion

    }

}
