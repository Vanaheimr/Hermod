/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

        /// <summary>
        /// The source of the DNS information.
        /// </summary>
        public IPSocket          Origin                { get; }

        public Int32             QueryId               { get; }

        public Boolean           AuthorativeAnswer     { get; }

        public Boolean           IsTruncated           { get; }

        public Boolean           RecursionRequested    { get; }

        public Boolean           RecursionAvailable    { get; }

        public DNSResponseCodes  ResponseCode          { get; }


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

            this.Origin               = Origin;
            this.QueryId              = QueryId;
            this.AuthorativeAnswer    = IsAuthorativeAnswer;
            this.IsTruncated          = IsTruncated;
            this.RecursionRequested   = RecursionDesired;
            this.RecursionAvailable   = RecursionAvailable;
            this.ResponseCode         = ResponseCode;

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

            var Now       = DateTime.UtcNow;
            var ToDelete  = new List<ADNSResourceRecord>();

            _Answers.           RemoveAll(RR => RR.EndOfLife > Now);
            _Authorities.       RemoveAll(RR => RR.EndOfLife > Now);
            _AdditionalRecords. RemoveAll(RR => RR.EndOfLife > Now);

        }

    }

}
