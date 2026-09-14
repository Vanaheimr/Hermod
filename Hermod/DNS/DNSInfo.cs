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
                            TimeSpan                         Timeout,

                            TimeSpan                         Runtime,

                            Boolean                          AuthenticData      = false,
                            Boolean                          CheckingDisabled   = false)

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
                  Timeout,
                  Runtime,
                  AuthenticData,
                  CheckingDisabled)

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
                   Legacy.Timeout,
                   Legacy.Runtime,
                   Legacy.AuthenticData,
                   Legacy.CheckingDisabled)

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

        /// <summary>
        /// Whether a security-aware resolver validated this answer
        /// (RFC 4035 §3.2.3, "Authentic Data").
        /// </summary>
        /// <remarks>
        /// The verdict of somebody else's validator, and worth exactly as much as
        /// the channel it arrived over: RFC 6840 §5.7 says a stub may trust the
        /// bit only when it trusts the path to the resolver, which is what a DoT
        /// or DoH client is for. It is reported here rather than acted on — what
        /// to make of it is the caller's decision, and Hermod carries a validator
        /// of its own for callers who would rather not decide.
        /// </remarks>
        public Boolean                          AuthenticData         { get; }

        /// <summary>
        /// Whether the sender asked that no validation be done on its behalf
        /// (RFC 4035 §3.2.2, "Checking Disabled").
        /// </summary>
        public Boolean                          CheckingDisabled      { get; }

        public DNSResponseCodes                 ResponseCode          { get; }


        public IEnumerable<IDNSResourceRecord>  Answers
            => answers.          AsReadOnly();

        public IEnumerable<IDNSResourceRecord>  Authorities
            => authorities.      AsReadOnly();

        public IEnumerable<IDNSResourceRecord>  AdditionalRecords
            => additionalRecords.AsReadOnly();


        /// <summary>
        /// The OPT pseudo-record from the additional section (if present).
        /// </summary>
        public OPT?                             OPTRecord
            => additionalRecords.OfType<OPT>().FirstOrDefault();

        /// <summary>
        /// All EDNS options from the response OPT record (empty if no OPT record).
        /// </summary>
        public IEnumerable<EDNSOption>          EDNSOptions
            => OPTRecord?.Options ?? [];


        public Boolean                          IsValid               { get; }

        public Boolean                          IsTimeout             { get; }

        public TimeSpan                         Timeout               { get; }

        /// <summary>
        /// The runtime of the DNS query.
        /// </summary>
        public TimeSpan                         Runtime               { get; }


        /// <summary>
        /// The DNSSEC validation status of this DNS response.
        /// Set after calling DNSSECValidator.ValidateAsync().
        /// </summary>
        public DNSSECValidationResult?          DNSSECStatus          { get; set; }

        #endregion

        #region Constructor(s)

        static DNSInfo()
        {

            #region Reflect ResourceRecordTypes

            foreach (var actualType in typeof(ADNSResourceRecord).
                                           Assembly.GetTypes().
                                           Where(type => type.IsClass &&
                                                !type.IsAbstract &&
                                                 type.IsSubclassOf(typeof(ADNSResourceRecord)) &&
                                                 // UnknownRecord is deliberately outside the index. The index
                                                 // maps one type code to one class, and this class answers for
                                                 // every code that has none — it carries its type as a
                                                 // constructor argument rather than a TypeId constant, and
                                                 // ReadResourceRecord reaches it after the lookup fails, not
                                                 // through it.
                                                 type != typeof(UnknownRecord)))
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
                       TimeSpan                         Timeout,

                       TimeSpan                         Runtime,

                       Boolean                          AuthenticData      = false,
                       Boolean                          CheckingDisabled   = false)
        {

            this.Origin               = Origin;
            this.QueryId              = QueryId;
            this.AuthoritativeAnswer  = IsAuthoritativeAnswer;
            this.IsTruncated          = IsTruncated;
            this.RecursionRequested   = RecursionDesired;
            this.RecursionAvailable   = RecursionAvailable;
            this.AuthenticData        = AuthenticData;
            this.CheckingDisabled     = CheckingDisabled;
            this.ResponseCode         = ResponseCode;

            this.answers              = [.. Answers];
            this.authorities          = [.. Authorities];
            this.additionalRecords    = [.. AdditionalRecords];

            this.IsValid              = IsValid;
            this.IsTimeout            = IsTimeout;
            this.Timeout              = Timeout;

            this.Runtime              = Runtime;

        }

        #endregion



        #region (internal static) ReadResponse(Origin, ExpectedTransactionId, DNSResponseStream)

        /// <param name="ExpectedQuestions">
        /// The question section of the outstanding query. RFC 5452 §9.1 makes
        /// matching it a MUST, alongside the transaction id.
        /// </param>
        internal static DNSInfo ReadResponse(DNSServerConfig           Origin,
                                             Int32                     ExpectedTransactionId,
                                             IEnumerable<DNSQuestion>  ExpectedQuestions,
                                             Stream                    DNSResponseStream,
                                             TimeSpan                  Timeout,
                                             TimeSpan                  Runtime)
        {

            #region DNS Header

            var requestId        = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) + (DNSResponseStream.ReadByte() & Byte.MaxValue);

            if (ExpectedTransactionId != requestId)
                //throw new Exception("Security Alert: Mallory might send us faked DNS replies! [" + ExpectedTransactionId + " != " + requestId + "]");
                return DNSInfo.Invalid(
                           Origin,
                           requestId
                       );

            var Byte2            = DNSResponseStream.ReadByte();
            var IS               = (Byte2 & 128) == 128;
            var OpCode           = (Byte2 >> 3 & 15);
            var AA               = (Byte2 & 4) == 4;
            var TC               = (Byte2 & 2) == 2;
            var RD               = (Byte2 & 1) == 1;

            var Byte3            = DNSResponseStream.ReadByte();
            var RA               = (Byte3 & 0x80) != 0;
            // 0x40 is Z. RFC 1035 §4.1.1 reserves it and RFC 6895 §2 keeps it
            // reserved, so it is read past rather than acted on — but it has to be
            // the right bit. The older line took 0x01 for Z, which is the low bit
            // of the RCODE, and the comment beside it said "reserved, not used".
            var AD               = (Byte3 & 0x20) != 0;   // RFC 4035 §3.2.3
            var CD               = (Byte3 & 0x10) != 0;   // RFC 4035 §3.2.2
            var ResponseCode     = (DNSResponseCodes) (Byte3 & 0x0F);

            var QuestionCount    = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AnswerCount      = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AuthorityCount   = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);
            var AdditionalCount  = ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8) | (DNSResponseStream.ReadByte() & Byte.MaxValue);

            #endregion

            #region The question section, which RFC 5452 §9.1 says MUST match

            // "A resolver implementation MUST match responses to all of the
            // following attributes of the query: ... Query ID, Query name, Query
            // class and type ... A mismatch and the response MUST be considered
            // invalid."
            //
            // §3 puts it the other way round: data is accepted "if and only if"
            // the question section of the reply is equivalent to that of a
            // question waiting for an answer. A transaction id alone leaves
            // sixteen bits doing the work of a check the RFC writes out in three
            // lines — and this section used to read the questions and throw them
            // away, under a comment asking whether that made sense.
            DNSResponseStream.Seek(12, SeekOrigin.Begin);

            var expected = ExpectedQuestions.ToArray();

            if (QuestionCount != expected.Length)
                return Invalid(Origin, requestId);

            for (var i = 0; i < QuestionCount; ++i)
            {

                var questionName  = DNSTools.ExtractName(DNSResponseStream);
                var typeId        = (DNSResourceRecordTypes) ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8 | DNSResponseStream.ReadByte() & Byte.MaxValue);
                var classId       = (DNSQueryClasses)        ((DNSResponseStream.ReadByte() & Byte.MaxValue) << 8 | DNSResponseStream.ReadByte() & Byte.MaxValue);

                // Equivalence of names is RFC 4343's, which makes them
                // case-insensitive: a resolver that folds the QNAME before
                // answering has returned the same name, and most of them do.
                // Comparing octets here would refuse the deployed world.
                if (!expected.Any(question => question.QueryType  == typeId  &&
                                              question.QueryClass == classId &&
                                              String.Equals(question.DomainName.FullName.TrimEnd('.'),
                                                            questionName.       TrimEnd('.'),
                                                            StringComparison.OrdinalIgnoreCase)))
                {
                    return Invalid(Origin, requestId);
                }

            }

            #endregion

            var answers            = new List<IDNSResourceRecord>();
            var authorities        = new List<IDNSResourceRecord>();
            var additionalRecords  = new List<IDNSResourceRecord>();

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

            // RFC 6891 §6.1.3: Full RCODE = (ExtendedRCODE << 4) | Header-RCODE
            // The OPT record (if present) carries the upper 8 bits of the RCODE.
            var optRecord     = additionalRecords.OfType<OPT>().FirstOrDefault();
            var fullRCODE     = optRecord is not null
                                    ? (DNSResponseCodes) ((optRecord.ExtendedRCODE << 4) | (Int32) ResponseCode)
                                    : ResponseCode;

            return new DNSInfo(

                       Origin,
                       requestId,
                       AA,
                       TC,
                       RD,
                       RA,
                       fullRCODE,

                       answers,
                       authorities,
                       additionalRecords,

                       true,
                       false,
                       Timeout,

                       Runtime,

                       AD,
                       CD

                   );

        }

        #endregion

        #region (public   static) ReadResourceRecord(DNSStream)

        /// <summary>
        /// Read one resource record — owner name, type, class, TTL, RDATA — from
        /// a DNS stream, using the reflection registry of record types.
        /// </summary>
        /// <param name="DNSStream">A stream positioned at the start of a resource record.</param>
        /// <returns>
        /// The record — as an <see cref="UnknownRecord"/> holding opaque RDATA when
        /// no type in this build claims the type code (RFC 3597 §2).
        /// </returns>
        public static IDNSResourceRecord? ReadResourceRecord(Stream DNSStream)
        {

            var resourceName  = DNSTools.ExtractName(DNSStream);
            var typeId        = (DNSResourceRecordTypes) ((DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);

            if (resourceName == "")
                resourceName = ".";

            // OPT is a pseudo-RR that does NOT extend ADNSResourceRecord,
            // so it must be handled explicitly before the reflection lookup.
            if (typeId == DNSResourceRecordTypes.OPT)
                return new OPT(
                           DNSServiceName.Parse(resourceName),
                           DNSStream
                       );

            if (rrLookup_DNSServiceName. TryGetValue(typeId, out var constructor_DNSServiceName))
                return (ADNSResourceRecord) constructor_DNSServiceName.Invoke([
                                                DNSServiceName.Parse(resourceName),
                                                DNSStream
                                            ]);

            else if (rrLookup_DomainName.TryGetValue(typeId, out var constructor_DomainName))
                // ParseLenient: RR owner names read from the wire may be underscore names
                // (e.g. "_dmarc.example.com", "selector._domainkey.example.com").
                return (ADNSResourceRecord) constructor_DomainName.Invoke([
                                                DomainName.    ParseLenient(resourceName),
                                                DNSStream
                                            ]);

            // RFC 3597 §2: no parser for this type is not a reason to lose the
            // record — and, more urgently, not a reason to leave the stream where
            // it is. Everything after the TYPE field is still readable by shape
            // alone, so the record is taken as opaque data and the reader ends up
            // exactly where the next record starts.
            //
            // Returning early here instead cost the rest of the message: the
            // caller's next call began reading an owner name out of this record's
            // CLASS field, and what it made of the remaining bytes was anyone's
            // guess.
            return new UnknownRecord(
                       DNSServiceName.Parse(resourceName),
                       typeId,
                       DNSStream
                   );

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
                    Timeout,
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
                    TimeSpan.Zero,
                    TimeSpan.Zero);


        /// <summary>
        /// Create a DNSInfo representing a query that failed due to a
        /// network error, malformed response or other non-timeout exception.
        /// Unlike TimedOut, this sets IsTimeout = false.
        /// </summary>
        public static DNSInfo Failed(DNSServerConfig  Origin,
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
                    false,
                    Timeout,
                    Timeout);


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
