using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// PTR - DNS Resource Record
    /// </summary>
    public class PTR : ADNSResourceRecord
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

        public PTR(String           Name,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           RText)

            : base(Name, DNSResourceRecordTypes.PTR, Class, TimeToLive, RText)

        {
            this._Text = RText;
        }

        #endregion

    }

}
