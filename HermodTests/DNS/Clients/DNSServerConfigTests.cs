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

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// A DNS server this client has no address for.
    /// </summary>
    /// <remarks>
    /// DNSServerConfig does two jobs: it says which server to query, and it says
    /// where an answer came from. Only the first needs an address. The second
    /// routinely has none — a DNS-over-HTTPS or DNS-over-TLS client created from
    /// a URL learns its address when the socket connects and never learns it if
    /// the connection fails — and the transports wrote RemoteIPAddress! into a
    /// non-nullable field to paper over the difference. ToString() would have
    /// dereferenced that null.
    /// </remarks>
    [TestFixture]
    public class DNSServerConfigTests
    {

        #region AServerKnownOnlyByName_SaysSoAndStillNamesItself()

        [Test]
        public void AServerKnownOnlyByName_SaysSoAndStillNamesItself()
        {

            var config = new DNSServerConfig(
                             DomainName.Parse("dns.example"),
                             IPPort.HTTPS,
                             DNSTransport.HTTPS
                         );

            Assert.Multiple(() => {
                Assert.That(config.IPAddress,   Is.Null,  "the premise: there is no address");
                // The trailing dot is the root label: DomainName renders the fully
                // qualified form.
                Assert.That(config.ToString(),  Is.EqualTo("https://dns.example.:443"));
            });

        }

        #endregion

        #region AServerWithNothingKnownAboutIt_CanStillBeAsked()

        /// <summary>
        /// The crash that was waiting. IPAddress was not nullable and the DoH and
        /// DoT clients wrote RemoteIPAddress! into it, so an object in exactly
        /// this state existed — and ToString() reached straight through the null
        /// for IsIPv6.
        /// </summary>
        [Test]
        public void AServerWithNothingKnownAboutIt_CanStillBeAsked()
        {

            var config = new DNSServerConfig(
                             (IIPAddress) null!,
                             IPPort.HTTPS,
                             DNSTransport.HTTPS
                         );

            Assert.That(config.ToString(), Is.EqualTo("https://<unknown>:443"));

        }

        #endregion

        #region AnIPv6Server_IsBracketedSoThePortIsReadable()

        /// <summary>
        /// An address a reader can tell from its port.
        /// </summary>
        [Test]
        public void AnIPv6Server_IsBracketedSoThePortIsReadable()
        {

            var config = new DNSServerConfig(
                             IPv6Address.Parse("2001:4860:4860::8888"),
                             IPPort.DNS_TLS,
                             DNSTransport.TLS
                         );

            // RFC 3986 §3.2.2 - without the brackets the colon before the port is
            // one colon among many.
            Assert.That(config.ToString(), Does.StartWith("tls://[2001:"));
            Assert.That(config.ToString(), Does.EndWith("]:853"));

        }

        #endregion

        #region EveryTransportHasAPort(...)

        /// <summary>
        /// The two constructors used to spell the port defaults out separately and
        /// each covered six of the ten transports. The other four kept Port at its
        /// default — port 0 — and HTTPS_GET is one DNSClient dispatches on.
        /// </summary>
        [TestCase(DNSTransport.UDP,           53)]
        [TestCase(DNSTransport.TCP,           53)]
        [TestCase(DNSTransport.TLS,          853)]
        [TestCase(DNSTransport.HTTP,          80)]
        [TestCase(DNSTransport.HTTP_Binary,   80)]
        [TestCase(DNSTransport.HTTP_JSON,     80)]
        [TestCase(DNSTransport.HTTPS,        443)]
        [TestCase(DNSTransport.HTTPS_Binary, 443)]
        [TestCase(DNSTransport.HTTPS_JSON,   443)]
        [TestCase(DNSTransport.HTTPS_GET,    443)]
        public void EveryTransportHasAPort(DNSTransport Transport, Int32 ExpectedPort)
        {

            var byAddress  = new DNSServerConfig(IPv4Address.Localhost,          Transport: Transport);
            var byName     = new DNSServerConfig(DomainName.Parse("dns.example"), Transport: Transport);

            Assert.Multiple(() => {
                Assert.That(byAddress.Port.ToUInt16(),  Is.EqualTo(ExpectedPort),  $"{Transport} by address");
                Assert.That(byName.   Port.ToUInt16(),  Is.EqualTo(ExpectedPort),  $"{Transport} by name");
            });

        }

        #endregion

        #region ADoHClientThatNeverConnected_StillNamesItsResolver()

        /// <summary>
        /// A DNS-over-HTTPS client pointed at a name nobody answers on. The query
        /// fails, and the answer it hands back still has to say which resolver it
        /// was asking.
        /// </summary>
        [Test]
        public async Task ADoHClientThatNeverConnected_StillNamesItsResolver()
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var closedPort = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();

            using var client = new DNSHTTPSClient(
                                   URL.Parse($"https://localhost:{closedPort}/dns-query"),
                                   QueryTimeout: TimeSpan.FromSeconds(5)
                               );

            var response = await client.QueryHTTP(
                                     DNSServiceName.Parse("example.org"),
                                     [DNSResourceRecordTypes.A]
                                 );

            Assert.Multiple(() => {

                Assert.That(response.IsValid,           Is.False,  "nothing is listening there");

                // The point of the exercise: asking is safe, and the answer is
                // the resolver rather than nothing.
                Assert.That(response.Origin.ToString(), Does.Contain("localhost"),
                            response.Origin.ToString());

            });

        }

        #endregion


        #region TheSameServerTwice_IsOneServer()

        /// <summary>
        /// Two configs of one server are one server.
        /// </summary>
        /// <remarks>
        /// DNSClient keeps its servers in a set, and fills it from every network
        /// interface - where one server is listed once per interface. Compared
        /// by reference, as a class is unless it says otherwise, every one of
        /// those was a server of its own: a Windows machine listed its router's
        /// address twice and fec0:0:0:ffff::1 to ::3 three times each, and a
        /// query nobody answered was asked of each of them again.
        /// </remarks>
        [Test]
        public void TheSameServerTwice_IsOneServer()
        {

            var first   = new DNSServerConfig(IPv6Address.Parse("fd00::2e3a:fdff:fefe:dd09"));
            var second  = new DNSServerConfig(IPv6Address.Parse("fd00::2e3a:fdff:fefe:dd09"));

            Assert.Multiple(() => {
                Assert.That(first.Equals(second),                             Is.True);
                Assert.That(first.GetHashCode(),                              Is.EqualTo(second.GetHashCode()));
                Assert.That(new HashSet<DNSServerConfig> { first, second },   Has.Count.EqualTo(1));
            });

        }

        #endregion

        #region TheSameServerOnThreeInterfaces_IsOneServerOfTheClient()

        /// <summary>
        /// The server list a client keeps holds each server once - here from the
        /// same address as three interfaces report it, each with a scope of its
        /// own, which is how Windows hands out its site-local name servers.
        /// </summary>
        /// <remarks>
        /// A client given its servers rather than looking for the machine's, so
        /// that what is counted is only what this test put in.
        /// </remarks>
        [Test]
        public void TheSameServerOnThreeInterfaces_IsOneServerOfTheClient()
        {

            var client = new DNSClient([
                             IPv6Address.From(System.Net.IPAddress.Parse("fec0:0:0:ffff::1%1")),
                             IPv6Address.From(System.Net.IPAddress.Parse("fec0:0:0:ffff::1%2")),
                             IPv6Address.From(System.Net.IPAddress.Parse("fec0:0:0:ffff::1%3"))
                         ]);

            Assert.That(client.DNSServers, Has.Count.EqualTo(1),
                        String.Join(", ", client.DNSServers));

        }

        #endregion

        #region AnotherTimeout_IsTheSameServer()

        /// <summary>
        /// How long to wait for a server is not which server it is.
        /// </summary>
        /// <remarks>
        /// And the timeout can be changed after the config went into a set,
        /// which is exactly what a hash code must not follow.
        /// </remarks>
        [Test]
        public void AnotherTimeout_IsTheSameServer()
        {

            var patient    = new DNSServerConfig(IPv4Address.Parse("192.0.2.53"), QueryTimeout: TimeSpan.FromSeconds(30));
            var impatient  = new DNSServerConfig(IPv4Address.Parse("192.0.2.53"), QueryTimeout: TimeSpan.FromSeconds(1));

            Assert.That(patient, Is.EqualTo(impatient));

        }

        #endregion

        #region AnotherTransportOrPort_IsAnotherServer()

        /// <summary>
        /// What keeps the equality from being too generous: the same address
        /// asked another way, or on another port, is another server.
        /// </summary>
        [Test]
        public void AnotherTransportOrPort_IsAnotherServer()
        {

            var address  = IPv4Address.Parse("192.0.2.53");

            var servers  = new HashSet<DNSServerConfig> {
                               new (address),
                               new (address, Transport: DNSTransport.TCP),
                               new (address, IPPort.Parse(5353)),
                               new (DomainName.Parse("dns.example"), address)
                           };

            Assert.That(servers, Has.Count.EqualTo(4),
                        String.Join(", ", servers));

        }

        #endregion

    }

}
