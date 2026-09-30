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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IP
{

    /// <summary>
    /// IPv6Address tests.
    /// </summary>
    [TestFixture]
    public class IPv6AddressTests
    {

        #region ParseIPv6String_001()

        /// <summary>
        /// IPv6Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv6String_001()
        {

            var ipv6Address = IPv6Address.Parse("2001:db8::1");

            Assert.That(ipv6Address.ToString(), Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:0001"));

        }

        #endregion

        #region ParseIPv6String_002()

        /// <summary>
        /// IPv6Address string parsing test for bracketed addresses.
        /// </summary>
        [Test]
        public void ParseIPv6String_002()
        {

            var ipv6Address = IPv6Address.Parse("[2001:db8::2]");

            Assert.That(ipv6Address.ToString(), Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:0002"));

        }

        #endregion

        #region ParseIPv6String_003()

        /// <summary>
        /// IPv6Address string parsing test for scoped addresses.
        /// </summary>
        [Test]
        public void ParseIPv6String_003()
        {

            var ipv6Address = IPv6Address.Parse("[fe80::1%eth0]");

            Assert.That(ipv6Address.InterfaceId, Is.EqualTo("eth0"));
            Assert.That(ipv6Address.ToString(),  Is.EqualTo("fe80:0000:0000:0000:0000:0000:0000:0001%eth0"));

        }

        #endregion

        #region ParseIPv6String_004()

        /// <summary>
        /// IPv6Address string parsing test for IPv4-mapped notation.
        /// </summary>
        [Test]
        public void ParseIPv6String_004()
        {

            var ipv6Address = IPv6Address.Parse("::ffff:192.0.2.128");

            Assert.That(ipv6Address.ToString(),     Is.EqualTo("0000:0000:0000:0000:0000:ffff:c000:0280"));
            Assert.That(ipv6Address.IsMappedIPv4,   Is.True);
            Assert.That(ipv6Address.MappedIPv4?.ToString(), Is.EqualTo("192.0.2.128"));

        }

        #endregion

        #region TryParseIPv6String_RejectsMalformedInput()

        /// <summary>
        /// IPv6Address string parsing should reject malformed input.
        /// </summary>
        [Test]
        public void TryParseIPv6String_RejectsMalformedInput()
        {

            Assert.That(IPv6Address.TryParse("",              out _), Is.False);
            Assert.That(IPv6Address.TryParse("192.0.2.1",     out _), Is.False);
            Assert.That(IPv6Address.TryParse("2001::db8::1",  out _), Is.False);
            Assert.That(IPv6Address.TryParse("2001:::1",      out _), Is.False);
            Assert.That(IPv6Address.TryParse("[::1",          out _), Is.False);
            Assert.That(IPv6Address.TryParse("::1]",          out _), Is.False);
            Assert.That(IPv6Address.TryParse("fe80::1%",      out _), Is.False);
            Assert.That(IPv6Address.TryParse("gggg::1",       out _), Is.False);
            Assert.That(IPv6Address.TryParse("1:2:3:4:5:6:7:8:9", out _), Is.False);

            Assert.Throws<ArgumentException>(() => IPv6Address.Parse("2001::db8::1"));

        }

        #endregion

        #region IPv6AddressProperties()

        /// <summary>
        /// Checks common IPv6 address properties.
        /// </summary>
        [Test]
        public void IPv6AddressProperties()
        {

            Assert.That(IPv6Address.Any.      IsAny,       Is.True);
            Assert.That(IPv6Address.Any.      ToString(),  Is.EqualTo("[::]"));
            Assert.That(IPv6Address.Localhost.IsLocalhost, Is.True);
            Assert.That(IPv6Address.Localhost.ToString(),  Is.EqualTo("[::1]"));

            Assert.That(IPv6Address.Parse("ff02::1").IsMulticast, Is.True);
            Assert.That(IPv6Address.Parse("2001:db8::1").IsMulticast, Is.False);

        }

        #endregion

        #region IPv6AddressByteOperations()

        /// <summary>
        /// Checks IPv6Address byte conversion helpers.
        /// </summary>
        [Test]
        public void IPv6AddressByteOperations()
        {

            var bytes       = new Byte[] { 0x20, 0x01, 0x0D, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            var ipv6Address = new IPv6Address(bytes);

            Assert.That(ipv6Address.GetBytes(), Is.EqualTo(bytes));

            var destination = new Byte[16];
            ipv6Address.CopyTo(destination);
            Assert.That(destination, Is.EqualTo(bytes));

            Assert.Throws<FormatException>(() => new IPv6Address(new Byte[15]));
            Assert.Throws<ArgumentException>(() => ipv6Address.CopyTo(new Byte[15]));

        }

        #endregion

        #region IPv6AddressFromIPAddress()

        /// <summary>
        /// Checks conversion to and from System.Net.IPAddress.
        /// </summary>
        [Test]
        public void IPv6AddressFromIPAddress()
        {

            var systemIPAddress = System.Net.IPAddress.Parse("2001:db8::1234");
            var ipv6Address     = IPv6Address.From(systemIPAddress);

            Assert.That(ipv6Address.ToString(), Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:1234"));

            System.Net.IPAddress roundtrip = ipv6Address;
            Assert.That(roundtrip.ToString(), Is.EqualTo("2001:db8::1234"));

            Assert.Throws<FormatException>(() => IPv6Address.From(System.Net.IPAddress.Loopback));

        }

        #endregion

        #region ScopedIPv6AddressConversionsPreserveScopeId()

        [Test]
        public void ScopedIPv6AddressConversionsPreserveScopeId()
        {

            var addressBytes       = System.Net.IPAddress.Parse("fe80::1234").GetAddressBytes();
            var systemIPAddress    = new System.Net.IPAddress(addressBytes, 17);
            var ipv6Address        = IPv6Address.From(systemIPAddress);
            var genericIPAddress   = IPAddress.FromDotNet(systemIPAddress);
            var builtIPAddress     = IPAddress.Build(systemIPAddress);

            System.Net.IPAddress implicitRoundtrip = ipv6Address;

            Assert.Multiple(() => {
                Assert.That(ipv6Address.InterfaceId,                       Is.EqualTo("17"));
                Assert.That(implicitRoundtrip.ScopeId,                     Is.EqualTo(17));
                Assert.That(((IPv6Address) genericIPAddress).InterfaceId,  Is.EqualTo("17"));
                Assert.That(genericIPAddress.ToDotNet().ScopeId,           Is.EqualTo(17));
                Assert.That(((IPv6Address) builtIPAddress).InterfaceId,    Is.EqualTo("17"));
                Assert.That(builtIPAddress.ToDotNet().ScopeId,             Is.EqualTo(17));
            });

        }

        #endregion

        #region UnknownNamedIPv6ScopeIsRejectedByDotNetConversion()

        [Test]
        public void UnknownNamedIPv6ScopeIsRejectedByDotNetConversion()
        {

            var ipv6Address = IPv6Address.Parse("fe80::1234%__hermod_missing_interface__");

            Assert.Throws<ArgumentException>(() => ipv6Address.ToDotNet());

        }

        #endregion

        #region IPv6AddressFromIPv4()

        /// <summary>
        /// Checks IPv4 mapped IPv6 address creation.
        /// </summary>
        [Test]
        public void IPv6AddressFromIPv4()
        {

            var ipv6Address = IPv6Address.FromIPv4(IPv4Address.Parse("192.0.2.128"));

            Assert.That(ipv6Address.ToString(), Is.EqualTo("0000:0000:0000:0000:0000:ffff:c000:0280"));
            Assert.That(ipv6Address.MappedIPv4?.ToString(), Is.EqualTo("192.0.2.128"));

        }

        #endregion

        #region IPv6AddressesAreEqual()

        /// <summary>
        /// Checks if two IPv6Address are equal.
        /// </summary>
        [Test]
        public void IPv6AddressesAreEqual()
        {

            var a = IPv6Address.Parse("2001:db8::1");
            var b = IPv6Address.Parse("2001:0db8:0000:0000:0000:0000:0000:0001");

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a == b,     Is.True);
            Assert.That(a != b,     Is.False);
            Assert.That(b,          Is.EqualTo(a));

        }

        #endregion

        #region CompareIPv6Addresses()

        /// <summary>
        /// Compares two IPv6Addresses.
        /// </summary>
        [Test]
        public void CompareIPv6Addresses()
        {

            var a = IPv6Address.Parse("2001:db8::2");
            var b = IPv6Address.Parse("2001:db8::1");

            Assert.That(a.CompareTo(b) > 0, Is.True);
            Assert.That(a > b,              Is.True);
            Assert.That(b < a,              Is.True);

        }

        #endregion

        #region ToIPLiteral_BracketsWhatIsNotBracketedYet()

        /// <summary>
        /// The authority of an URL carries an IPv6 address in brackets - RFC 3986
        /// section 3.2.2 - so that the colon before a port stays distinguishable from
        /// the colons inside the address.
        ///
        /// ToString() already brackets "::" and "::1" and spells every other address
        /// out bare, which is the trap this helper exists for: bracketing every
        /// address turned the loopback into "[[::1]]", and bracketing none of them
        /// left a global address unusable as a host.
        /// </summary>
        [Test]
        public void ToIPLiteral_BracketsWhatIsNotBracketedYet()
        {

            // Spelled out bare by ToString(), so the brackets are added...
            Assert.That(IPv6Address.Parse("2606:2800:220:1:248:1893:25c8:1946").ToIPLiteral(),
                        Is.EqualTo("[2606:2800:0220:0001:0248:1893:25c8:1946]"));

            // ...and the two ToString() brackets itself do not get a second pair.
            Assert.That(IPv6Address.Localhost.ToIPLiteral(), Is.EqualTo("[::1]"));
            Assert.That(IPv6Address.Any.      ToIPLiteral(), Is.EqualTo("[::]"));

            // An IPv4 address is not an IP-literal and stays as it is.
            Assert.That(IPv4Address.Parse("10.0.0.1").ToIPLiteral(), Is.EqualTo("10.0.0.1"));

            // Whatever comes out is bracketed exactly once.
            foreach (var address in new IIPAddress[] {
                                        IPv6Address.Parse("2606:2800:220:1:248:1893:25c8:1946"),
                                        IPv6Address.Localhost,
                                        IPv6Address.Any
                                    })
            {
                var literal = address.ToIPLiteral();
                Assert.That(literal, Does.StartWith("["));
                Assert.That(literal, Does.EndWith("]"));
                Assert.That(literal.StartsWith("[["), Is.False, literal);
            }

        }

        #endregion


        #region ToString_Long_SpellsOutEveryGroup(Text, Expected)

        /// <summary>
        /// The long form spells out all eight groups with all four of their
        /// digits, in lower case - "::1" and "::" included, which ToString()
        /// writes as "[::1]" and "[::]" - and keeps the interface.
        /// </summary>
        [TestCase("2001:db8::1",         "2001:0db8:0000:0000:0000:0000:0000:0001")]
        [TestCase("::1",                 "0000:0000:0000:0000:0000:0000:0000:0001")]
        [TestCase("::",                  "0000:0000:0000:0000:0000:0000:0000:0000")]
        [TestCase("2001:DB8::ABCD",      "2001:0db8:0000:0000:0000:0000:0000:abcd")]
        [TestCase("::ffff:192.0.2.128",  "0000:0000:0000:0000:0000:ffff:c000:0280")]
        [TestCase("fe80::1%eth0",        "fe80:0000:0000:0000:0000:0000:0000:0001%eth0")]
        public void ToString_Long_SpellsOutEveryGroup(String Text, String Expected)
        {

            Assert.That(IPv6Address.Parse(Text).ToString(IPv6Format.Long), Is.EqualTo(Expected));

        }

        #endregion

        #region ToString_Short_IsTheTextOfRFC5952(Text, Expected)

        /// <summary>
        /// The short form is the text RFC 5952 recommends, section by section.
        /// </summary>
        [TestCase("2001:0db8:0000:0000:0000:0000:0000:0001",  "2001:db8::1",           Description = "4.1: no leading zeros")]
        [TestCase("2001:db8:0:0:0:0:2:1",                     "2001:db8::2:1",         Description = "4.2.1: :: as far as it goes")]
        [TestCase("2001:db8:0:1:1:1:1:1",                     "2001:db8:0:1:1:1:1:1",  Description = "4.2.2: not for a single zero group")]
        [TestCase("2001:0:0:1:0:0:0:1",                       "2001:0:0:1::1",         Description = "4.2.3: the longest run")]
        [TestCase("2001:db8:0:0:1:0:0:1",                     "2001:db8::1:0:0:1",     Description = "4.2.3: the first of two equally long runs")]
        [TestCase("0:0:1:0:0:0:0:0",                          "0:0:1::",               Description = "4.2.3: a longer run at the end")]
        [TestCase("2001:DB8::ABCD",                           "2001:db8::abcd",        Description = "4.3: lower case")]
        [TestCase("::ffff:192.0.2.128",                       "::ffff:192.0.2.128",    Description = "5: an IPv4-mapped address in dotted decimal")]
        [TestCase("::",                                       "::",                    Description = "the unspecified address, bare")]
        [TestCase("::1",                                      "::1",                   Description = "the loopback, bare")]
        [TestCase("1::",                                      "1::",                   Description = "a run that ends the address")]
        [TestCase("fe80::1%eth0",                             "fe80::1%eth0",          Description = "an address with its interface")]
        [TestCase("2001:4860:4860:0:0:0:0:8888",              "2001:4860:4860::8888",  Description = "a name server a station showed in full")]
        public void ToString_Short_IsTheTextOfRFC5952(String Text, String Expected)
        {

            Assert.That(IPv6Address.Parse(Text).ToString(IPv6Format.Short), Is.EqualTo(Expected));

        }

        #endregion

        #region ToString_Short_WritesWhatDotNetWrites()

        /// <summary>
        /// .NET's own IPAddress writes RFC 5952's text too, and is a second
        /// opinion on thousands of addresses with runs of zeros in every place.
        /// Not where the first four groups are all zero: there .NET writes an
        /// IPv4-compatible and an IPv4-translated address in dotted decimal as
        /// well, and this only an IPv4-mapped one.
        /// </summary>
        [Test]
        public void ToString_Short_WritesWhatDotNetWrites()
        {

            var random   = new Random(5952);
            var bytes    = new Byte[16];
            var compared = 0;

            while (compared < 5000)
            {

                for (var group = 0; group < 8; group++)
                {
                    var value = random.Next(2) == 0 ? 0 : random.Next(1, 0x10000);
                    bytes[2 * group]     = (Byte) (value >> 8);
                    bytes[2 * group + 1] = (Byte)  value;
                }

                if (bytes.Take(8).All(one => one == 0))
                    continue;

                Assert.That(new IPv6Address(bytes).ToString(IPv6Format.Short),
                            Is.EqualTo(new System.Net.IPAddress(bytes).ToString()));

                compared++;

            }

        }

        #endregion

        #region ToString_ReadsBackAsTheSameAddress(Format)

        /// <summary>
        /// Either form is read back as the address it was written from, with
        /// its interface.
        /// </summary>
        [Test]
        public void ToString_ReadsBackAsTheSameAddress([Values] IPv6Format Format)
        {

            foreach (var text in new[] { "2001:db8::1", "::", "::1", "1::", "2001:0:0:1:0:0:0:1", "::ffff:192.0.2.128",
                                         "fe80::1%eth0", "2001:db8:0:1:1:1:1:1", "ff02::1:ff00:1" })
            {

                var address  = IPv6Address.Parse(text);
                var written  = address.ToString(Format);
                var read     = IPv6Address.Parse(written);

                Assert.That(read,              Is.EqualTo(address),              written);
                Assert.That(read.InterfaceId,  Is.EqualTo(address.InterfaceId),  written);

            }

        }

        #endregion

        #region ToString_AsAnyIPAddress_TakesTheFormat()

        /// <summary>
        /// Asked as an IIPAddress, an IPv6 address writes the format it is
        /// asked for, and an IPv4 address writes the one text it has.
        /// </summary>
        [Test]
        public void ToString_AsAnyIPAddress_TakesTheFormat()
        {

            IIPAddress ipv6 = IPv6Address.Parse("2001:db8::1");
            IIPAddress ipv4 = IPv4Address.Parse("10.0.0.1");

            Assert.Multiple(() => {
                Assert.That(ipv6.ToString(IPv6Format.Short),  Is.EqualTo("2001:db8::1"));
                Assert.That(ipv6.ToString(IPv6Format.Long),   Is.EqualTo("2001:0db8:0000:0000:0000:0000:0000:0001"));
                Assert.That(ipv4.ToString(IPv6Format.Short),  Is.EqualTo("10.0.0.1"));
                Assert.That(ipv4.ToString(IPv6Format.Long),   Is.EqualTo("10.0.0.1"));
            });

        }

        #endregion

        #region ToString_KeepsItsForm()

        /// <summary>
        /// ToString() writes what it always wrote, which is kept elsewhere as a
        /// name or a key: the long form, but "::" and "::1" in brackets.
        /// </summary>
        [Test]
        public void ToString_KeepsItsForm()
        {

            Assert.Multiple(() => {
                Assert.That(IPv6Address.Parse("2001:db8::1").ToString(),   Is.EqualTo(IPv6Address.Parse("2001:db8::1").ToString(IPv6Format.Long)));
                Assert.That(IPv6Address.Parse("fe80::1%eth0").ToString(),  Is.EqualTo("fe80:0000:0000:0000:0000:0000:0000:0001%eth0"));
                Assert.That(IPv6Address.Localhost.ToString(),              Is.EqualTo("[::1]"));
                Assert.That(IPv6Address.Any.      ToString(),              Is.EqualTo("[::]"));
            });

        }

        #endregion

    }

}
