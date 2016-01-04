/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public class DNSInfo
    {

        #region Properties

        #region QueryId

        private readonly Int32 _QueryId;

        public Int32 QueryId
        {
            get
            {
                return _QueryId;
            }
        }

        #endregion

        #region AuthorativeAnswer

        private readonly Boolean _AuthorativeAnswer;

        public Boolean AuthorativeAnswer
        {
            get
            {
                return _AuthorativeAnswer;
            }
        }

        #endregion

        #region IsTruncated

        private readonly Boolean _IsTruncated;

        public Boolean IsTruncated
        {
            get
            {
                return _IsTruncated;
            }
        }

        #endregion

        #region RecursionRequested

        private readonly Boolean _RecursionDesired;

        public Boolean RecursionRequested
        {
            get
            {
                return _RecursionDesired;
            }
        }

        #endregion

        #region RecursionAvailable

        private readonly Boolean _RecursionAvailable;

        public Boolean RecursionAvailable
        {
            get
            {
                return _RecursionAvailable;
            }
        }

        #endregion

        #region ResponseCode

        private readonly DNSResponseCodes _ResponseCode;

        public DNSResponseCodes ResponseCode
        {
            get
            {
                return _ResponseCode;
            }
        }

        #endregion


        #region Answers

        private readonly List<ADNSResourceRecord> _Answers;

        public IEnumerable<ADNSResourceRecord> Answers
        {
            get
            {
                return _Answers;
            }
        }

        #endregion

        #region Authorities

        private readonly List<ADNSResourceRecord> _Authorities;

        public IEnumerable<ADNSResourceRecord> Authorities
        {
            get
            {
                return _Authorities;
            }
        }

        #endregion

        #region AdditionalRecords

        private List<ADNSResourceRecord> _AdditionalRecords;

        public IEnumerable<ADNSResourceRecord> AdditionalRecords
        {
            get
            {
                return _AdditionalRecords;
            }
        }

        #endregion


        #region Origin

        private readonly IPSocket _Origin;

        /// <summary>
        /// The source of the DNS information.
        /// </summary>
        public IPSocket Origin
        {
            get
            {
                return _Origin;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        public DNSInfo(IPSocket                         Origin,
                       Int32                            QueryId,
                       Boolean                          IsAuthorativeAnswer,
                       Boolean                          IsTruncated,
                       Boolean                          RecursionDesired,
                       Boolean                          RecursionAvailable,
                       DNSResponseCodes                 ResponseCode,
                       IEnumerable<ADNSResourceRecord>  Answers,
                       IEnumerable<ADNSResourceRecord>  Authorities,
                       IEnumerable<ADNSResourceRecord>  AdditionalRecords)
        {

            this._Origin              = Origin;
            this._QueryId             = QueryId;
            this._AuthorativeAnswer   = IsAuthorativeAnswer;
            this._IsTruncated         = IsTruncated;
            this._RecursionDesired    = RecursionDesired;
            this._RecursionAvailable  = RecursionAvailable;
            this._ResponseCode        = ResponseCode;

            this._Answers             = new List<ADNSResourceRecord>(Answers);
            this._Authorities         = new List<ADNSResourceRecord>(Authorities);
            this._AdditionalRecords   = new List<ADNSResourceRecord>(AdditionalRecords);

        }

        #endregion


        internal void AddAnswer(ADNSResourceRecord ResourceRecord)
        {
            this._Answers.Add(ResourceRecord);
        }

        internal void CleanUp()
        {

            var Now       = DateTime.Now;
            var ToDelete  = new List<ADNSResourceRecord>();

            _Answers.           RemoveAll(RR => RR.EndOfLife > Now);
            _Authorities.       RemoveAll(RR => RR.EndOfLife > Now);
            _AdditionalRecords. RemoveAll(RR => RR.EndOfLife > Now);

        }

    }

}
