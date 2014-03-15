using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eu.Vanaheimr.Hermod.Services.DNS
{

    public class DNSResponse
    {

        private int _QueryID;

        //Property Internals
        private bool _AuthorativeAnswer;
        private bool _IsTruncated;
        private bool _RecursionDesired;
        private bool _RecursionAvailable;
        private DNSResponseCodes _ResponseCode;

        private List<ADNSResourceRecord> _ResourceRecords;
        private List<ADNSResourceRecord> _Answers;
        private List<ADNSResourceRecord> _Authorities;
        private List<ADNSResourceRecord> _AdditionalRecords;

        //Read Only Public Properties
        public int QueryID
        {
            get { return _QueryID; }
        }

        public bool AuthorativeAnswer
        {
            get { return _AuthorativeAnswer; }
        }

        public bool IsTruncated
        {
            get { return _IsTruncated; }
        }

        public bool RecursionRequested
        {
            get { return _RecursionDesired; }
        }

        public bool RecursionAvailable
        {
            get { return _RecursionAvailable; }
        }

        public DNSResponseCodes ResponseCode
        {
            get { return _ResponseCode; } 
        }

        public List<ADNSResourceRecord> Answers 
        {
            get { return _Answers; }
        }

        public List<ADNSResourceRecord> Authorities
        {
            get { return _Authorities; }
        }

        public List<ADNSResourceRecord> AdditionalRecords
        {
            get { return _AdditionalRecords; }
        }


        public List<ADNSResourceRecord> ResourceRecords
        {
            get 
            {
                if (_ResourceRecords.Count == 0 && _Answers.Count > 0 && _Authorities.Count > 0 && _AdditionalRecords.Count > 0)
                {

                    foreach (var rr in Answers)
                        this._ResourceRecords.Add(rr);

                    foreach (var rr in Authorities)
                        this._ResourceRecords.Add(rr);

                    foreach (var rr in AdditionalRecords)
                        this._ResourceRecords.Add(rr);

                }

                return _ResourceRecords; 
            } 
        }

        public DNSResponse(int ID, bool AA, bool TC, bool RD, bool RA, int RC) 
        {
            this._QueryID = ID; 
            this._AuthorativeAnswer = AA; 
            this._IsTruncated = TC; 
            this._RecursionDesired = RD; 
            this._RecursionAvailable = RA; 
            this._ResponseCode = (DNSResponseCodes) RC; 

            this._ResourceRecords   = new List<ADNSResourceRecord>(); 
            this._Answers           = new List<ADNSResourceRecord>(); 
            this._Authorities       = new List<ADNSResourceRecord>(); 
            this._AdditionalRecords = new List<ADNSResourceRecord>(); 
        }


    }
}
