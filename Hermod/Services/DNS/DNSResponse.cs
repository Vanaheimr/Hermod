/*
 * Copyright (c) 2010-2014, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Hermod <http://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System;
using System.Linq;
using System.Collections.Generic;

#endregion

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
        private IEnumerable<ADNSResourceRecord> _Answers;
        private IEnumerable<ADNSResourceRecord> _Authorities;
        private IEnumerable<ADNSResourceRecord> _AdditionalRecords;

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

        public IEnumerable<ADNSResourceRecord> Answers 
        {
            get { return _Answers; }
        }

        public IEnumerable<ADNSResourceRecord> Authorities
        {
            get { return _Authorities; }
        }

        public IEnumerable<ADNSResourceRecord> AdditionalRecords
        {
            get { return _AdditionalRecords; }
        }


        public DNSResponse(Int32                     ID,
                           Boolean                   AA,
                           Boolean                   TC,
                           Boolean                   RD,
                           Boolean                   RA,
                           DNSResponseCodes          RC,
                           List<ADNSResourceRecord>  Answers,
                           List<ADNSResourceRecord>  Authorities,
                           List<ADNSResourceRecord>  AdditionalRecords)

        {

            this._QueryID               = ID;
            this._AuthorativeAnswer     = AA;
            this._IsTruncated           = TC;
            this._RecursionDesired      = RD;
            this._RecursionAvailable    = RA;
            this._ResponseCode          = RC;
            this._Answers               = Answers;
            this._Authorities           = Authorities;
            this._AdditionalRecords     = AdditionalRecords;

        }


    }
}
