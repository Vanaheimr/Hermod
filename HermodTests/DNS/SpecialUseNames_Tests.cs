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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// "localhost." and every name below it are the loopback addresses, and "invalid." and every name
    /// below it are NXDOMAIN, answered by the library without a name server (RFC 6761 §6.3, §6.4).
    /// </summary>
    /// <remarks>
    /// The client here has no server at all and no cache: whatever it answers, it answered itself. A name
    /// that is not special gets what a client without a server gives every name, a failure.
    /// </remarks>
    [TestFixture]
    public class SpecialUseNames_Tests
    {

        private static DNSClient WithoutServers()

            => new (ManualDNSServers:         [],
                    SearchForIPv4DNSServers:  false,
                    SearchForIPv6DNSServers:  false,
                    UseQueryCache:            false);


        #region ALocalhostName_IsTheLoopback(Name)

        [TestCase("localhost.")]
        [TestCase("www.localhost.")]
        [TestCase("a.b.localhost.")]
        [TestCase("App.LocalHost.")]
        public async Task ALocalhostName_IsTheLoopback(String Name)
        {

            using var client = WithoutServers();

            var a     = await client.Query(DNSServiceName.Parse(Name), [ DNSResourceRecordTypes.A    ]);
            var aaaa  = await client.Query(DNSServiceName.Parse(Name), [ DNSResourceRecordTypes.AAAA ]);
            var both  = await client.Query(DNSServiceName.Parse(Name), [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ]);

            Assert.Multiple(() => {

                Assert.That(a.   Answers.OfType<A>().   Select(record => record.IPv4Address).ToArray(), Is.EqualTo(new[] { IPv4Address.Localhost }));
                Assert.That(aaaa.Answers.OfType<AAAA>().Select(record => record.IPv6Address).ToArray(), Is.EqualTo(new[] { IPv6Address.Localhost }));
                Assert.That(both.Answers.Count(),                                                       Is.EqualTo(2));

                Assert.That(a.ResponseCode,         Is.EqualTo(DNSResponseCodes.NoError));
                Assert.That(a.IsValid,              Is.True);
                Assert.That(a.AuthoritativeAnswer,  Is.True, "the answer is the specification's");
                Assert.That(a.Answers.Single().DomainName.FullName.TrimEnd('.'), Is.EqualTo(Name.TrimEnd('.')).IgnoreCase, "the name that was asked");

            });

        }

        #endregion

        #region AnotherTypeOfALocalhostName_IsNoData(Type)

        [TestCase(DNSResourceRecordTypes.MX)]
        [TestCase(DNSResourceRecordTypes.TXT)]
        [TestCase(DNSResourceRecordTypes.SRV)]
        public async Task AnotherTypeOfALocalhostName_IsNoData(DNSResourceRecordTypes Type)
        {

            using var client = WithoutServers();

            var answer = await client.Query(DNSServiceName.Parse("www.localhost."), [ Type ]);

            Assert.Multiple(() => {
                Assert.That(answer.Answers,       Is.Empty);
                Assert.That(answer.ResponseCode,  Is.EqualTo(DNSResponseCodes.NoError), "NODATA: the name has addresses");
                Assert.That(answer.IsValid,       Is.True);
            });

        }

        #endregion

        #region AnInvalidName_IsNXDomain(Name)

        [TestCase("invalid.",       DNSResourceRecordTypes.A)]
        [TestCase("host.invalid.",  DNSResourceRecordTypes.AAAA)]
        [TestCase("a.b.INVALID.",   DNSResourceRecordTypes.MX)]
        public async Task AnInvalidName_IsNXDomain(String Name, DNSResourceRecordTypes Type)
        {

            using var client = WithoutServers();

            var answer = await client.Query(DNSServiceName.Parse(Name), [ Type ]);

            Assert.Multiple(() => {
                Assert.That(answer.Answers,       Is.Empty);
                Assert.That(answer.ResponseCode,  Is.EqualTo(DNSResponseCodes.NameError));
                Assert.That(answer.IsValid,       Is.True);
            });

        }

        #endregion

        #region AnOnionName_IsNXDomain(Name)

        /// <summary>
        /// RFC 7686 §2, item 3: "Resolvers MUST either respond to requests for .onion names by resolving them
        /// according to [tor-rendezvous] or by responding with NXDOMAIN".
        /// </summary>
        [TestCase("onion.",                    DNSResourceRecordTypes.A)]
        [TestCase("hiddenservice.onion.",      DNSResourceRecordTypes.AAAA)]
        [TestCase("www.HiddenService.ONION.",  DNSResourceRecordTypes.TXT)]
        public async Task AnOnionName_IsNXDomain(String Name, DNSResourceRecordTypes Type)
        {

            using var client = WithoutServers();

            var answer = await client.Query(DNSServiceName.Parse(Name), [ Type ]);

            Assert.Multiple(() => {
                Assert.That(SpecialUseNames.IsOnion(DNSServiceName.Parse(Name)),  Is.True);
                Assert.That(answer.Answers,                                        Is.Empty);
                Assert.That(answer.ResponseCode,                                   Is.EqualTo(DNSResponseCodes.NameError));
                Assert.That(answer.IsValid,                                        Is.True);
            });

        }

        #endregion

        #region ALinkLocalName_IsNXDomain_UnlessItIsToBeSent(Name)

        /// <summary>
        /// RFC 6762 §22.1, item 3: libraries "SHOULD NOT send queries for these names to their configured
        /// (unicast) caching DNS server(s)". A client without servers answers NXDOMAIN for them; told to send
        /// them, it asks the servers it has none of.
        /// </summary>
        [TestCase("local.")]
        [TestCase("printer.LOCAL.")]
        [TestCase("_ipp._tcp.local.")]
        [TestCase("4.3.254.169.in-addr.arpa.")]
        [TestCase("1.8.e.f.ip6.arpa.")]
        [TestCase("0.9.E.F.ip6.arpa.")]
        [TestCase("a.e.f.ip6.arpa.")]
        [TestCase("f.b.e.f.IP6.ARPA.")]
        public async Task ALinkLocalName_IsNXDomain_UnlessItIsToBeSent(String Name)
        {

            using var client = WithoutServers();

            var answer = await client.Query(DNSServiceName.Parse(Name), [ DNSResourceRecordTypes.A ]);

            client.SendLinkLocalNamesToServers = true;

            var sent   = await client.Query(DNSServiceName.Parse(Name), [ DNSResourceRecordTypes.A ]);

            Assert.Multiple(() => {
                Assert.That(SpecialUseNames.IsLinkLocal(DNSServiceName.Parse(Name)),  Is.True);
                Assert.That(answer.ResponseCode,                                      Is.EqualTo(DNSResponseCodes.NameError));
                Assert.That(answer.Answers,                                           Is.Empty);
                Assert.That(sent.ResponseCode,                                        Is.EqualTo(DNSResponseCodes.ServerFailure), "asked of servers there are none of");
            });

        }

        #endregion

        #region ANameBesideTheLinkLocalOnes_IsNotLinkLocal(Name)

        /// <summary>
        /// Label by label: a longer label that ends in the same digits is another label.
        /// </summary>
        [TestCase("local.example.")]
        [TestCase("notlocal.")]
        [TestCase("254.169.in-addr.arpa.example.")]
        [TestCase("169.in-addr.arpa.")]
        [TestCase("255.169.in-addr.arpa.")]
        [TestCase("1.1254.169.in-addr.arpa.")]
        [TestCase("254.168.in-addr.arpa.")]
        [TestCase("e.f.ip6.arpa.")]
        [TestCase("7.e.f.ip6.arpa.")]
        [TestCase("c.e.f.ip6.arpa.")]
        [TestCase("18.e.f.ip6.arpa.")]
        [TestCase("8.e.e.ip6.arpa.")]
        public void ANameBesideTheLinkLocalOnes_IsNotLinkLocal(String Name)
        {
            Assert.That(SpecialUseNames.IsLinkLocal(DNSServiceName.Parse(Name)), Is.False);
        }

        #endregion

        #region ANameThatIsNotBelowEither_IsNotSpecial(Name)

        /// <summary>
        /// The last label decides, and the whole of it: a name that ends in the text, or holds the label
        /// further up, is an ordinary name - which a client without servers cannot answer. And a .alt name is
        /// an ordinary name to a DNS library (RFC 9476 §3.2, item 3).
        /// </summary>
        [TestCase("notlocalhost.")]
        [TestCase("localhost.example.")]
        [TestCase("notinvalid.")]
        [TestCase("invalid.example.")]
        [TestCase("onions.")]
        [TestCase("onion.example.")]
        [TestCase("name.alt.")]
        public async Task ANameThatIsNotBelowEither_IsNotSpecial(String Name)
        {

            using var client = WithoutServers();

            var answer = await client.Query(DNSServiceName.Parse(Name), [ DNSResourceRecordTypes.A ]);

            Assert.Multiple(() => {
                Assert.That(SpecialUseNames.IsLocalhost(DNSServiceName.Parse(Name)),  Is.False);
                Assert.That(SpecialUseNames.IsInvalid  (DNSServiceName.Parse(Name)),  Is.False);
                Assert.That(SpecialUseNames.IsOnion    (DNSServiceName.Parse(Name)),  Is.False);
                Assert.That(answer.ResponseCode,                                      Is.EqualTo(DNSResponseCodes.ServerFailure), "asked of servers there are none of");
            });

        }

        #endregion

        #region ALetterAboveASCII_IsNotTheLetter()

        /// <summary>
        /// Only A to Z have a case (RFC 4343 §3). The long s and the dotless i are "S" and "I" in
        /// upper case under Unicode's rules, which an ordinal comparison that ignores case applies, and
        /// they are not the "s" and the "i" of a name.
        /// </summary>
        [Test]
        public void ALetterAboveASCII_IsNotTheLetter()
        {

            Assert.Multiple(() => {
                Assert.That(SpecialUseNames.IsLocalhost(DNSServiceName.Parse("www.LocalHOST.")),      Is.True,  "A to Z in either case");
                Assert.That(SpecialUseNames.IsInvalid  (DNSServiceName.Parse("www.INValid.")),        Is.True,  "A to Z in either case");
                Assert.That(SpecialUseNames.IsLocalhost(DNSServiceName.Parse("www.localhoſt.")), Is.False, "a long s");
                Assert.That(SpecialUseNames.IsInvalid  (DNSServiceName.Parse("www.ınvalid.")),   Is.False, "a dotless i");
            });

        }

        #endregion

    }

}
