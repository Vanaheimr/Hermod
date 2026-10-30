/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

// The test namespace has an HTTPS of its own.
using HTTPSRecord = org.GraphDefined.Vanaheimr.Hermod.DNS.HTTPS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// The presentation format of SVCB and HTTPS SvcParams (RFC 9460 §2.1, §7,
    /// §8, Appendix A): each value is decoded as a character-string and kept in
    /// its key's wire format, and written back in that key's own presentation.
    /// </summary>
    [TestFixture]
    public class SVCB_Presentation_Tests
    {

        #region Data

        private static readonly DomainName  Name  = DomainName.Parse("example.com.");
        private static readonly TimeSpan    TTL   = TimeSpan.FromSeconds(3600);

        private static IEnumerable<SVCParameter>? Read(String Type, String RData)

            => Type == "SVCB"
                   ? SVCB. TryParseFromJSON(Name, TTL, RData)?.SVCParameters
                   : HTTPSRecord.TryParseFromJSON(Name, TTL, RData)?.SVCParameters;

        private static String Write(String Type, params SVCParameter[] Parameters)

            => Type == "SVCB"
                   ? new SVCB (Name, DNSQueryClasses.IN, TTL, 1, DomainName.Parse("svc.example."), Parameters).ToZoneFileString()
                   : new HTTPSRecord(Name, DNSQueryClasses.IN, TTL, 1, DomainName.Parse("svc.example."), Parameters).ToZoneFileString();

        private static Byte[] Hex(String Hex)
            => Convert.FromHexString(Hex);

        /// <summary>
        /// The RDATA of a zone-file line: everything after owner, TTL, class and type.
        /// </summary>
        private static String RDataOf(String ZoneFileLine)
            => String.Join(" ", ZoneFileLine.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[4..]);

        #endregion


        #region Reading: RFC 9460 Appendix D.2

        [TestCase("SVCB",  "16 foo.example.com. port=53",                                    (UInt16) 3, "0035")]
        [TestCase("HTTPS", "16 foo.example.com. port=53",                                    (UInt16) 3, "0035")]
        [TestCase("SVCB",  "1 foo.example.com. key667=hello",                                (UInt16) 667, "68656c6c6f")]
        [TestCase("SVCB",  "1 foo.example.com. key667=\"hello\\210qoo\"",                    (UInt16) 667, "68656c6c6fd2716f6f")]
        [TestCase("HTTPS", "1 foo.example.com. key667=\"hello\\210qoo\"",                    (UInt16) 667, "68656c6c6fd2716f6f")]
        [TestCase("SVCB",  "1 foo.example.com. ipv6hint=\"2001:db8::1,2001:db8::53:1\"",     (UInt16) 6, "20010db800000000000000000000000120010db8000000000000000000530001")]
        [TestCase("SVCB",  "1 example.com. ipv6hint=\"2001:db8:122:344::192.0.2.33\"",       (UInt16) 6, "20010db80122034400000000c0000221")]
        [TestCase("SVCB",  "16 foo.example.org. alpn=\"f\\\\\\\\oo\\\\,bar,h2\"",            (UInt16) 1, "08665c6f6f2c626172026832")]
        [TestCase("SVCB",  "16 foo.example.org. alpn=f\\\\\\092oo\\092,bar,h2",              (UInt16) 1, "08665c6f6f2c626172026832")]
        [TestCase("HTTPS", "16 foo.example.org. alpn=f\\\\\\092oo\\092,bar,h2",              (UInt16) 1, "08665c6f6f2c626172026832")]
        public void A_Test_Vector_Reads_As_Its_Wire_Format(String Type, String RData, UInt16 Key, String Wire)
        {

            var parameters = Read(Type, RData);

            Assert.That(parameters,                                Is.Not.Null, "the reader refused the RFC's own vector");
            Assert.That(parameters!.Single(p => p.Key == Key).Value, Is.EqualTo(Hex(Wire)));

        }


        [Test]
        public void Mandatory_Alpn_And_Ipv4hint_Read_As_Figure_9_Has_Them()
        {

            var parameters = Read("SVCB", "16 foo.example.org. alpn=h2,h3-19 mandatory=ipv4hint,alpn ipv4hint=192.0.2.1")!.
                                 ToDictionary(p => p.Key, p => p.Value);

            Assert.Multiple(() => {
                Assert.That(parameters[0], Is.EqualTo(Hex("00010004")),          "mandatory: the keys as numbers, in increasing order");
                Assert.That(parameters[1], Is.EqualTo(Hex("0268320568332d3139")), "alpn: each alpn-id prefixed by its length");
                Assert.That(parameters[4], Is.EqualTo(Hex("c0000201")),          "ipv4hint: the address in network byte order");
            });

        }

        #endregion

        #region Reading: escapes (RFC 1035 §5.1)

        [TestCase("SVCB")]
        [TestCase("HTTPS")]
        public void An_Escaped_Quote_And_A_Decimal_Escape_Are_One_Octet_Each(String Type)
        {

            Assert.Multiple(() => {
                Assert.That(Read(Type, "1 . key65280=\"a\\\"b\"")!.Single().Value,    Is.EqualTo("a\"b"u8.ToArray()));
                Assert.That(Read(Type, "1 . key65280=\\000a\\092\\255")!.Single().Value, Is.EqualTo(Hex("00615cff")));
                Assert.That(Read(Type, "1 . key65280=\"a\\\" port=8080\"")!.Select(p => p.Key),
                            Is.EqualTo(new UInt16[] { 65280 }),
                            "an escaped quote does not end the value");
            });

        }

        #endregion

        #region Reading: RFC 9460 Appendix D.3, and values that are not valid for their key

        [TestCase("1 foo.example.com. mandatory")]
        [TestCase("1 foo.example.com. alpn")]
        [TestCase("1 foo.example.com. port")]
        [TestCase("1 foo.example.com. ipv4hint")]
        [TestCase("1 foo.example.com. ipv6hint")]
        [TestCase("1 foo.example.com. no-default-alpn=abc")]
        [TestCase("1 foo.example.com. mandatory=mandatory")]
        [TestCase("1 foo.example.com. mandatory=key123,key123 key123=abc")]
        [TestCase("1 foo.example.com. port=65536")]
        [TestCase("1 foo.example.com. port=8o")]
        [TestCase("1 foo.example.com. ipv4hint=192.0.2")]
        [TestCase("1 foo.example.com. ipv4hint=2001:db8::1")]
        [TestCase("1 foo.example.com. ipv6hint=192.0.2.1")]
        [TestCase("1 foo.example.com. alpn=h2,,h3")]
        [TestCase("1 foo.example.com. key667=\"unterminated")]
        [TestCase("1 foo.example.com. key667=\\25")]
        [TestCase("1 foo.example.com. key667=\\256")]
        public void A_Value_That_Is_Not_Valid_For_Its_Key_Refuses_The_Record(String RData)
        {

            Assert.Multiple(() => {
                Assert.That(Read("SVCB",  RData), Is.Null);
                Assert.That(Read("HTTPS", RData), Is.Null);
            });

        }

        #endregion

        #region Writing

        [TestCase("SVCB")]
        [TestCase("HTTPS")]
        public void Each_Key_Is_Written_In_Its_Own_Presentation(String Type)
        {

            var written = Write(Type,
                                new SVCParameter(0, Hex("00010003")),
                                new SVCParameter(1, Hex("026832")),
                                new SVCParameter(2, []),
                                new SVCParameter(3, Hex("01bb")),
                                new SVCParameter(4, Hex("c0000201c0000202")),
                                new SVCParameter(6, Hex("20010db8000000000000000000000001")));

            Assert.That(written, Does.Contain(" mandatory=alpn,port alpn=\"h2\" no-default-alpn port=443 ipv4hint=192.0.2.1,192.0.2.2 ipv6hint=2001:db8::1"));

        }


        [TestCase("SVCB")]
        [TestCase("HTTPS")]
        public void An_Alpn_Id_With_A_Comma_Or_A_Backslash_Is_Escaped_For_The_List(String Type)
        {

            // Figure 10's value: the alpn-ids "f\oo,bar" and "h2".
            var written = Write(Type, new SVCParameter(1, Hex("08665c6f6f2c626172026832")));
            var reread  = Read (Type, RDataOf(written))!;

            Assert.That(reread.Single().Value, Is.EqualTo(Hex("08665c6f6f2c626172026832")), written);

        }


        [TestCase("SVCB",  (UInt16) 3, "003500")]
        [TestCase("SVCB",  (UInt16) 1, "0368")]
        [TestCase("SVCB",  (UInt16) 0, "00030001")]
        [TestCase("SVCB",  (UInt16) 4, "c00002")]
        [TestCase("HTTPS", (UInt16) 6, "20010db8")]
        [TestCase("HTTPS", (UInt16) 2, "00")]
        public void Octets_Not_Valid_For_Their_Key_Are_Written_In_The_Unknown_Key_Form_And_Read_Back(String Type, UInt16 Key, String Wire)
        {

            // RFC 9460 §2.1: "Arbitrary keys can be represented using the
            // unknown-key presentation format". Nothing is lost.
            var written = Write(Type, new SVCParameter(Key, Hex(Wire)));
            var reread  = Read (Type, RDataOf(written))!;

            Assert.Multiple(() => {
                Assert.That(written,               Does.Contain($" key{Key}=\""));
                Assert.That(reread.Single().Key,   Is.EqualTo(Key));
                Assert.That(reread.Single().Value, Is.EqualTo(Hex(Wire)));
            });

        }

        #endregion

    }

}
