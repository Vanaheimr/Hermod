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
    /// The record comparisons Multicast DNS is built on: resource record sets
    /// (RFC 6762 §10.2), identical records (§6, §7.1), the lexicographic order of
    /// probe tie-breaking (§8.2.1) and the dictionary keys derived from them.
    /// </summary>
    [TestFixture]
    public class MulticastDNSRecordExtensions_Tests
    {

        #region Helpers

        private static readonly DNSServiceName  InstanceName  = DNSServiceName.Parse("myhost._test._tcp.local.");

        private static A ARecord(String           Address      = "10.0.0.7",
                                 String           Name         = "myhost.local.",
                                 Int32            TimeToLive   = 120,
                                 DNSQueryClasses  Class        = DNSQueryClasses.IN)

            => new (DomainName.Parse(Name),
                    Class,
                    TimeSpan.FromSeconds(TimeToLive),
                    IPv4Address.Parse(Address));

        private static AAAA AAAARecord(String  Address   = "fe80::7",
                                       String  Name      = "myhost.local.")

            => new (DomainName.Parse(Name),
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(120),
                    IPv6Address.Parse(Address));

        private static TXT TXTRecord(params String[] Strings)

            => new (DomainName.Parse("myhost.local."),
                    DNSQueryClasses.IN,
                    TimeSpan.FromSeconds(4500),
                    Strings);

        #endregion


        #region RData

        #region RData_OfAnARecord_IsTheAddress()

        [Test]
        public void RData_OfAnARecord_IsTheAddress()
        {

            var a     = ARecord("10.0.0.7");
            var aaaa  = AAAARecord("fe80::7");

            Assert.Multiple(() => {

                Assert.That(a.RData().ToArray(),         Is.EqualTo(new Byte[] { 10, 0, 0, 7 }));

                // "myhost.local." (14) + TYPE, CLASS, TTL, RDLENGTH (10) + RDATA (4)
                Assert.That(a.ToWireFormat(),            Has.Length.EqualTo(28));

                var rdata6 = aaaa.RData().ToArray();
                Assert.That(rdata6,                      Has.Length.EqualTo(16));
                Assert.That(rdata6[0],                   Is.EqualTo(0xFE));
                Assert.That(rdata6[1],                   Is.EqualTo(0x80));
                Assert.That(rdata6[15],                  Is.EqualTo(0x07));

            });

        }

        #endregion

        #region RData_OfAnSRVRecord_StartsWithPriorityWeightAndPort()

        [Test]
        public void RData_OfAnSRVRecord_StartsWithPriorityWeightAndPort()
        {

            var srv    = new SRV(InstanceName, DNSQueryClasses.IN, TimeSpan.FromSeconds(120), 10, 20, IPPort.Parse(8443), DomainName.Parse("myhost.local."));
            var rdata  = srv.RData().ToArray();

            Assert.Multiple(() => {

                // priority 10, weight 20, port 8443 = 0x20FB, all big-endian
                Assert.That(rdata[0..6],   Is.EqualTo(new Byte[] { 0x00, 0x0A, 0x00, 0x14, 0x20, 0xFB }));

                // followed by the uncompressed target name (RFC 2782)
                Assert.That(rdata[6..],    Is.EqualTo(Encoding.ASCII.GetBytes("\u0006myhost\u0005local\u0000")));
                Assert.That(rdata,         Has.Length.EqualTo(20));

            });

        }

        #endregion

        #endregion

        #region Resource record sets and identity

        #region IsSameRRSet_IgnoresCaseTimeToLiveAndTheCacheFlushBit()

        [Test]
        public void IsSameRRSet_IgnoresCaseTimeToLiveAndTheCacheFlushBit()
        {

            var a1  = ARecord("10.0.0.7", "MyHost.Local.", 120);
            var a2  = ARecord("10.0.0.8", "myhost.local.", 60);
            var a3  = ARecord("10.0.0.9", "myhost.local.", 120, (DNSQueryClasses) (0x8000 | (UInt16) DNSQueryClasses.IN));

            Assert.Multiple(() => {

                Assert.That(a1.IsSameRRSet(a1),   Is.True);
                Assert.That(a1.IsSameRRSet(a2),   Is.True,  "same name (case-insensitive), type and class; RDATA and TTL do not matter");
                Assert.That(a2.IsSameRRSet(a1),   Is.True);
                Assert.That(a1.IsSameRRSet(a3),   Is.True,  "the cache-flush bit in the class is ignored");
                Assert.That(a3.IsSameRRSet(a2),   Is.True);

            });

        }

        #endregion

        #region IsSameRRSet_DistinguishesNameTypeAndClass()

        [Test]
        public void IsSameRRSet_DistinguishesNameTypeAndClass()
        {

            var a      = ARecord();
            var aaaa   = AAAARecord();
            var other  = ARecord(Name: "other.local.");
            var chaos  = ARecord(Class: DNSQueryClasses.CH);

            Assert.Multiple(() => {
                Assert.That(a.IsSameRRSet(aaaa),    Is.False, "type differs");
                Assert.That(a.IsSameRRSet(other),   Is.False, "name differs");
                Assert.That(a.IsSameRRSet(chaos),   Is.False, "class differs");
            });

        }

        #endregion

        #region IsIdenticalTo_ComparesTheRData()

        [Test]
        public void IsIdenticalTo_ComparesTheRData()
        {

            var a7        = ARecord("10.0.0.7", TimeToLive: 120);
            var a7Later   = ARecord("10.0.0.7", TimeToLive: 60);
            var a7Upper   = ARecord("10.0.0.7", "MYHOST.LOCAL.");
            var a8        = ARecord("10.0.0.8");
            var txtAB     = TXTRecord("a", "b");
            var txtAB2    = TXTRecord("a", "b");
            var txtBA     = TXTRecord("b", "a");

            Assert.Multiple(() => {

                Assert.That(a7.IsIdenticalTo(a8),        Is.False, "10.0.0.7 and 10.0.0.8 differ");
                Assert.That(a7.IsIdenticalTo(a7Later),   Is.True,  "the time-to-live is not part of the identity");
                Assert.That(a7.IsIdenticalTo(a7Upper),   Is.True,  "the owner name compares case-insensitively");
                Assert.That(a7.IsIdenticalTo(AAAARecord()), Is.False, "different type");

                Assert.That(txtAB.IsIdenticalTo(txtAB2), Is.True);
                Assert.That(txtAB.IsIdenticalTo(txtBA),  Is.False, "the character-strings are ordered");

            });

        }

        #endregion

        #endregion

        #region Lexicographic order (RFC 6762 §8.2.1)

        #region CompareLexicographically_OrdersByClassThenTypeThenRData()

        [Test]
        public void CompareLexicographically_OrdersByClassThenTypeThenRData()
        {

            var aIN     = ARecord("10.0.0.7");
            var aCH     = ARecord("10.0.0.7", Class: DNSQueryClasses.CH);
            var aFlush  = ARecord("10.0.0.7", Class: (DNSQueryClasses) 0x8001);
            var aBig    = ARecord("255.255.255.255");
            var txt     = TXTRecord("\u0000");
            var aOther  = ARecord("10.0.0.7", "zzz.local.", 60);

            Assert.Multiple(() => {

                // class first: IN (1) < CH (3)
                Assert.That(aIN.CompareLexicographically(aCH),      Is.Negative);
                Assert.That(aCH.CompareLexicographically(aIN),      Is.Positive);

                // the cache-flush bit is not part of the class
                Assert.That(aIN.CompareLexicographically(aFlush),   Is.Zero);

                // then type: A (1) < TXT (16), whatever the RDATA
                Assert.That(aBig.CompareLexicographically(txt),     Is.Negative);
                Assert.That(txt.CompareLexicographically(aBig),     Is.Positive);

                // the owner name and the time-to-live are not compared at all
                Assert.That(aIN.CompareLexicographically(aOther),   Is.Zero);
                Assert.That(aIN.CompareLexicographically(aIN),      Is.Zero);

            });

        }

        #endregion

        #region CompareLexicographically_ComparesRDataAsUnsignedBytesAndAPrefixIsLesser()

        [Test]
        public void CompareLexicographically_ComparesRDataAsUnsignedBytesAndAPrefixIsLesser()
        {

            var a7     = ARecord("10.0.0.7");
            var a8     = ARecord("10.0.0.8");
            var a100   = ARecord("10.0.0.100");
            var a200   = ARecord("10.0.0.200");
            var a192   = ARecord("192.0.0.1");
            var a10    = ARecord("10.0.0.1");

            var txtA   = TXTRecord("a");
            var txtAB  = TXTRecord("a", "b");
            var txtB   = TXTRecord("b");

            Assert.Multiple(() => {

                Assert.That(a7.  CompareLexicographically(a8),      Is.Negative);
                Assert.That(a8.  CompareLexicographically(a7),      Is.Positive);

                // unsigned: 200 > 100 and 192 > 10, although both would be negative as signed bytes
                Assert.That(a200.CompareLexicographically(a100),    Is.Positive);
                Assert.That(a192.CompareLexicographically(a10),     Is.Positive);

                // [1 'a'] is a prefix of [1 'a' 1 'b'] and therefore the lesser one
                Assert.That(txtA. CompareLexicographically(txtAB),  Is.Negative);
                Assert.That(txtAB.CompareLexicographically(txtA),   Is.Positive);
                Assert.That(txtA. CompareLexicographically(txtB),   Is.Negative);

            });

        }

        #endregion

        #region CompareLexicographically_Sets_APrefixSetIsLesser()

        [Test]
        public void CompareLexicographically_Sets_APrefixSetIsLesser()
        {

            var a7    = ARecord("10.0.0.7");
            var a8    = ARecord("10.0.0.8");
            var aaaa  = AAAARecord();
            var txt   = TXTRecord("txtver=1");

            IDNSResourceRecord[] one     = [ a7 ];
            IDNSResourceRecord[] two     = [ a7, txt ];
            IDNSResourceRecord[] none    = [ ];
            IDNSResourceRecord[] ours    = [ a7, aaaa ];
            IDNSResourceRecord[] theirs  = [ a8 ];

            Assert.Multiple(() => {

                // {A} is a prefix of {A, TXT}
                Assert.That(one. CompareLexicographically(two),      Is.Negative);
                Assert.That(two. CompareLexicographically(one),      Is.Positive);
                Assert.That(one. CompareLexicographically(one),      Is.Zero);

                // the empty set is a prefix of everything
                Assert.That(none.CompareLexicographically(one),      Is.Negative);
                Assert.That(one. CompareLexicographically(none),     Is.Positive);
                Assert.That(none.CompareLexicographically(none),     Is.Zero);

                // the first differing record decides, before the size of the sets does:
                // {A 10.0.0.7, AAAA} < {A 10.0.0.8} although it has more records
                Assert.That(ours.  CompareLexicographically(theirs), Is.Negative);
                Assert.That(theirs.CompareLexicographically(ours),   Is.Positive);

            });

        }

        #endregion

        #region CompareLexicographically_Sets_IsOrderIndependent()

        [Test]
        public void CompareLexicographically_Sets_IsOrderIndependent()
        {

            var a7    = ARecord("10.0.0.7");
            var a8    = ARecord("10.0.0.8");
            var aaaa  = AAAARecord();
            var txt   = TXTRecord("txtver=1");

            IDNSResourceRecord[] set1  = [ txt, aaaa, a7 ];
            IDNSResourceRecord[] set2  = [ a7, txt, aaaa ];
            IDNSResourceRecord[] set3  = [ aaaa, a8, txt ];

            Assert.Multiple(() => {

                // the same records in a different order are the same set
                Assert.That(set1.CompareLexicographically(set2),   Is.Zero);
                Assert.That(set2.CompareLexicographically(set1),   Is.Zero);

                // both sets are sorted before they are compared: A 10.0.0.7 < A 10.0.0.8 decides,
                // wherever the A records happen to be within the given sequences
                Assert.That(set1.CompareLexicographically(set3),   Is.Negative);
                Assert.That(set3.CompareLexicographically(set1),   Is.Positive);
                Assert.That(set2.CompareLexicographically(set3),   Is.Negative);

            });

        }

        #endregion

        #endregion

        #region Keys

        #region RecordKey_IgnoresCaseAndTimeToLive()

        [Test]
        public void RecordKey_IgnoresCaseAndTimeToLive()
        {

            var a7       = ARecord("10.0.0.7", "MyHost.Local.", 120);
            var a7Later  = ARecord("10.0.0.7", "myhost.local.", 60);
            var a7Flush  = ARecord("10.0.0.7", "myhost.local.", 60, (DNSQueryClasses) 0x8001);
            var a8       = ARecord("10.0.0.8");
            var aaaa     = AAAARecord();
            var other    = ARecord("10.0.0.7", "other.local.");

            Assert.Multiple(() => {

                Assert.That(a7.RecordKey(),   Is.EqualTo(a7Later.RecordKey()), "case and time-to-live are not part of the key");
                Assert.That(a7.RecordKey(),   Is.EqualTo(a7Flush.RecordKey()), "the cache-flush bit is not part of the key");
                Assert.That(a7.RecordKey(),   Is.EqualTo("myhost.local.|1|1|0A000007"));

                Assert.That(a7.RecordKey(),   Is.Not.EqualTo(a8.   RecordKey()), "the RDATA is");
                Assert.That(a7.RecordKey(),   Is.Not.EqualTo(aaaa. RecordKey()), "the type is");
                Assert.That(a7.RecordKey(),   Is.Not.EqualTo(other.RecordKey()), "the owner name is");

            });

        }

        #endregion

        #region RRSetKey_StaticAndInstanceOverloadsAgree()

        [Test]
        public void RRSetKey_StaticAndInstanceOverloadsAgree()
        {

            var a7    = ARecord("10.0.0.7", "MyHost.Local.");
            var a8    = ARecord("10.0.0.8", "myhost.local.", 60);
            var txt   = TXTRecord("txtver=1");

            Assert.Multiple(() => {

                Assert.That(a7.RRSetKey(),   Is.EqualTo(MulticastDNSRecordExtensions.RRSetKey(DNSServiceName.Parse("MYHOST.LOCAL."), DNSResourceRecordTypes.A, DNSQueryClasses.IN)));
                Assert.That(a7.RRSetKey(),   Is.EqualTo("myhost.local.|1|1"));

                // every record of a set has the same key ...
                Assert.That(a7.RRSetKey(),   Is.EqualTo(a8.RRSetKey()));

                // ... records of another set do not
                Assert.That(a7.RRSetKey(),   Is.Not.EqualTo(txt.RRSetKey()));
                Assert.That(txt.RRSetKey(),  Is.EqualTo("myhost.local.|16|1"));

                // the cache-flush bit is masked in the static overload as well
                Assert.That(MulticastDNSRecordExtensions.RRSetKey(DNSServiceName.Parse("myhost.local."), DNSResourceRecordTypes.A, (DNSQueryClasses) 0x8001),
                            Is.EqualTo(a7.RRSetKey()));

                // the record key extends the set key by the RDATA
                Assert.That(a7.RecordKey(),  Does.StartWith(a7.RRSetKey() + "|"));

            });

        }

        #endregion

        #endregion

    }

}
