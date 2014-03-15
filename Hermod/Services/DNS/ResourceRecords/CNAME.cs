using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// CNAME - DNS Resource Record
    /// </summary>
    public class CNAME : ADNSResourceRecord
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

        public CNAME(String           Name,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     String           RText)

            : base(Name, DNSResourceRecordTypes.CNAME, Class, TimeToLive, RText)

        {
            this._Text = RText;
        }

        #endregion

    }

}
