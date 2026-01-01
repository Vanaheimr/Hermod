/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

#region Usings

using System.Reflection;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{


    public class DNSInfo<T>(DNSServerConfig                  Origin,
                            Int32                            QueryId,
                            Boolean                          IsAuthoritativeAnswer,
                            Boolean                          IsTruncated,
                            Boolean                          RecursionDesired,
                            Boolean                          RecursionAvailable,
                            DNSResponseCodes                 ResponseCode,
                            IEnumerable<IDNSResourceRecord>  Answers,
                            IEnumerable<IDNSResourceRecord>  Authorities,
                            IEnumerable<IDNSResourceRecord>  AdditionalRecords,

                            Boolean                          IsValid,
                            Boolean                          IsTimeout,
                            TimeSpan                         Timeout)

        : DNSInfo(Origin,
                  QueryId,
                  IsAuthoritativeAnswer,
                  IsTruncated,
                  RecursionDesired,
                  RecursionAvailable,
                  ResponseCode,
                  Answers,
                  Authorities,
                  AdditionalRecords,
                  IsValid,
                  IsTimeout,
                  Timeout)

        where T : ADNSResourceRecord

    {

        public IEnumerable<T>  FilteredAnswers
            => Answers.OfType<T>();


        public DNSInfo(DNSInfo Legacy)

            : this(Legacy.Origin,
                   Legacy.QueryId,
                   Legacy.AuthoritativeAnswer,
                   Legacy.IsTruncated,
                   Legacy.RecursionRequested,
                   Legacy.RecursionAvailable,
                   Legacy.ResponseCode,
                   Legacy.Answers,
                   Legacy.Authorities,
                   Legacy.AdditionalRecords,
                   Legacy.IsValid,
                   Legacy.IsTimeout,
                   Legacy.Timeout)

        { }

    }


    public class DNSInfo
    {

        private static readonly ConcurrentDictionary<DNSResourceRecordTypes, ConstructorInfo>  rrLookup_DomainName       = [];
        private static readonly ConcurrentDictionary<DNSResourceRecordTypes, ConstructorInfo>  rrLookup_DNSServiceName   = [];



        private readonly List<IDNSResourceRecord> answers;
        private readonly List<IDNSResourceRecord> authorities;
        private readonly List<IDNSResourceRecord> additionalRecords;

        #region Properties

        /// <summary>
        /// The source of the DNS information.
        /// </summary>
        public DNSServerConfig                  Origin                { get; }

        /// <summary>
        /// The identification of the DNS query.
        /// </summary>
        public Int32                            QueryId               { get; }

        public Boolean                          AuthoritativeAnswer     { get; }

        public Boolean                          IsTruncated           { get; }

        public Boolean                          RecursionRequested    { get; }

        public Boolean                          RecursionAvailable    { get; }

        public DNSResponseCodes                 ResponseCode          { get; }


        public IEnumerable<IDNSResourceRecord>  Answers
            => answers.          AsReadOnly();

        public IEnumerable<IDNSResourceRecord>  Authorities
            => authorities.      AsReadOnly();

        public IEnumerable<IDNSResourceRecord>  AdditionalRecords
            => additionalRecords.AsReadOnly();


        public Boolean                          IsValid               { get; }

        public Boolean                          IsTimeout             { get; }

        public TimeSpan                         Timeout               { get; }

        #endregion

        #region Constructor(s)

        static DNSInfo()
        {

            #region Reflect ResourceRecordTypes

            foreach (var actualType in typeof(ADNSResourceRecord).
                                           Assembly.GetTypes().
                                           Where(type => type.IsClass &&
                                                !type.IsAbstract &&
                                                 type.IsSubclassOf(typeof(ADNSResourceRecord))))
            {

                var constructor_DomainName      = actualType.GetConstructor([ typeof(DomainName),     typeof(Stream) ]);
                var constructor_DNSServiceName  = actualType.GetConstructor([ typeof(DNSServiceName), typeof(Stream) ]);

                var typeIdField                 = actualType.GetField("TypeId") ?? throw new ArgumentException($"Constant field 'TypeId' of type '{actualType.Name}' was not found!");
                var actualTypeId                = typeIdField.GetValue(actualType);

                if (actualTypeId is DNSResourceRecordTypes id)
                {

                    if (constructor_DomainName is not null)
                        rrLookup_DomainName.    TryAdd(id, constructor_DomainName);

                    if (constructor_DNSServiceName is not null)
                        rrLookup_DNSServiceName.TryAdd(id, constructor_DNSServiceName);

                }

                else
                    throw new ArgumentException($"Constant field 'TypeId' of type '{actualType.Name}' was null!");

            }

            #endregion

        }


        public DNSInfo(DNSServerConfig                  Origin,
                       Int32                            QueryId,
                       Boolean                          IsAuthoritativeAnswer,
                       Boolean                          IsTruncated,
                       Boolean                          RecursionDesired,
                       Boolean                          RecursionAvailable,
                       DNSResponseCodes                 ResponseCode,
                       IEnumerable<IDNSResourceRecord>  Answers,
                       IEnumerable<IDNSResourceRecord>  Authorities,
                       IEnumerable<IDNSResourceRecord>  AdditionalRecords,

                       Boolean                          IsValid,
                       Boolean                          IsTimeout,
                       TimeSpan                         Timeout)
        {

            this.Origin               = Origin;
            this.QueryId              = QueryId;
            this.AuthoritativeAnswer  = IsAuthoritativeAnswer;
            this.IsTruncated          = IsTruncated;
            this.RecursionRequested   = RecursionDesired;
            this.RecursionAvailable   = RecursionAvailable;
            this.ResponseCode         = ResponseCode;

            this.answers              = [.. Answers];
            this.authorities          = [.. Authorities];
            this.additionalRecords    = [.. AdditionalRecords];

            this.IsValid              = IsValid;
            this.IsTimeout            = IsTimeout;
            this.Timeout              = Timeout;

        }

        #endregion




        #region (internal static) ReadResponse(Origin, ExpectedTransactionId, DNSResponseStream)

        internal static DNSInfo ReadResponse(DNSServerConfig  Origin,
                                             Int32            ExpectedTransactionId,
                                             Stream           DNSResponseStream)
        {

            #region DNS Header

            var requestId       = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) + (DNSResponseStream.ReadByte() & Byte.MaxValue);

            if (ExpectedTransactionId != requestId)
                //throw new Exception("Security Alert: Mallory might send us faked DNS replies! [" + ExpectedTransactionId + " != " + requestId + "]");
                return DNSInfo.Invalid(
                           Origin,
                           requestId
                       );

            var Byte2           = DNSResponseStream.ReadByte();
            var IS              = (Byte2 & 128) == 128;
            var OpCode          = (Byte2 >> 3 & 15);
            var AA              = (Byte2 & 4) == 4;
            var TC              = (Byte2 & 2) == 2;
            var RD              = (Byte2 & 1) == 1;

            var Byte3           = DNSResponseStream.ReadByte();
            var RA              = (Byte3 & 128) == 128;
            var Z               = (Byte3 & 1);    //reserved, not used
            var ResponseCode    = (DNSResponseCodes) (Byte3 & 15);

            var QuestionCount   = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AnswerCount     = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AuthorityCount  = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AdditionalCount = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);

            #endregion

            //ToDo: Does this make sense?
            #region Process Questions

            DNSResponseStream.Seek(12, SeekOrigin.Begin);

            for (var i = 0; i < QuestionCount; ++i) {
                var questionName  = DNSTools.ExtractName(DNSResponseStream);
                var typeId        = (UInt16)          ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8 | DNSResponseStream.ReadByte() & Byte.MaxValue);
                var classId       = (DNSQueryClasses) ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8 | DNSResponseStream.ReadByte() & Byte.MaxValue);
            }

            #endregion

            var answers            = new List<ADNSResourceRecord>();
            var authorities        = new List<ADNSResourceRecord>();
            var additionalRecords  = new List<ADNSResourceRecord>();

            for (var i = 0; i < AnswerCount; ++i)
            {
                var rr = ReadResourceRecord(DNSResponseStream);
                if (rr is not null)
                    answers.Add(rr);
            }

            for (var i = 0; i < AuthorityCount; ++i)
            {
                var rr = ReadResourceRecord(DNSResponseStream);
                if (rr is not null)
                    authorities.Add(rr);
            }

            for (var i = 0; i < AdditionalCount; ++i)
            {
                var rr = ReadResourceRecord(DNSResponseStream);
                if (rr is not null)
                    additionalRecords.Add(rr);
            }

            return new DNSInfo(
                       Origin,
                       requestId,
                       AA,
                       TC,
                       RD,
                       RA,
                       ResponseCode,

                       answers,
                       authorities,
                       additionalRecords,

                       true,
                       false,
                       TimeSpan.Zero
                   );

        }

        #endregion

        #region (private  static) ReadResourceRecord(DNSStream)

        private static ADNSResourceRecord? ReadResourceRecord(Stream DNSStream)
        {

            var resourceName  = DNSTools.ExtractName(DNSStream);
            var typeId        = (DNSResourceRecordTypes) ((DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);

            if (resourceName == "")
                resourceName = ".";

            if (rrLookup_DNSServiceName. TryGetValue(typeId, out var constructor_DNSServiceName))
                return (ADNSResourceRecord) constructor_DNSServiceName.Invoke([
                                                DNSServiceName.Parse(resourceName),
                                                DNSStream
                                            ]);

            else if (rrLookup_DomainName.TryGetValue(typeId, out var constructor_DomainName))
                return (ADNSResourceRecord) constructor_DomainName.Invoke([
                                                DomainName.    Parse(resourceName),
                                                DNSStream
                                            ]);

            DebugX.LogT($"Unknown DNS resource record '{typeId}' for '{resourceName}' received!");

            return null;

        }

        #endregion




        internal void AddAnswers(IEnumerable<IDNSResourceRecord> ResourceRecords)
        {
            answers.AddRange(ResourceRecords);
        }


        public static DNSInfo TimedOut(DNSServerConfig  Origin,
                                       Int32            QueryId,
                                       TimeSpan         Timeout)

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


        public static DNSInfo Invalid(DNSServerConfig  Origin,
                                      Int32            QueryId)

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
