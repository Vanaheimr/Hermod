/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    public class DNSInfo
    {

        private readonly List<ADNSResourceRecord> answers;
        private readonly List<ADNSResourceRecord> authorities;
        private readonly List<ADNSResourceRecord> additionalRecords;

        #region Properties

        /// <summary>
        /// The source of the DNS information.
        /// </summary>
        public IPSocket                         Origin                { get; }

        /// <summary>
        /// The identification of the DNS query.
        /// </summary>
        public Int32                            QueryId               { get; }

        public Boolean                          AuthoritativeAnswer     { get; }

        public Boolean                          IsTruncated           { get; }

        public Boolean                          RecursionRequested    { get; }

        public Boolean                          RecursionAvailable    { get; }

        public DNSResponseCodes                 ResponseCode          { get; }


        public IEnumerable<ADNSResourceRecord>  Answers
            => answers.AsReadOnly();

        public IEnumerable<ADNSResourceRecord>  Authorities
            => authorities.AsReadOnly();

        public IEnumerable<ADNSResourceRecord>  AdditionalRecords
            => additionalRecords.AsReadOnly();


        public Boolean                          IsValid               { get; }

        public Boolean                          IsTimeout             { get; }

        public TimeSpan                         Timeout               { get; }

        #endregion

        #region Constructor(s)

        public DNSInfo(IPSocket                         Origin,
                       Int32                            QueryId,
                       Boolean                          IsAuthoritativeAnswer,
                       Boolean                          IsTruncated,
                       Boolean                          RecursionDesired,
                       Boolean                          RecursionAvailable,
                       DNSResponseCodes                 ResponseCode,
                       IEnumerable<ADNSResourceRecord>  Answers,
                       IEnumerable<ADNSResourceRecord>  Authorities,
                       IEnumerable<ADNSResourceRecord>  AdditionalRecords,

                       Boolean                          IsValid,
                       Boolean                          IsTimeout,
                       TimeSpan                         Timeout)
        {

            this.Origin              = Origin;
            this.QueryId             = QueryId;
            this.AuthoritativeAnswer   = IsAuthoritativeAnswer;
            this.IsTruncated         = IsTruncated;
            this.RecursionRequested  = RecursionDesired;
            this.RecursionAvailable  = RecursionAvailable;
            this.ResponseCode        = ResponseCode;

            this.answers             = [.. Answers];
            this.authorities         = [.. Authorities];
            this.additionalRecords   = [.. AdditionalRecords];

            this.IsValid             = IsValid;
            this.IsTimeout           = IsTimeout;
            this.Timeout             = Timeout;

        }

        #endregion



        internal void AddAnswers(IEnumerable<ADNSResourceRecord> ResourceRecords)
        {
            answers.AddRange(ResourceRecords);
        }


        public static DNSInfo TimedOut(IPSocket  Origin,
                                       Int32     QueryId,
                                       TimeSpan  Timeout)

            => new (Origin,
                    QueryId,
                    false,
                    false,
                    false,
                    false,
                    DNSResponseCodes.ServerFailure,
                    [],
                    [],
                    [],
                    false,
                    true,
                    Timeout);


        public static DNSInfo Invalid(IPSocket  Origin,
                                      Int32     QueryId)

            => new (Origin,
                    QueryId,
                    false,
                    false,
                    false,
                    false,
                    DNSResponseCodes.ServerFailure,
                    [],
                    [],
                    [],
                    false,
                    false,
                    TimeSpan.Zero);


        //internal void AddAnswer(ADNSResourceRecord ResourceRecord)
        //{
        //    this._Answers.Add(ResourceRecord);
        //}

        //internal void CleanUp()
        //{

        //    var Now       = Timestamp.Now;
        //    var ToDelete  = new List<ADNSResourceRecord>();

        //    _Answers.           RemoveAll(RR => RR.EndOfLife > Now);
        //    _Authorities.       RemoveAll(RR => RR.EndOfLife > Now);
        //    _AdditionalRecords. RemoveAll(RR => RR.EndOfLife > Now);

        //}

    }

}
