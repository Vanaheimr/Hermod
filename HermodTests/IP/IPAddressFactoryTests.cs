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
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.IP
{

    /// <summary>
    /// IPAddressFactory test.
    /// </summary>
    [TestFixture]
    public class IPAddressTests
    {

        #region ParseIPv4String_001()

        /// <summary>
        /// IPv4Address string parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4String_001()
        {

            var ipv4Address = IPAddress.Parse("141.24.12.2");

            Assert.That(ipv4Address is IIPAddress,  Is.True);
            Assert.That(ipv4Address is IPv4Address, Is.True);
            Assert.That(ipv4Address is IPv6Address, Is.False);

        }

        #endregion

        #region ParseTooShortByteArray()

        /// <summary>
        /// IPAddressFactory byte array parsing test.
        /// </summary>
        [Test]
        public void ParseTooShortByteArray()
        {
            Assert.Throws<ArgumentException>(() => IPAddress.Parse([10, 0, 0]));
        }

        #endregion

        #region ParseIPv4ByteArray()

        /// <summary>
        /// IPv4Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv4ByteArray()
        {

            var ipv4Address = IPAddress.Parse([10, 0, 0, 0]);

            Assert.That(ipv4Address is IIPAddress,  Is.True);
            Assert.That(ipv4Address is IPv4Address, Is.True);
            Assert.That(ipv4Address is IPv6Address, Is.False);

        }

        #endregion

        #region ParseIPv6ByteArray()

        /// <summary>
        /// IPv6Address byte array parsing test.
        /// </summary>
        [Test]
        public void ParseIPv6ByteArray()
        {

            var ipv6Address = IPAddress.Parse([10, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0, 10, 0, 0, 0]);

            Assert.That(ipv6Address is IIPAddress,  Is.True);
            Assert.That(ipv6Address is IPv4Address, Is.False);
            Assert.That(ipv6Address is IPv6Address, Is.True);

        }

        #endregion


        #region TryParse_says_no_to_a_text_that_only_contains_an_address(Text)

        /// <summary>
        /// An address with something around it is not an address, and a
        /// TryParse says so rather than throwing.
        /// </summary>
        /// <remarks>
        /// Every one of these threw: a pattern found an address somewhere in
        /// the text, and IPv4Address.Parse or IPv6Address.Parse then threw on
        /// the rest of it. The first is a name server as a log line names it,
        /// written into a vehicle's configuration file - which stopped the
        /// vehicle at its start with this exception instead of a sentence
        /// about the file.
        /// </remarks>
        [TestCase("udp://213.133.98.98:53")]
        [TestCase("udp://[2a01:4f8:0:1::add:1010]:53")]
        [TestCase("213.133.98.98:53")]
        [TestCase("[2001:db8::1]:53")]
        [TestCase("10.0.0.1.nip.io")]
        [TestCase("999.1.1.1")]
        public void TryParse_says_no_to_a_text_that_only_contains_an_address(String Text)
        {

            var         parsed     = true;
            IIPAddress? ipAddress  = null;

            Assert.That(() => parsed = IPAddress.TryParse(Text, out ipAddress),  Throws.Nothing);

            Assert.Multiple(() => {
                Assert.That(parsed,                  Is.False);
                Assert.That(ipAddress,               Is.Null);
                Assert.That(IPAddress.IsIPv4(Text),  Is.False);
                Assert.That(IPAddress.IsIPv6(Text),  Is.False);
            });

        }

        #endregion

        #region TryParse_still_reads_what_is_an_address(Text, Version)

        /// <summary>
        /// And what is an address is still read as one, in every form the old
        /// pattern let through: with blanks around it, in brackets, in capitals,
        /// with an interface.
        /// </summary>
        [TestCase("141.24.12.2",    4)]
        [TestCase(" 141.24.12.2 ",  4)]
        [TestCase("::1",            6)]
        [TestCase("[::1]",          6)]
        [TestCase("2A01:4F8::1",    6)]
        [TestCase("fe80::1%eth0",   6)]
        public void TryParse_still_reads_what_is_an_address(String Text, Int32 Version)
        {

            Assert.That(IPAddress.TryParse(Text, out var ipAddress),  Is.True);

            Assert.Multiple(() => {

                Assert.That(Version == 4
                                ? ipAddress is IPv4Address
                                : ipAddress is IPv6Address,          Is.True);

                Assert.That(Version == 4
                                ? IPAddress.IsIPv4(Text)
                                : IPAddress.IsIPv6(Text),            Is.True);

            });

        }

        #endregion

        #region Localhost_is_the_address_and_not_a_name_that_begins_with_it()

        /// <summary>
        /// "127.0.0.1.example.com" is a host name somebody else's name server
        /// answers for. It was taken for localhost, because it contains an
        /// address and begins with 127.
        /// </summary>
        [Test]
        public void Localhost_is_the_address_and_not_a_name_that_begins_with_it()
        {

            Assert.Multiple(() => {
                Assert.That(IPAddress.IsIPv4Localhost("127.0.0.1"),              Is.True);
                Assert.That(IPAddress.IsIPv4Localhost("127.0.0.1.example.com"),  Is.False);
                Assert.That(IPAddress.IsIPv6Localhost("::1"),                    Is.True);
                Assert.That(IPAddress.IsLocalhost    ("localhost"),              Is.True);
            });

        }

        #endregion

        #region A_host_names_port_and_a_domain_names_root_are_not_part_of_the_question()

        /// <summary>
        /// The overloads for a host name and a domain name ask about the name:
        /// a port after it, or the root's dot, do not make it any less an
        /// address - and a name with an address inside it is still a name.
        /// </summary>
        [Test]
        public void A_host_names_port_and_a_domain_names_root_are_not_part_of_the_question()
        {

            Assert.Multiple(() => {
                Assert.That(IPAddress.IsIPv4(HTTPHostname.Parse("141.24.12.2:8080")),  Is.True);
                Assert.That(IPAddress.IsIPv4(DomainName.  Parse("141.24.12.2")),       Is.True);
                Assert.That(IPAddress.IsIPv4(DomainName.  Parse("10.0.0.1.nip.io")),   Is.False);
                Assert.That(IPAddress.IsIPv6(HTTPHostname.Parse("[::1]:8080")),        Is.True);
            });

        }

        #endregion

    }

}
