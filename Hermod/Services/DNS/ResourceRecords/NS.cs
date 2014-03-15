using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    /// <summary>
    /// NS - DNS Resource Record
    /// </summary>
    public class NS : ADNSResourceRecord
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

        public NS(String           Name,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  String           RText)

            : base(Name, DNSResourceRecordTypes.NS, Class, TimeToLive, RText)

        {
            this._Text = RText;
        }

        #endregion

    }

}
