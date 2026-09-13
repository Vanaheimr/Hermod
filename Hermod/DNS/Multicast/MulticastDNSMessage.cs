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

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    #region MulticastDNSQuestion

    /// <summary>
    /// A question of a Multicast DNS message: a DNS question plus the
    /// "unicast-response" (QU) bit (RFC 6762 §5.4).
    /// </summary>
    /// <param name="Question">The DNS question (with the QU bit removed from its class).</param>
    /// <param name="UnicastResponseRequested">Whether the querier asked for a unicast response (the QU bit).</param>
    public sealed record MulticastDNSQuestion(DNSQuestion  Question,
                                              Boolean      UnicastResponseRequested   = false)
    {

        /// <summary>
        /// The queried name.
        /// </summary>
        public DNSServiceName          Name
            => Question.DomainName;

        /// <summary>
        /// The queried resource record type.
        /// </summary>
        public DNSResourceRecordTypes  Type
            => Question.QueryType;

        /// <summary>
        /// The queried class (without the QU bit).
        /// </summary>
        public DNSQueryClasses         Class
            => Question.QueryClass;


        /// <summary>
        /// Create a question for the given name, type and class.
        /// </summary>
        /// <param name="Name">The name to query.</param>
        /// <param name="Type">The resource record type to query (default: ANY).</param>
        /// <param name="UnicastResponseRequested">Whether to ask for a unicast response.</param>
        /// <param name="Class">The class to query (default: IN).</param>
        public MulticastDNSQuestion(DNSServiceName          Name,
                                    DNSResourceRecordTypes  Type                       = DNSResourceRecordTypes.Any,
                                    Boolean                 UnicastResponseRequested   = false,
                                    DNSQueryClasses         Class                      = DNSQueryClasses.IN)

            : this(new DNSQuestion(Name, Type, Class),
                   UnicastResponseRequested)

        { }


        /// <summary>
        /// Whether the given resource record answers this question (RFC 6762 §6):
        /// same name (case-insensitive), matching type (or ANY) and matching class (or ANY).
        /// </summary>
        /// <param name="Record">A resource record.</param>
        public Boolean Matches(IDNSResourceRecord Record)

            => Name.Equals(Record.DomainName) &&
               (Type  == DNSResourceRecordTypes.Any || Record.Type  == Type) &&
               (Class == DNSQueryClasses.ANY        || Record.Class == Class);


        /// <summary>
        /// Return a text representation of this question.
        /// </summary>
        public override String ToString()

            => $"{Name} {Class} {Type}{(UnicastResponseRequested ? " (QU)" : "")}";

    }

    #endregion

    #region MulticastDNSRecord

    /// <summary>
    /// A resource record within a Multicast DNS message: the record plus the
    /// "cache-flush" bit (RFC 6762 §10.2) and an optional time-to-live override,
    /// which is used for goodbye packets (TTL 0, §10.1) and legacy unicast responses (§6.7)
    /// without cloning the record.
    /// </summary>
    /// <param name="Record">The resource record (with the cache-flush bit removed from its class).</param>
    /// <param name="CacheFlush">Whether the record carries the cache-flush bit.</param>
    /// <param name="TimeToLiveOverride">An optional time-to-live to send instead of the record's own.</param>
    public sealed record MulticastDNSRecord(IDNSResourceRecord  Record,
                                            Boolean             CacheFlush           = false,
                                            TimeSpan?           TimeToLiveOverride   = null)
    {

        /// <summary>
        /// The owner name of the record.
        /// </summary>
        public DNSServiceName          Name
            => Record.DomainName;

        /// <summary>
        /// The type of the record.
        /// </summary>
        public DNSResourceRecordTypes  Type
            => Record.Type;

        /// <summary>
        /// The class of the record (without the cache-flush bit).
        /// </summary>
        public DNSQueryClasses         Class
            => Record.Class;

        /// <summary>
        /// The time-to-live that is (or was) on the wire.
        /// </summary>
        public TimeSpan                TimeToLive
            => TimeToLiveOverride ?? Record.TimeToLive;

        /// <summary>
        /// Whether this record announces its own removal (a "goodbye" record with a time-to-live of zero, RFC 6762 §10.1).
        /// </summary>
        public Boolean                 IsGoodbye
            => TimeToLive <= TimeSpan.Zero;


        /// <summary>
        /// Return a text representation of this record.
        /// </summary>
        public override String ToString()

            => $"{Record}{(CacheFlush ? " (cache-flush)" : "")}{(TimeToLiveOverride.HasValue ? $" (TTL {TimeToLiveOverride.Value.TotalSeconds}s)" : "")}";

    }

    #endregion


    /// <summary>
    /// A Multicast DNS message (RFC 6762 §18): a DNS message whose question class
    /// field may carry the unicast-response bit and whose record class field may
    /// carry the cache-flush bit. The parser is tolerant: a record or question that
    /// cannot be understood (an unknown type is fine, a name this library cannot
    /// represent is not) is skipped and reported in <see cref="Warnings"/> while
    /// the rest of the message is still delivered, because one strange record from
    /// one device on the link must not blind a browser to every other device.
    /// </summary>
    public sealed class MulticastDNSMessage
    {

        #region Data

        private const Int32 HeaderLength = 12;

        #endregion

        #region Properties

        /// <summary>
        /// The transaction identification (zero for multicast queries and responses, RFC 6762 §18.1).
        /// </summary>
        public UInt16                                TransactionId          { get; }

        /// <summary>
        /// Whether this message is a response (QR bit).
        /// </summary>
        public Boolean                               IsResponse             { get; }

        /// <summary>
        /// Whether this message is a query.
        /// </summary>
        public Boolean                               IsQuery
            => !IsResponse;

        /// <summary>
        /// The opcode (zero for standard queries, RFC 6762 §18.3).
        /// </summary>
        public Byte                                  Opcode                 { get; }

        /// <summary>
        /// Whether the answers are authoritative (AA bit, set in every multicast response, RFC 6762 §18.4).
        /// </summary>
        public Boolean                               AuthoritativeAnswer    { get; }

        /// <summary>
        /// Whether the message was truncated (TC bit): within a query it announces
        /// further known answers in a following packet (RFC 6762 §7.2).
        /// </summary>
        public Boolean                               Truncated              { get; }

        /// <summary>
        /// The recursion-desired bit (ignored by Multicast DNS, RFC 6762 §18.6).
        /// </summary>
        public Boolean                               RecursionDesired       { get; }

        /// <summary>
        /// The response code (zero in Multicast DNS, RFC 6762 §18.11).
        /// </summary>
        public DNSResponseCodes                      ResponseCode           { get; }

        /// <summary>
        /// The questions.
        /// </summary>
        public IReadOnlyList<MulticastDNSQuestion>   Questions              { get; }

        /// <summary>
        /// The answer records (within a query: the known answers of the querier, RFC 6762 §7.1).
        /// </summary>
        public IReadOnlyList<MulticastDNSRecord>     Answers                { get; }

        /// <summary>
        /// The authority records (within a probe query: the proposed records, RFC 6762 §8.2).
        /// </summary>
        public IReadOnlyList<MulticastDNSRecord>     Authorities            { get; }

        /// <summary>
        /// The additional records.
        /// </summary>
        public IReadOnlyList<MulticastDNSRecord>     Additionals            { get; }

        /// <summary>
        /// The questions and records the parser had to skip.
        /// </summary>
        public IReadOnlyList<String>                 Warnings               { get; }

        /// <summary>
        /// All records of all sections.
        /// </summary>
        public IEnumerable<MulticastDNSRecord>       AllRecords
            => Answers.Concat(Authorities).Concat(Additionals);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new Multicast DNS message.
        /// </summary>
        /// <param name="TransactionId">The transaction identification.</param>
        /// <param name="IsResponse">Whether this message is a response.</param>
        /// <param name="Opcode">The opcode.</param>
        /// <param name="AuthoritativeAnswer">Whether the answers are authoritative.</param>
        /// <param name="Truncated">Whether the message is truncated.</param>
        /// <param name="RecursionDesired">The recursion-desired bit.</param>
        /// <param name="ResponseCode">The response code.</param>
        /// <param name="Questions">The questions.</param>
        /// <param name="Answers">The answer records.</param>
        /// <param name="Authorities">The authority records.</param>
        /// <param name="Additionals">The additional records.</param>
        /// <param name="Warnings">The questions and records the parser had to skip.</param>
        public MulticastDNSMessage(UInt16                              TransactionId,
                                   Boolean                             IsResponse,
                                   Byte                                Opcode,
                                   Boolean                             AuthoritativeAnswer,
                                   Boolean                             Truncated,
                                   Boolean                             RecursionDesired,
                                   DNSResponseCodes                    ResponseCode,
                                   IEnumerable<MulticastDNSQuestion>?  Questions,
                                   IEnumerable<MulticastDNSRecord>?    Answers,
                                   IEnumerable<MulticastDNSRecord>?    Authorities,
                                   IEnumerable<MulticastDNSRecord>?    Additionals,
                                   IEnumerable<String>?                Warnings   = null)
        {

            this.TransactionId        = TransactionId;
            this.IsResponse           = IsResponse;
            this.Opcode               = Opcode;
            this.AuthoritativeAnswer  = AuthoritativeAnswer;
            this.Truncated            = Truncated;
            this.RecursionDesired     = RecursionDesired;
            this.ResponseCode         = ResponseCode;
            this.Questions            = [.. Questions   ?? []];
            this.Answers              = [.. Answers     ?? []];
            this.Authorities          = [.. Authorities ?? []];
            this.Additionals          = [.. Additionals ?? []];
            this.Warnings             = [.. Warnings    ?? []];

        }

        #endregion


        #region (static) Query   (Questions, KnownAnswers = null, Authorities = null, Truncated = false, TransactionId = 0)

        /// <summary>
        /// Create a Multicast DNS query (RFC 6762 §5): opcode zero, no flags but an optional
        /// truncation bit, the given questions, known answers and (for probes) authorities.
        /// </summary>
        /// <param name="Questions">The questions.</param>
        /// <param name="KnownAnswers">The known answers of the querier (RFC 6762 §7.1).</param>
        /// <param name="Authorities">The proposed records of a probe (RFC 6762 §8.2).</param>
        /// <param name="Truncated">Whether more known answers follow in another packet (RFC 6762 §7.2).</param>
        /// <param name="TransactionId">The transaction identification (zero for multicast queries).</param>
        public static MulticastDNSMessage Query(IEnumerable<MulticastDNSQuestion>  Questions,
                                                IEnumerable<MulticastDNSRecord>?   KnownAnswers    = null,
                                                IEnumerable<MulticastDNSRecord>?   Authorities     = null,
                                                Boolean                            Truncated       = false,
                                                UInt16                             TransactionId   = 0)

            => new (TransactionId,
                    false,
                    0,
                    false,
                    Truncated,
                    false,
                    DNSResponseCodes.NoError,
                    Questions,
                    KnownAnswers,
                    Authorities,
                    null);

        #endregion

        #region (static) Response(Answers, Additionals = null, Questions = null, Truncated = false, TransactionId = 0)

        /// <summary>
        /// Create a Multicast DNS response (RFC 6762 §6 and §18): authoritative, opcode zero,
        /// the given answers and additionals. Only a legacy unicast response repeats the
        /// question and the transaction identification of the query (RFC 6762 §6.7).
        /// </summary>
        /// <param name="Answers">The answer records.</param>
        /// <param name="Additionals">The additional records.</param>
        /// <param name="Questions">The questions to repeat (legacy unicast responses only).</param>
        /// <param name="Truncated">Whether the response is truncated.</param>
        /// <param name="TransactionId">The transaction identification (legacy unicast responses only).</param>
        public static MulticastDNSMessage Response(IEnumerable<MulticastDNSRecord>     Answers,
                                                   IEnumerable<MulticastDNSRecord>?    Additionals     = null,
                                                   IEnumerable<MulticastDNSQuestion>?  Questions       = null,
                                                   Boolean                             Truncated       = false,
                                                   UInt16                              TransactionId   = 0)

            => new (TransactionId,
                    true,
                    0,
                    true,
                    Truncated,
                    false,
                    DNSResponseCodes.NoError,
                    Questions,
                    Answers,
                    null,
                    Additionals);

        #endregion


        #region (static) TryParse(Packet, out Message, out ErrorResponse)

        /// <summary>
        /// Try to parse the given packet as Multicast DNS message. A packet whose header
        /// or section structure is broken fails; a question or record that cannot be
        /// represented is skipped and listed in <see cref="Warnings"/>.
        /// </summary>
        /// <param name="Packet">The packet.</param>
        /// <param name="Message">The parsed message.</param>
        /// <param name="ErrorResponse">An error message when the packet could not be parsed.</param>
        public static Boolean TryParse(ReadOnlyMemory<Byte>                           Packet,
                                       [NotNullWhen(true)]  out MulticastDNSMessage?  Message,
                                       [NotNullWhen(false)] out String?               ErrorResponse)
        {

            Message        = null;
            ErrorResponse  = null;

            if (Packet.Length < HeaderLength)
            {
                ErrorResponse = $"The packet is shorter than the DNS header ({Packet.Length} bytes)!";
                return false;
            }

            // The class bits are cleared within a private copy, so that the standard
            // resource record parsers see plain classes.
            var buffer            = Packet.ToArray();
            var span              = buffer.AsSpan();

            var transactionId     = BinaryPrimitives.ReadUInt16BigEndian(span[0..2]);
            var flags1            = buffer[2];
            var flags2            = buffer[3];
            var questionCount     = BinaryPrimitives.ReadUInt16BigEndian(span[4..6]);
            var answerCount       = BinaryPrimitives.ReadUInt16BigEndian(span[6..8]);
            var authorityCount    = BinaryPrimitives.ReadUInt16BigEndian(span[8..10]);
            var additionalCount   = BinaryPrimitives.ReadUInt16BigEndian(span[10..12]);

            var questions         = new List<MulticastDNSQuestion>();
            var answers           = new List<MulticastDNSRecord>();
            var authorities       = new List<MulticastDNSRecord>();
            var additionals       = new List<MulticastDNSRecord>();
            var warnings          = new List<String>();

            using var stream      = new MemoryStream(buffer, false);
            var offset            = HeaderLength;

            #region Questions

            for (var i = 0; i < questionCount; i++)
            {

                var start = offset;

                if (!MulticastDNSWireFormat.TrySkipName(span, ref offset, out var nameError))
                {
                    ErrorResponse = $"Question {i + 1}: {nameError}";
                    return false;
                }

                if (offset + 4 > buffer.Length)
                {
                    ErrorResponse = $"Question {i + 1}: the packet ends within the type and class fields!";
                    return false;
                }

                var type      = (DNSResourceRecordTypes) BinaryPrimitives.ReadUInt16BigEndian(span[offset..(offset + 2)]);
                var rawClass  =                          BinaryPrimitives.ReadUInt16BigEndian(span[(offset + 2)..(offset + 4)]);
                var unicast   = (rawClass & MulticastDNS.UnicastResponseBit) != 0;

                BinaryPrimitives.WriteUInt16BigEndian(span[(offset + 2)..(offset + 4)], (UInt16) (rawClass & MulticastDNS.ClassMask));
                offset += 4;

                try
                {

                    stream.Position = start;
                    var name = DNSTools.ExtractName(stream);

                    if (DNSServiceName.TryParse(name, out var serviceName, out var parseError))
                        questions.Add(new MulticastDNSQuestion(
                                          new DNSQuestion(serviceName, type, (DNSQueryClasses) (rawClass & MulticastDNS.ClassMask)),
                                          unicast
                                      ));
                    else
                        warnings.Add($"Question {i + 1} skipped: '{name}' {parseError}");

                }
                catch (Exception e)
                {
                    warnings.Add($"Question {i + 1} skipped: {e.Message}");
                }

            }

            #endregion

            #region Resource records

            if (!TryParseSection(buffer, stream, ref offset, answerCount,     answers,     warnings, "Answer",     out ErrorResponse) ||
                !TryParseSection(buffer, stream, ref offset, authorityCount,  authorities, warnings, "Authority",  out ErrorResponse) ||
                !TryParseSection(buffer, stream, ref offset, additionalCount, additionals, warnings, "Additional", out ErrorResponse))
            {
                return false;
            }

            #endregion

            Message = new MulticastDNSMessage(
                          transactionId,
                          (flags1 & 0x80) != 0,
                          (Byte) ((flags1 >> 3) & 0x0F),
                          (flags1 & 0x04) != 0,
                          (flags1 & 0x02) != 0,
                          (flags1 & 0x01) != 0,
                          (DNSResponseCodes) (flags2 & 0x0F),
                          questions,
                          answers,
                          authorities,
                          additionals,
                          warnings
                      );

            return true;

        }

        #endregion

        #region (private static) TryParseSection(Buffer, Stream, ref Offset, Count, Records, Warnings, Section, out ErrorResponse)

        private static Boolean TryParseSection(Byte[]                     Buffer,
                                               MemoryStream               Stream,
                                               ref Int32                  Offset,
                                               UInt16                     Count,
                                               List<MulticastDNSRecord>   Records,
                                               List<String>               Warnings,
                                               String                     Section,
                                               [NotNullWhen(false)] out String?  ErrorResponse)
        {

            ErrorResponse = null;

            var span    = Buffer.AsSpan();
            var offset  = Offset;

            for (var i = 0; i < Count; i++)
            {

                var start = offset;

                if (!MulticastDNSWireFormat.TrySkipName(span, ref offset, out var nameError))
                {
                    ErrorResponse = $"{Section} record {i + 1}: {nameError}";
                    return false;
                }

                if (offset + 10 > Buffer.Length)
                {
                    ErrorResponse = $"{Section} record {i + 1}: the packet ends within the fixed fields!";
                    return false;
                }

                var rawClass    = BinaryPrimitives.ReadUInt16BigEndian(span[(offset + 2)..(offset + 4)]);
                var cacheFlush  = (rawClass & MulticastDNS.CacheFlushBit) != 0;
                var rdLength    = BinaryPrimitives.ReadUInt16BigEndian(span[(offset + 8)..(offset + 10)]);
                var end         = offset + 10 + rdLength;

                if (end > Buffer.Length)
                {
                    ErrorResponse = $"{Section} record {i + 1}: the RDATA ({rdLength} bytes) exceeds the packet!";
                    return false;
                }

                BinaryPrimitives.WriteUInt16BigEndian(span[(offset + 2)..(offset + 4)], (UInt16) (rawClass & MulticastDNS.ClassMask));

                try
                {

                    Stream.Position = start;
                    var record = DNSInfo.ReadResourceRecord(Stream);

                    if (record is not null)
                        Records.Add(new MulticastDNSRecord(record, cacheFlush));
                    else
                        Warnings.Add($"{Section} record {i + 1} skipped: unreadable");

                }
                catch (Exception e)
                {
                    Warnings.Add($"{Section} record {i + 1} skipped: {e.Message}");
                }

                offset = end;

            }

            Offset = offset;
            return true;

        }

        #endregion

        #region Serialize()

        /// <summary>
        /// Serialize this message without name compression, setting the unicast-response
        /// bit of questions, the cache-flush bit of records and the time-to-live overrides.
        /// </summary>
        public Byte[] Serialize()
        {

            using var stream = new MemoryStream();

            stream.WriteUInt16BE(TransactionId);

            var flags1 = (Byte) 0x00;
            var flags2 = (Byte) 0x00;

            if (IsResponse)
                flags1 |= 0x80;

            flags1 |= (Byte) ((Opcode & 0x0F) << 3);

            if (AuthoritativeAnswer)
                flags1 |= 0x04;

            if (Truncated)
                flags1 |= 0x02;

            if (RecursionDesired)
                flags1 |= 0x01;

            flags2 |= (Byte) ((Byte) ResponseCode & 0x0F);

            stream.WriteByte(flags1);
            stream.WriteByte(flags2);

            stream.WriteUInt16BE((UInt16) Questions.  Count);
            stream.WriteUInt16BE((UInt16) Answers.    Count);
            stream.WriteUInt16BE((UInt16) Authorities.Count);
            stream.WriteUInt16BE((UInt16) Additionals.Count);

            foreach (var question in Questions)
            {

                question.Name.Serialize(stream, (Int32) stream.Position, false, null);

                stream.WriteUInt16BE((UInt16) question.Type);
                stream.WriteUInt16BE((UInt16) (((UInt16) question.Class & MulticastDNS.ClassMask) |
                                               (question.UnicastResponseRequested ? MulticastDNS.UnicastResponseBit : 0)));

            }

            foreach (var record in Answers.Concat(Authorities).Concat(Additionals))
            {

                var start = (Int32) stream.Position;

                record.Record.Serialize(stream, UseCompression: false, CompressionOffsets: []);

                if (record.CacheFlush || record.TimeToLiveOverride.HasValue)
                {

                    var buffer  = stream.GetBuffer().AsSpan(0, (Int32) stream.Length);
                    var offset  = start;

                    if (!MulticastDNSWireFormat.TrySkipName(buffer, ref offset, out var error))
                        throw new InvalidOperationException($"The serialized record '{record.Record}' could not be walked: {error}");

                    if (record.CacheFlush)
                    {
                        var rawClass = BinaryPrimitives.ReadUInt16BigEndian(buffer[(offset + 2)..(offset + 4)]);
                        BinaryPrimitives.WriteUInt16BigEndian(buffer[(offset + 2)..(offset + 4)], (UInt16) (rawClass | MulticastDNS.CacheFlushBit));
                    }

                    if (record.TimeToLiveOverride.HasValue)
                    {
                        var seconds = Math.Clamp((Int64) record.TimeToLiveOverride.Value.TotalSeconds, 0, ADNSResourceRecord.MaximumTimeToLive);
                        BinaryPrimitives.WriteUInt32BigEndian(buffer[(offset + 4)..(offset + 8)], (UInt32) seconds);
                    }

                }

            }

            return stream.ToArray();

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this message.
        /// </summary>
        public override String ToString()

            => String.Concat(
                   IsResponse ? "Response" : "Query",
                   TransactionId != 0 ? $" #{TransactionId}" : "",
                   Truncated          ? " (truncated)"       : "",
                   $": {Questions.Count} question(s), {Answers.Count} answer(s), {Authorities.Count} authority record(s), {Additionals.Count} additional record(s)",
                   Warnings.Count > 0 ? $", {Warnings.Count} warning(s)" : ""
               );

        #endregion

    }


    /// <summary>
    /// Helpers for walking the DNS wire format without parsing it.
    /// </summary>
    internal static class MulticastDNSWireFormat
    {

        #region TrySkipName(Packet, ref Offset, out ErrorResponse)

        /// <summary>
        /// Advance the offset over a (possibly compressed) domain name (RFC 1035 §4.1.4).
        /// </summary>
        /// <param name="Packet">The packet.</param>
        /// <param name="Offset">The offset of the name; on success the offset of the field following it.</param>
        /// <param name="ErrorResponse">An error message when the name is malformed.</param>
        public static Boolean TrySkipName(ReadOnlySpan<Byte>  Packet,
                                          ref Int32           Offset,
                                          out String?         ErrorResponse)
        {

            ErrorResponse = null;

            var offset  = Offset;
            var labels  = 0;

            while (true)
            {

                if (offset >= Packet.Length)
                {
                    ErrorResponse = "the packet ends within a name!";
                    return false;
                }

                var length = Packet[offset];

                if (length == 0)
                {
                    Offset = offset + 1;
                    return true;
                }

                if ((length & 0xC0) == 0xC0)
                {

                    if (offset + 1 >= Packet.Length)
                    {
                        ErrorResponse = "the packet ends within a compression pointer!";
                        return false;
                    }

                    Offset = offset + 2;
                    return true;

                }

                if ((length & 0xC0) != 0)
                {
                    ErrorResponse = $"invalid label length byte 0x{length:X2}!";
                    return false;
                }

                offset += 1 + length;

                if (++labels > 128)
                {
                    ErrorResponse = "too many labels within a name!";
                    return false;
                }

            }

        }

        #endregion

    }

}
