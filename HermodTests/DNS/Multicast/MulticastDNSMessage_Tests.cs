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

using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    /// <summary>
    /// The Multicast DNS message (RFC 6762 §18): the unicast-response bit of questions,
    /// the cache-flush bit of records, time-to-live overrides, and a parser that
    /// refuses a broken packet but tolerates a strange record within a good one.
    /// The helpers of <see cref="MulticastDNS"/> are tested here as well.
    /// </summary>
    [TestFixture]
    public class MulticastDNSMessage_Tests
    {

        #region Helpers

        private static readonly DNSServiceName  HostName      = DNSServiceName.Parse("myhost.local.");
        private static readonly DNSServiceName  ServiceName   = DNSServiceName.Parse("_test._tcp.local.");
        private static readonly DNSServiceName  InstanceName  = DNSServiceName.Parse("myhost._test._tcp.local.");

        private static A ARecord(String  Address      = "10.0.0.7",
                                 String  Name         = "myhost.local.",
                                 Int32   TimeToLive   = 120)

            => new (DomainName.Parse(Name),
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(TimeToLive),
                    IPv4Address.Parse(Address));

        private static TXT TXTRecord(params String[] Strings)

            => new (InstanceName,
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(4500),
                    Strings);

        private static SRV SRVRecord(UInt16 Port = 8443)

            => new (InstanceName,
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(120),
                    0,
                    0,
                    IPPort.Parse(Port),
                    DomainName.Parse("myhost.local."));

        private static PTR PTRRecord()

            => new (ServiceName,
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(4500),
                    InstanceName);

        private static MulticastDNSMessage Parse(Byte[] Packet)
        {

            Assert.That(MulticastDNSMessage.TryParse(Packet, out var message, out var error), Is.True, error);

            return message!;

        }

        private const UInt16 QueryFlags     = 0x0000;
        private const UInt16 ResponseFlags  = 0x8400;   // QR + AA
        private const UInt16 ClassIN        = 0x0001;
        private const UInt16 ClassINFlush   = 0x8001;   // IN + cache-flush / unicast-response bit


        /// <summary>
        /// Appends the pieces of a DNS packet in wire order: header, names as
        /// length-prefixed labels, big-endian integers, compression pointers and raw bytes.
        /// </summary>
        private sealed class PacketBuilder
        {

            private readonly List<Byte> bytes = [];

            public Int32 Length
                => bytes.Count;

            public PacketBuilder Header(UInt16  TransactionId,
                                        UInt16  Flags,
                                        UInt16  QuestionCount,
                                        UInt16  AnswerCount,
                                        UInt16  AuthorityCount    = 0,
                                        UInt16  AdditionalCount   = 0)

                => UInt16BE(TransactionId).
                   UInt16BE(Flags).
                   UInt16BE(QuestionCount).
                   UInt16BE(AnswerCount).
                   UInt16BE(AuthorityCount).
                   UInt16BE(AdditionalCount);

            public PacketBuilder UInt16BE(UInt16 Value)
            {
                bytes.Add((Byte) (Value >> 8));
                bytes.Add((Byte) (Value & 0xFF));
                return this;
            }

            public PacketBuilder UInt32BE(UInt32 Value)
            {
                bytes.Add((Byte) (Value >> 24));
                bytes.Add((Byte) (Value >> 16));
                bytes.Add((Byte) (Value >>  8));
                bytes.Add((Byte)  Value);
                return this;
            }

            /// <summary>
            /// One label: its length byte and its (unvalidated) bytes, no terminator.
            /// </summary>
            public PacketBuilder Label(String Label)
            {
                var labelBytes = Encoding.ASCII.GetBytes(Label);
                bytes.Add((Byte) labelBytes.Length);
                bytes.AddRange(labelBytes);
                return this;
            }

            /// <summary>
            /// A complete, uncompressed name: labels and the root terminator.
            /// </summary>
            public PacketBuilder Name(String Name)
            {

                foreach (var label in Name.TrimEnd('.').Split('.'))
                    Label(label);

                bytes.Add(0x00);
                return this;

            }

            /// <summary>
            /// A compression pointer to the given packet offset (RFC 1035 §4.1.4).
            /// </summary>
            public PacketBuilder Pointer(Int32 Offset)
                => UInt16BE((UInt16) (0xC000 | Offset));

            public PacketBuilder Bytes(params Byte[] Values)
            {
                bytes.AddRange(Values);
                return this;
            }

            /// <summary>
            /// TYPE, CLASS, TTL, RDLENGTH and RDATA of a record (the owner name was already written).
            /// </summary>
            public PacketBuilder Fields(DNSResourceRecordTypes  Type,
                                        UInt16                  Class,
                                        UInt32                  TimeToLive,
                                        params Byte[]           RData)

                => UInt16BE((UInt16) Type).
                   UInt16BE(Class).
                   UInt32BE(TimeToLive).
                   UInt16BE((UInt16) RData.Length).
                   Bytes(RData);

            /// <summary>
            /// A complete resource record with an uncompressed owner name.
            /// </summary>
            public PacketBuilder Record(String                  Name,
                                        DNSResourceRecordTypes  Type,
                                        UInt16                  Class,
                                        UInt32                  TimeToLive,
                                        params Byte[]           RData)

                => this.Name(Name).
                        Fields(Type, Class, TimeToLive, RData);

            public Byte[] ToArray()
                => [.. bytes];

        }

        #endregion


        #region Factories and header

        #region Query_HasQueryFlagsOnly()

        [Test]
        public void Query_HasQueryFlagsOnly()
        {

            var query = MulticastDNSMessage.Query([ new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A) ]);

            Assert.Multiple(() => {

                Assert.That(query.IsQuery,              Is.True);
                Assert.That(query.IsResponse,           Is.False);
                Assert.That(query.AuthoritativeAnswer,  Is.False);
                Assert.That(query.Truncated,            Is.False);
                Assert.That(query.RecursionDesired,     Is.False);
                Assert.That(query.Opcode,               Is.EqualTo(0));
                Assert.That(query.ResponseCode,         Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(query.TransactionId,        Is.EqualTo(0));

                Assert.That(query.Questions,            Has.Count.EqualTo(1));
                Assert.That(query.Answers,              Is.Empty);
                Assert.That(query.Authorities,          Is.Empty);
                Assert.That(query.Additionals,          Is.Empty);
                Assert.That(query.Warnings,             Is.Empty);

                // The flag bytes on the wire are all zero.
                var packet = query.Serialize();
                Assert.That(packet[2],                  Is.EqualTo(0x00));
                Assert.That(packet[3],                  Is.EqualTo(0x00));

            });

            // A probe query carries known answers and proposed records, and may be truncated (RFC 6762 §7.2, §8.2).
            var probe = MulticastDNSMessage.Query(
                            [ new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.Any) ],
                            KnownAnswers:  [ new MulticastDNSRecord(PTRRecord()) ],
                            Authorities:   [ new MulticastDNSRecord(ARecord()) ],
                            Truncated:     true
                        );

            Assert.Multiple(() => {
                Assert.That(probe.IsQuery,              Is.True);
                Assert.That(probe.Truncated,            Is.True);
                Assert.That(probe.Answers,              Has.Count.EqualTo(1));
                Assert.That(probe.Authorities,          Has.Count.EqualTo(1));
                Assert.That(probe.Additionals,          Is.Empty);
                Assert.That(probe.Serialize()[2],       Is.EqualTo(0x02), "only the TC bit is set");
            });

        }

        #endregion

        #region Response_IsAuthoritativeResponse()

        [Test]
        public void Response_IsAuthoritativeResponse()
        {

            var response = MulticastDNSMessage.Response(
                               [ new MulticastDNSRecord(ARecord(), true) ],
                               Additionals: [ new MulticastDNSRecord(SRVRecord(), true) ]
                           );

            Assert.Multiple(() => {

                Assert.That(response.IsResponse,           Is.True);
                Assert.That(response.IsQuery,              Is.False);
                Assert.That(response.AuthoritativeAnswer,  Is.True, "RFC 6762 §18.4: every multicast response is authoritative");
                Assert.That(response.Truncated,            Is.False);
                Assert.That(response.RecursionDesired,     Is.False);
                Assert.That(response.Opcode,               Is.EqualTo(0));
                Assert.That(response.ResponseCode,         Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(response.TransactionId,        Is.EqualTo(0));

                Assert.That(response.Questions,            Is.Empty);
                Assert.That(response.Answers,              Has.Count.EqualTo(1));
                Assert.That(response.Authorities,          Is.Empty);
                Assert.That(response.Additionals,          Has.Count.EqualTo(1));

                // QR and AA on the wire
                var packet = response.Serialize();
                Assert.That(packet[2],                     Is.EqualTo(0x84));
                Assert.That(packet[3],                     Is.EqualTo(0x00));

            });

            var parsed = Parse(response.Serialize());

            Assert.Multiple(() => {
                Assert.That(parsed.IsResponse,             Is.True);
                Assert.That(parsed.AuthoritativeAnswer,    Is.True);
                Assert.That(parsed.Answers,                Has.Count.EqualTo(1));
                Assert.That(parsed.Additionals,            Has.Count.EqualTo(1));
            });

            // A legacy unicast response repeats the question and the transaction id (RFC 6762 §6.7).
            var legacy = MulticastDNSMessage.Response(
                             [ new MulticastDNSRecord(ARecord()) ],
                             Questions:      [ new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A) ],
                             TransactionId:  4711
                         );

            Assert.Multiple(() => {
                Assert.That(legacy.TransactionId,          Is.EqualTo(4711));
                Assert.That(legacy.Questions,              Has.Count.EqualTo(1));
                Assert.That(Parse(legacy.Serialize()).TransactionId, Is.EqualTo(4711));
            });

        }

        #endregion

        #region Serialize_WritesTheHeaderFields()

        [Test]
        public void Serialize_WritesTheHeaderFields()
        {

            var message = new MulticastDNSMessage(
                              0x1267,
                              true,
                              0,
                              true,
                              true,
                              true,
                              DNSResponseCodes.NoError,
                              [ new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A), new MulticastDNSQuestion(ServiceName, DNSResourceRecordTypes.PTR) ],
                              [ new MulticastDNSRecord(ARecord()) ],
                              [ new MulticastDNSRecord(ARecord()), new MulticastDNSRecord(ARecord("10.0.0.8")) ],
                              [ new MulticastDNSRecord(SRVRecord()), new MulticastDNSRecord(TXTRecord("txtver=1")), new MulticastDNSRecord(PTRRecord()) ]
                          );

            var packet = message.Serialize();

            Assert.Multiple(() => {

                // ID
                Assert.That(packet[0],   Is.EqualTo(0x12));
                Assert.That(packet[1],   Is.EqualTo(0x67));

                // QR | AA | TC | RD, RCODE 0
                Assert.That(packet[2],   Is.EqualTo(0x87));
                Assert.That(packet[3],   Is.EqualTo(0x00));

                // QDCOUNT, ANCOUNT, NSCOUNT, ARCOUNT
                Assert.That(packet[4],   Is.EqualTo(0x00));
                Assert.That(packet[5],   Is.EqualTo(2));
                Assert.That(packet[6],   Is.EqualTo(0x00));
                Assert.That(packet[7],   Is.EqualTo(1));
                Assert.That(packet[8],   Is.EqualTo(0x00));
                Assert.That(packet[9],   Is.EqualTo(2));
                Assert.That(packet[10],  Is.EqualTo(0x00));
                Assert.That(packet[11],  Is.EqualTo(3));

            });

            var parsed = Parse(packet);

            Assert.Multiple(() => {
                Assert.That(parsed.TransactionId,         Is.EqualTo(0x1267));
                Assert.That(parsed.IsResponse,            Is.True);
                Assert.That(parsed.Opcode,                Is.EqualTo(0));
                Assert.That(parsed.AuthoritativeAnswer,   Is.True);
                Assert.That(parsed.Truncated,             Is.True);
                Assert.That(parsed.RecursionDesired,      Is.True);
                Assert.That(parsed.ResponseCode,          Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(parsed.Questions,             Has.Count.EqualTo(2));
                Assert.That(parsed.Answers,               Has.Count.EqualTo(1));
                Assert.That(parsed.Authorities,           Has.Count.EqualTo(2));
                Assert.That(parsed.Additionals,           Has.Count.EqualTo(3));
            });

        }

        #endregion

        #endregion

        #region Multicast DNS bits

        #region RoundTrip_KeepsTheUnicastResponseBitPerQuestion()

        [Test]
        public void RoundTrip_KeepsTheUnicastResponseBitPerQuestion()
        {

            var query = MulticastDNSMessage.Query([
                            new MulticastDNSQuestion(HostName,    DNSResourceRecordTypes.A,   UnicastResponseRequested: true),
                            new MulticastDNSQuestion(ServiceName, DNSResourceRecordTypes.PTR)
                        ]);

            var packet = query.Serialize();

            Assert.Multiple(() => {

                // Header (12) + "myhost.local." (14) → TYPE at 26, CLASS at 28: QU bit set (RFC 6762 §5.4)
                Assert.That(packet[28],  Is.EqualTo(0x80));
                Assert.That(packet[29],  Is.EqualTo(0x01));

                // "_test._tcp.local." (18) follows at 30 → CLASS at 50: plain IN
                Assert.That(packet[50],  Is.EqualTo(0x00));
                Assert.That(packet[51],  Is.EqualTo(0x01));
                Assert.That(packet,      Has.Length.EqualTo(52));

            });

            var parsed = Parse(packet);

            Assert.Multiple(() => {

                Assert.That(parsed.Questions,                                Has.Count.EqualTo(2));

                Assert.That(parsed.Questions[0].UnicastResponseRequested,    Is.True);
                Assert.That(parsed.Questions[0].Name.FullName,               Is.EqualTo("myhost.local."));
                Assert.That(parsed.Questions[0].Type,                        Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(parsed.Questions[0].Class,                       Is.EqualTo(DNSQueryClasses.IN), "the QU bit is not part of the class");
                Assert.That((UInt16) parsed.Questions[0].Question.QueryClass, Is.EqualTo(1));

                Assert.That(parsed.Questions[1].UnicastResponseRequested,    Is.False);
                Assert.That(parsed.Questions[1].Name.FullName,               Is.EqualTo("_test._tcp.local."));
                Assert.That(parsed.Questions[1].Type,                        Is.EqualTo(DNSResourceRecordTypes.PTR));
                Assert.That(parsed.Questions[1].Class,                       Is.EqualTo(DNSQueryClasses.IN));

                Assert.That(parsed.Warnings,                                 Is.Empty);

            });

        }

        #endregion

        #region RoundTrip_KeepsTheCacheFlushBitPerRecord()

        [Test]
        public void RoundTrip_KeepsTheCacheFlushBitPerRecord()
        {

            var response = MulticastDNSMessage.Response([
                               new MulticastDNSRecord(ARecord(),   CacheFlush: true),
                               new MulticastDNSRecord(PTRRecord(), CacheFlush: false)
                           ]);

            var packet = response.Serialize();

            Assert.Multiple(() => {
                // Header (12) + "myhost.local." (14) → TYPE at 26, CLASS at 28: cache-flush bit set (RFC 6762 §10.2)
                Assert.That(packet[28],  Is.EqualTo(0x80));
                Assert.That(packet[29],  Is.EqualTo(0x01));
            });

            var parsed = Parse(packet);

            Assert.Multiple(() => {

                Assert.That(parsed.Answers,                          Has.Count.EqualTo(2));

                Assert.That(parsed.Answers[0].CacheFlush,            Is.True);
                Assert.That(parsed.Answers[0].Type,                  Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(parsed.Answers[0].Class,                 Is.EqualTo(DNSQueryClasses.IN), "the cache-flush bit is not part of the class");
                Assert.That(parsed.Answers[0].Record.Class,          Is.EqualTo(DNSQueryClasses.IN));
                Assert.That((UInt16) parsed.Answers[0].Record.Class, Is.EqualTo(1));

                Assert.That(parsed.Answers[1].CacheFlush,            Is.False);
                Assert.That(parsed.Answers[1].Type,                  Is.EqualTo(DNSResourceRecordTypes.PTR));
                Assert.That(parsed.Answers[1].Class,                 Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(((PTR) parsed.Answers[1].Record).Target, Is.EqualTo(InstanceName));

                Assert.That(parsed.Warnings,                         Is.Empty);

            });

        }

        #endregion

        #region RoundTrip_AppliesTheTimeToLiveOverride()

        [Test]
        public void RoundTrip_AppliesTheTimeToLiveOverride()
        {

            var a         = ARecord(TimeToLive: 120);

            var response  = MulticastDNSMessage.Response([
                                new MulticastDNSRecord(a, false, TimeSpan.FromSeconds(60)),
                                new MulticastDNSRecord(a)
                            ]);

            Assert.Multiple(() => {

                // The override is applied on the wire without touching the record itself.
                Assert.That(response.Answers[0].TimeToLive,              Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(response.Answers[0].TimeToLiveOverride,      Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(response.Answers[0].Record.TimeToLive,       Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(response.Answers[1].TimeToLive,              Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(response.Answers[1].TimeToLiveOverride,      Is.Null);

            });

            var packet = response.Serialize();

            Assert.Multiple(() => {
                // Header (12) + "myhost.local." (14) + TYPE (2) + CLASS (2) → TTL at 30..33
                Assert.That(packet[30..34],  Is.EqualTo(new Byte[] { 0x00, 0x00, 0x00, 60 }));
            });

            var parsed = Parse(packet);

            Assert.Multiple(() => {
                Assert.That(parsed.Answers[0].TimeToLive,           Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(parsed.Answers[0].Record.TimeToLive,    Is.EqualTo(TimeSpan.FromSeconds(60)));
                Assert.That(parsed.Answers[0].IsGoodbye,            Is.False);
                Assert.That(parsed.Answers[1].TimeToLive,           Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(a.TimeToLive,                           Is.EqualTo(TimeSpan.FromSeconds(120)), "the original record is untouched");
            });

        }

        #endregion

        #region ParsedClasses_AreStrippedOfTheHighBit()

        [Test]
        public void ParsedClasses_AreStrippedOfTheHighBit()
        {

            // A hand-built packet with the high bit set in a question class (QU)
            // and in a record class (cache-flush).
            var packet = new PacketBuilder().
                             Header(0, ResponseFlags, 1, 1).
                             Name("myhost.local.").UInt16BE((UInt16) DNSResourceRecordTypes.A).UInt16BE(ClassINFlush).
                             Record("myhost.local.", DNSResourceRecordTypes.A, ClassINFlush, 120, 10, 0, 0, 7).
                             ToArray();

            var parsed = Parse(packet);

            Assert.Multiple(() => {

                Assert.That(parsed.Questions,                                  Has.Count.EqualTo(1));
                Assert.That(parsed.Questions[0].UnicastResponseRequested,      Is.True);
                Assert.That(parsed.Questions[0].Class,                         Is.EqualTo(DNSQueryClasses.IN));
                Assert.That((UInt16) parsed.Questions[0].Question.QueryClass,  Is.EqualTo(1));

                Assert.That(parsed.Answers,                                    Has.Count.EqualTo(1));
                Assert.That(parsed.Answers[0].CacheFlush,                      Is.True);
                Assert.That(parsed.Answers[0].Class,                           Is.EqualTo(DNSQueryClasses.IN));
                Assert.That((UInt16) parsed.Answers[0].Record.Class,           Is.EqualTo(1));
                Assert.That(((A) parsed.Answers[0].Record).IPv4Address,        Is.EqualTo(IPv4Address.Parse("10.0.0.7")));

                Assert.That(parsed.Warnings,                                   Is.Empty);

            });

        }

        #endregion

        #region GoodbyeRecord_HasATimeToLiveOfZero()

        [Test]
        public void GoodbyeRecord_HasATimeToLiveOfZero()
        {

            var goodbye  = new MulticastDNSRecord(ARecord(TimeToLive: 120), true, TimeSpan.Zero);
            var normal   = new MulticastDNSRecord(ARecord(TimeToLive: 120));

            Assert.Multiple(() => {
                Assert.That(goodbye.IsGoodbye,      Is.True);
                Assert.That(goodbye.TimeToLive,     Is.EqualTo(TimeSpan.Zero));
                Assert.That(normal.IsGoodbye,       Is.False);
            });

            var packet = MulticastDNSMessage.Response([ goodbye ]).Serialize();

            // TTL at 30..33 is zero on the wire
            Assert.That(packet[30..34], Is.EqualTo(new Byte[] { 0x00, 0x00, 0x00, 0x00 }));

            var parsed = Parse(packet);

            Assert.Multiple(() => {
                Assert.That(parsed.Answers,                                Has.Count.EqualTo(1));
                Assert.That(parsed.Answers[0].IsGoodbye,                   Is.True);
                Assert.That(parsed.Answers[0].TimeToLive,                  Is.EqualTo(TimeSpan.Zero));
                Assert.That(parsed.Answers[0].CacheFlush,                  Is.True);
                Assert.That(((A) parsed.Answers[0].Record).IPv4Address,    Is.EqualTo(IPv4Address.Parse("10.0.0.7")));
            });

        }

        #endregion

        #endregion

        #region Sections

        #region RoundTrip_KeepsAllFourSections()

        [Test]
        public void RoundTrip_KeepsAllFourSections()
        {

            var message = new MulticastDNSMessage(
                              0,
                              true,
                              0,
                              true,
                              false,
                              false,
                              DNSResponseCodes.NoError,
                              [ new MulticastDNSQuestion(InstanceName, DNSResourceRecordTypes.Any) ],
                              [ new MulticastDNSRecord(PTRRecord()), new MulticastDNSRecord(SRVRecord(), true) ],
                              [ new MulticastDNSRecord(TXTRecord("txtver=1"), true) ],
                              [ new MulticastDNSRecord(ARecord(), true), new MulticastDNSRecord(ARecord("10.0.0.8"), true), new MulticastDNSRecord(TXTRecord("a", "b")) ]
                          );

            var parsed = Parse(message.Serialize());

            Assert.Multiple(() => {

                Assert.That(parsed.Questions,                                  Has.Count.EqualTo(1));
                Assert.That(parsed.Answers,                                    Has.Count.EqualTo(2));
                Assert.That(parsed.Authorities,                                Has.Count.EqualTo(1));
                Assert.That(parsed.Additionals,                                Has.Count.EqualTo(3));
                Assert.That(parsed.AllRecords.Count(),                         Is.EqualTo(6));
                Assert.That(parsed.Warnings,                                   Is.Empty);

                Assert.That(parsed.Questions[0].Name,                          Is.EqualTo(InstanceName));
                Assert.That(parsed.Questions[0].Type,                          Is.EqualTo(DNSResourceRecordTypes.Any));

                Assert.That(parsed.Answers.    Select(r => r.Type),            Is.EqualTo(new[] { DNSResourceRecordTypes.PTR, DNSResourceRecordTypes.SRV }));
                Assert.That(parsed.Authorities.Select(r => r.Type),            Is.EqualTo(new[] { DNSResourceRecordTypes.TXT }));
                Assert.That(parsed.Additionals.Select(r => r.Type),            Is.EqualTo(new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.A, DNSResourceRecordTypes.TXT }));

                Assert.That(((PTR) parsed.Answers[0].Record).Target,           Is.EqualTo(InstanceName));
                Assert.That(((SRV) parsed.Answers[1].Record).Port.ToUInt16(),  Is.EqualTo(8443));
                Assert.That(parsed.Answers[1].CacheFlush,                      Is.True);
                Assert.That(((TXT) parsed.Authorities[0].Record).Strings,      Is.EqualTo(new[] { "txtver=1" }));
                Assert.That(((A)   parsed.Additionals[1].Record).IPv4Address,  Is.EqualTo(IPv4Address.Parse("10.0.0.8")));
                Assert.That(((TXT) parsed.Additionals[2].Record).Strings,      Is.EqualTo(new[] { "a", "b" }));
                Assert.That(parsed.Additionals[2].CacheFlush,                  Is.False);

            });

        }

        #endregion

        #endregion

        #region Broken packets are refused

        #region PacketShorterThanTheHeader_IsRejected(Length)

        [TestCase(0)]
        [TestCase(5)]
        [TestCase(11)]
        public void PacketShorterThanTheHeader_IsRejected(Int32 Length)
        {

            Assert.That(MulticastDNSMessage.TryParse(new Byte[Length], out var message, out var error), Is.False);

            Assert.Multiple(() => {
                Assert.That(message,  Is.Null);
                Assert.That(error,    Is.Not.Null);
                Assert.That(error,    Does.Contain("shorter"));
            });

        }

        #endregion

        #region EmptyHeader_IsAnEmptyQuery()

        [Test]
        public void EmptyHeader_IsAnEmptyQuery()
        {

            // Twelve zero bytes are a complete, if pointless, DNS message.
            var parsed = Parse(new Byte[12]);

            Assert.Multiple(() => {
                Assert.That(parsed.IsQuery,      Is.True);
                Assert.That(parsed.Questions,    Is.Empty);
                Assert.That(parsed.AllRecords,   Is.Empty);
                Assert.That(parsed.Warnings,     Is.Empty);
            });

        }

        #endregion

        #region PacketEndingWithinAQuestion_IsRejected()

        [Test]
        public void PacketEndingWithinAQuestion_IsRejected()
        {

            // One question announced, but the name stops after three bytes of a six-byte label.
            var withinName = new PacketBuilder().
                                 Header(0, QueryFlags, 1, 0).
                                 Bytes(6, (Byte) 'm', (Byte) 'y', (Byte) 'h').
                                 ToArray();

            // The name is complete, but only the TYPE follows.
            var withinFields = new PacketBuilder().
                                   Header(0, QueryFlags, 1, 0).
                                   Name("myhost.local.").UInt16BE((UInt16) DNSResourceRecordTypes.A).
                                   ToArray();

            Assert.Multiple(() => {

                Assert.That(MulticastDNSMessage.TryParse(withinName, out var message1, out var error1), Is.False);
                Assert.That(message1,  Is.Null);
                Assert.That(error1,    Does.Contain("Question 1"));

                Assert.That(MulticastDNSMessage.TryParse(withinFields, out var message2, out var error2), Is.False);
                Assert.That(message2,  Is.Null);
                Assert.That(error2,    Does.Contain("Question 1"));

            });

        }

        #endregion

        #region RecordWhoseRDataExceedsThePacket_IsRejected()

        [Test]
        public void RecordWhoseRDataExceedsThePacket_IsRejected()
        {

            var packet = new PacketBuilder().
                             Header(0, ResponseFlags, 0, 1).
                             Name("myhost.local.").
                             UInt16BE((UInt16) DNSResourceRecordTypes.A).
                             UInt16BE(ClassIN).
                             UInt32BE(120).
                             UInt16BE(100).              // RDLENGTH claims 100 bytes ...
                             Bytes(10, 0, 0, 7).         // ... but only four follow
                             ToArray();

            Assert.That(MulticastDNSMessage.TryParse(packet, out var message, out var error), Is.False);

            Assert.Multiple(() => {
                Assert.That(message,  Is.Null);
                Assert.That(error,    Does.Contain("Answer record 1"));
                Assert.That(error,    Does.Contain("RDATA"));
            });

        }

        #endregion

        #region RecordEndingWithinTheFixedFields_IsRejected()

        [Test]
        public void RecordEndingWithinTheFixedFields_IsRejected()
        {

            // The owner name is complete, but TTL and RDLENGTH are missing.
            var withinFields = new PacketBuilder().
                                   Header(0, ResponseFlags, 0, 1).
                                   Name("myhost.local.").
                                   UInt16BE((UInt16) DNSResourceRecordTypes.A).
                                   UInt16BE(ClassIN).
                                   ToArray();

            // Two answers announced, one present.
            var missingRecord = new PacketBuilder().
                                    Header(0, ResponseFlags, 0, 2).
                                    Record("myhost.local.", DNSResourceRecordTypes.A, ClassIN, 120, 10, 0, 0, 7).
                                    ToArray();

            Assert.Multiple(() => {

                Assert.That(MulticastDNSMessage.TryParse(withinFields, out var message1, out var error1), Is.False);
                Assert.That(message1,  Is.Null);
                Assert.That(error1,    Does.Contain("Answer record 1"));

                Assert.That(MulticastDNSMessage.TryParse(missingRecord, out var message2, out var error2), Is.False);
                Assert.That(message2,  Is.Null);
                Assert.That(error2,    Does.Contain("Answer record 2"));

            });

        }

        #endregion

        #endregion

        #region Strange records are skipped, not fatal

        #region RecordWithAnUnrepresentableName_IsSkippedWithAWarning()

        [Test]
        public void RecordWithAnUnrepresentableName_IsSkippedWithAWarning()
        {

            // The first record's owner name has a label with a space and an exclamation
            // mark — legal on the wire, but not a name this library can represent.
            var packet = new PacketBuilder().
                             Header(0, ResponseFlags, 0, 2).
                             Label("my host!").Label("local").Bytes(0x00).
                             Fields(DNSResourceRecordTypes.A, ClassIN, 120, 10, 0, 0, 7).
                             Record("other.local.", DNSResourceRecordTypes.A, ClassIN, 120, 10, 0, 0, 8).
                             ToArray();

            var parsed = Parse(packet);

            Assert.Multiple(() => {

                Assert.That(parsed.Answers,                                Has.Count.EqualTo(1), "the strange record is skipped, the good one is delivered");
                Assert.That(parsed.Answers[0].Name.FullName,               Is.EqualTo("other.local."));
                Assert.That(((A) parsed.Answers[0].Record).IPv4Address,    Is.EqualTo(IPv4Address.Parse("10.0.0.8")));

                Assert.That(parsed.Warnings,                               Has.Count.EqualTo(1));
                Assert.That(parsed.Warnings[0],                            Does.Contain("Answer record 1"));
                Assert.That(parsed.ToString(),                             Does.Contain("1 warning(s)"));

            });

        }

        #endregion

        #region QuestionWithAnUnrepresentableName_IsSkippedWithAWarning()

        [Test]
        public void QuestionWithAnUnrepresentableName_IsSkippedWithAWarning()
        {

            var packet = new PacketBuilder().
                             Header(0, QueryFlags, 2, 0).
                             Label("bad name").Label("local").Bytes(0x00).UInt16BE((UInt16) DNSResourceRecordTypes.A).UInt16BE(ClassIN).
                             Name("myhost.local.").                        UInt16BE((UInt16) DNSResourceRecordTypes.A).UInt16BE(ClassINFlush).
                             ToArray();

            var parsed = Parse(packet);

            Assert.Multiple(() => {
                Assert.That(parsed.Questions,                              Has.Count.EqualTo(1));
                Assert.That(parsed.Questions[0].Name.FullName,             Is.EqualTo("myhost.local."));
                Assert.That(parsed.Questions[0].UnicastResponseRequested,  Is.True);
                Assert.That(parsed.Warnings,                               Has.Count.EqualTo(1));
                Assert.That(parsed.Warnings[0],                            Does.Contain("Question 1"));
            });

        }

        #endregion

        #region UnknownRecordType_IsKeptAsUnknownRecord()

        [Test]
        public void UnknownRecordType_IsKeptAsUnknownRecord()
        {

            var packet = new PacketBuilder().
                             Header(0, ResponseFlags, 0, 2).
                             Record("myhost.local.", (DNSResourceRecordTypes) 65280, ClassIN, 120, 1, 2, 3).
                             Record("myhost.local.", DNSResourceRecordTypes.A,       ClassIN, 120, 10, 0, 0, 7).
                             ToArray();

            var parsed = Parse(packet);

            Assert.Multiple(() => {

                Assert.That(parsed.Answers,                                Has.Count.EqualTo(2));
                Assert.That(parsed.Warnings,                               Is.Empty, "an unknown type is data, not a warning (RFC 3597 §2)");

                Assert.That(parsed.Answers[0].Record,                      Is.InstanceOf<UnknownRecord>());
                Assert.That(parsed.Answers[0].Type,                        Is.EqualTo((DNSResourceRecordTypes) 65280));
                Assert.That(parsed.Answers[0].Name.FullName,               Is.EqualTo("myhost.local."));
                Assert.That(((UnknownRecord) parsed.Answers[0].Record).RData, Is.EqualTo(new Byte[] { 1, 2, 3 }));

                // ... and the record after it is still read from the right offset.
                Assert.That(parsed.Answers[1].Record,                      Is.InstanceOf<A>());
                Assert.That(((A) parsed.Answers[1].Record).IPv4Address,    Is.EqualTo(IPv4Address.Parse("10.0.0.7")));

            });

        }

        #endregion

        #region CompressionPointer_ResolvesToTheSameName()

        [Test]
        public void CompressionPointer_ResolvesToTheSameName()
        {

            // Record 1: "myhost.local." written out at offset 12 ("local" starts at 19).
            // Record 2: a pointer to offset 12.
            // Record 3: "other" plus a pointer to offset 19.
            var packet = new PacketBuilder().
                             Header(0, ResponseFlags, 0, 3).
                             Record("myhost.local.", DNSResourceRecordTypes.A, ClassIN, 120, 10, 0, 0, 7).
                             Pointer(12).               Fields(DNSResourceRecordTypes.A, ClassIN, 120, 10, 0, 0, 8).
                             Label("other").Pointer(19).Fields(DNSResourceRecordTypes.A, ClassIN, 120, 10, 0, 0, 9).
                             ToArray();

            var parsed = Parse(packet);

            Assert.Multiple(() => {

                Assert.That(parsed.Answers,                                    Has.Count.EqualTo(3));
                Assert.That(parsed.Warnings,                                   Is.Empty);

                Assert.That(parsed.Answers[0].Name.FullName,                   Is.EqualTo("myhost.local."));
                Assert.That(parsed.Answers[1].Name.FullName,                   Is.EqualTo("myhost.local."));
                Assert.That(parsed.Answers[1].Name,                            Is.EqualTo(parsed.Answers[0].Name));
                Assert.That(parsed.Answers[0].Record.IsSameRRSet(parsed.Answers[1].Record), Is.True);
                Assert.That(((A) parsed.Answers[1].Record).IPv4Address,        Is.EqualTo(IPv4Address.Parse("10.0.0.8")));

                Assert.That(parsed.Answers[2].Name.FullName,                   Is.EqualTo("other.local."));
                Assert.That(((A) parsed.Answers[2].Record).IPv4Address,        Is.EqualTo(IPv4Address.Parse("10.0.0.9")));

            });

        }

        #endregion

        #endregion

        #region Questions

        #region Question_Matches_ByNameTypeAndClass()

        [Test]
        public void Question_Matches_ByNameTypeAndClass()
        {

            var a       = ARecord();
            var aaaa    = new AAAA(DomainName.Parse("myhost.local."), DNSQueryClasses.IN, TimeSpan.FromSeconds(120), IPv6Address.Parse("fe80::7"));
            var chaos   = new A   (DomainName.Parse("myhost.local."), DNSQueryClasses.CH, TimeSpan.FromSeconds(120), IPv4Address.Parse("10.0.0.7"));
            var other   = ARecord(Name: "other.local.");

            var exact     = new MulticastDNSQuestion(DNSServiceName.Parse("MYHOST.LOCAL."), DNSResourceRecordTypes.A);
            var anyType   = new MulticastDNSQuestion(HostName);
            var anyClass  = new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A,   false, DNSQueryClasses.ANY);
            var anyBoth   = new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.Any, false, DNSQueryClasses.ANY);

            Assert.Multiple(() => {

                Assert.That(exact.Name,                 Is.EqualTo(HostName));
                Assert.That(exact.Type,                 Is.EqualTo(DNSResourceRecordTypes.A));
                Assert.That(exact.Class,                Is.EqualTo(DNSQueryClasses.IN));
                Assert.That(anyType.Type,               Is.EqualTo(DNSResourceRecordTypes.Any), "ANY is the default type");

                // name (case-insensitive), type and class
                Assert.That(exact.Matches(a),           Is.True);
                Assert.That(exact.Matches(aaaa),        Is.False, "type differs");
                Assert.That(exact.Matches(other),       Is.False, "name differs");
                Assert.That(exact.Matches(chaos),       Is.False, "class differs");

                // type ANY matches every type
                Assert.That(anyType.Matches(a),         Is.True);
                Assert.That(anyType.Matches(aaaa),      Is.True);
                Assert.That(anyType.Matches(other),     Is.False);
                Assert.That(anyType.Matches(chaos),     Is.False);

                // class ANY matches every class
                Assert.That(anyClass.Matches(a),        Is.True);
                Assert.That(anyClass.Matches(chaos),    Is.True);
                Assert.That(anyClass.Matches(aaaa),     Is.False);

                Assert.That(anyBoth.Matches(a),         Is.True);
                Assert.That(anyBoth.Matches(aaaa),      Is.True);
                Assert.That(anyBoth.Matches(chaos),     Is.True);
                Assert.That(anyBoth.Matches(other),     Is.False);

            });

        }

        #endregion

        #endregion

        #region Text representations

        #region ToString_ContainsTheSectionCounts()

        [Test]
        public void ToString_ContainsTheSectionCounts()
        {

            var query = MulticastDNSMessage.Query(
                            [ new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A) ],
                            KnownAnswers: [ new MulticastDNSRecord(ARecord()), new MulticastDNSRecord(ARecord("10.0.0.8")) ]
                        ).ToString();

            var response = MulticastDNSMessage.Response(
                               [ new MulticastDNSRecord(ARecord()) ],
                               Additionals:    [ new MulticastDNSRecord(SRVRecord()), new MulticastDNSRecord(TXTRecord("a")), new MulticastDNSRecord(PTRRecord()) ],
                               Truncated:      true,
                               TransactionId:  4711
                           ).ToString();

            Assert.Multiple(() => {

                Assert.That(query,     Does.StartWith("Query"));
                Assert.That(query,     Does.Contain("1 question(s)"));
                Assert.That(query,     Does.Contain("2 answer(s)"));
                Assert.That(query,     Does.Contain("0 authority record(s)"));
                Assert.That(query,     Does.Contain("0 additional record(s)"));
                Assert.That(query,     Does.Not.Contain("#"));
                Assert.That(query,     Does.Not.Contain("warning"));

                Assert.That(response,  Does.StartWith("Response"));
                Assert.That(response,  Does.Contain("#4711"));
                Assert.That(response,  Does.Contain("(truncated)"));
                Assert.That(response,  Does.Contain("0 question(s)"));
                Assert.That(response,  Does.Contain("1 answer(s)"));
                Assert.That(response,  Does.Contain("3 additional record(s)"));

                Assert.That(new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A, true).ToString(),  Does.Contain("myhost.local.").And.Contain("(QU)"));
                Assert.That(new MulticastDNSQuestion(HostName, DNSResourceRecordTypes.A).ToString(),        Does.Not.Contain("(QU)"));

                Assert.That(new MulticastDNSRecord(ARecord(), true, TimeSpan.FromSeconds(60)).ToString(),   Does.Contain("(cache-flush)").And.Contain("(TTL 60s)"));
                Assert.That(new MulticastDNSRecord(ARecord()).ToString(),                                   Does.Not.Contain("(cache-flush)").And.Not.Contain("(TTL"));

            });

        }

        #endregion

        #endregion

        #region MulticastDNS helpers

        #region IsLocalName(Name, Expected)

        [TestCase("myhost.local",              true)]
        [TestCase("MYHOST.LOCAL.",             true)]
        [TestCase("local",                     true)]
        [TestCase("local.",                    true)]
        [TestCase("a.b.c.local.",              true)]
        [TestCase("5.254.169.in-addr.arpa",    true)]
        [TestCase("5.254.169.IN-ADDR.ARPA.",   true)]
        [TestCase("x.8.e.f.ip6.arpa",          true)]
        [TestCase("1.9.e.f.ip6.arpa",          true)]
        [TestCase("1.a.e.f.ip6.arpa",          true)]
        [TestCase("1.b.e.f.ip6.arpa.",         true)]
        [TestCase("example.com",               false)]
        [TestCase("localhost",                 false)]
        [TestCase("mylocal.com",               false)]
        [TestCase("notlocal",                  false)]
        [TestCase("local.example.com",         false)]
        [TestCase("10.0.0.10.in-addr.arpa",    false)]
        [TestCase("1.c.e.f.ip6.arpa",          false)]
        [TestCase("",                          false)]
        [TestCase("   ",                       false)]
        public void IsLocalName(String Name, Boolean Expected)
        {

            Assert.That(MulticastDNS.IsLocalName(Name),
                        Is.EqualTo(Expected),
                        $"'{Name}'");

        }

        #endregion

        #region IsLocalName_AcceptsDomainNamesAndServiceNames()

        [Test]
        public void IsLocalName_AcceptsDomainNamesAndServiceNames()
        {

            Assert.Multiple(() => {

                Assert.That(MulticastDNS.IsLocalName(DomainName.    Parse("myhost.local.")),        Is.True);
                Assert.That(MulticastDNS.IsLocalName(DomainName.    Parse("MyHost.Local")),         Is.True);
                Assert.That(MulticastDNS.IsLocalName(DNSServiceName.Parse("_http._tcp.local.")),    Is.True);
                Assert.That(MulticastDNS.IsLocalName(DomainName.    Parse("example.com.")),         Is.False);
                Assert.That(MulticastDNS.IsLocalName(DNSServiceName.Parse("_http._tcp.example.com.")), Is.False);

                Assert.That(() => MulticastDNS.IsLocalName((IDomainName) null!), Throws.InstanceOf<ArgumentNullException>());

                Assert.That(MulticastDNS.LocalDomain.FullName,      Is.EqualTo("local."));

            });

        }

        #endregion

        #region IsUniqueRecordType(Type, Expected)

        [TestCase(DNSResourceRecordTypes.PTR,    false)]
        [TestCase(DNSResourceRecordTypes.NSEC,   false)]
        [TestCase(DNSResourceRecordTypes.A,      true)]
        [TestCase(DNSResourceRecordTypes.AAAA,   true)]
        [TestCase(DNSResourceRecordTypes.SRV,    true)]
        [TestCase(DNSResourceRecordTypes.TXT,    true)]
        [TestCase(DNSResourceRecordTypes.HINFO,  true)]
        public void IsUniqueRecordType(DNSResourceRecordTypes Type, Boolean Expected)
        {

            Assert.That(MulticastDNS.IsUniqueRecordType(Type),
                        Is.EqualTo(Expected),
                        $"{Type}");

        }

        #endregion

        #region DefaultTimeToLive(Type, ExpectedSeconds)

        [TestCase(DNSResourceRecordTypes.A,      120)]
        [TestCase(DNSResourceRecordTypes.AAAA,   120)]
        [TestCase(DNSResourceRecordTypes.SRV,    120)]
        [TestCase(DNSResourceRecordTypes.HINFO,  120)]
        [TestCase(DNSResourceRecordTypes.PTR,    4500)]
        [TestCase(DNSResourceRecordTypes.TXT,    4500)]
        [TestCase(DNSResourceRecordTypes.NSEC,   4500)]
        public void DefaultTimeToLive(DNSResourceRecordTypes Type, Int32 ExpectedSeconds)
        {

            Assert.Multiple(() => {

                Assert.That(MulticastDNS.DefaultTimeToLive(Type),   Is.EqualTo(TimeSpan.FromSeconds(ExpectedSeconds)), $"{Type}");
                Assert.That(MulticastDNS.IsHostRecordType(Type),    Is.EqualTo(ExpectedSeconds == 120),                 $"{Type}");

                // RFC 6762 §10: 120 seconds for records containing a host name, 75 minutes otherwise.
                Assert.That(MulticastDNS.HostRecordTimeToLive,      Is.EqualTo(TimeSpan.FromSeconds(120)));
                Assert.That(MulticastDNS.SharedRecordTimeToLive,    Is.EqualTo(TimeSpan.FromMinutes(75)));

            });

        }

        #endregion

        #endregion

    }

}
