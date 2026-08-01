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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS
{

    /// <summary>
    /// Tests for resolving the host of an URL.
    ///
    /// An URL whose host already is an IP address must never be sent to a DNS server:
    /// the old code did DomainName.Parse(RemoteURL.Hostname.Name), and as "192.168.1.1"
    /// happens to pass the domain name syntax check, that fired a pointless A query for
    /// a name that can not exist.
    /// </summary>
    [TestFixture]
    public class URLResolutionTests
    {

        #region Query_IPv4Addresses_AnIPv4HostIsNotResolved()

        /// <summary>
        /// An URL with an IPv4 host returns that address without any DNS lookup.
        /// </summary>
        [Test]
        public async Task Query_IPv4Addresses_AnIPv4HostIsNotResolved()
        {

            var dnsClient = new FakeDNSClient();

            var addresses = await dnsClient.Query_IPv4Addresses(URL.Parse("https://192.168.1.1/some/path"));

            Assert.That(dnsClient.QueryCount,                 Is.EqualTo(0));
            Assert.That(addresses.Select(a => a.ToString()),  Is.EqualTo(new[] { "192.168.1.1" }));

        }

        #endregion

        #region Query_IPv6Addresses_AnIPv6HostIsNotResolved()

        /// <summary>
        /// An URL with an IPv6 host returns that address without any DNS lookup.
        /// </summary>
        [Test]
        public async Task Query_IPv6Addresses_AnIPv6HostIsNotResolved()
        {

            var dnsClient = new FakeDNSClient();

            var addresses = await dnsClient.Query_IPv6Addresses(URL.Parse("https://[::1]/"));

            Assert.That(dnsClient.QueryCount, Is.EqualTo(0));
            Assert.That(addresses.Count(),    Is.EqualTo(1));
            Assert.That(addresses.First().IsLocalhost, Is.True);

        }

        #endregion

        #region Query_IPvXAddresses_AMismatchingIPFamilyYieldsNothing()

        /// <summary>
        /// An IPv4 host has no IPv6 address and vice versa - and still must not
        /// trigger a lookup.
        /// </summary>
        [Test]
        public async Task Query_IPvXAddresses_AMismatchingIPFamilyYieldsNothing()
        {

            var dnsClient = new FakeDNSClient();

            Assert.That((await dnsClient.Query_IPv6Addresses(URL.Parse("https://192.168.1.1/"))).Any(), Is.False);
            Assert.That((await dnsClient.Query_IPv4Addresses(URL.Parse("https://[::1]/"))).      Any(), Is.False);

            Assert.That(dnsClient.QueryCount, Is.EqualTo(0));

        }

        #endregion

        #region Query_IPAddresses_AnIPHostIsNotResolved()

        /// <summary>
        /// The combined lookup must short-circuit as well, instead of firing two queries.
        /// </summary>
        [Test]
        public async Task Query_IPAddresses_AnIPHostIsNotResolved()
        {

            var dnsClient = new FakeDNSClient();

            var addresses = await dnsClient.Query_IPAddresses(URL.Parse("https://192.168.1.1/"));

            Assert.That(dnsClient.QueryCount,                 Is.EqualTo(0));
            Assert.That(addresses.Select(a => a.ToString()),  Is.EqualTo(new[] { "192.168.1.1" }));

        }

        #endregion

        #region Query_IPv4Addresses_ADomainNameIsResolvedExactlyOnce()

        /// <summary>
        /// A real host name is of course still resolved - and the already parsed domain
        /// name of the URL is handed over as-is, without a detour through its text.
        /// </summary>
        [Test]
        public async Task Query_IPv4Addresses_ADomainNameIsResolvedExactlyOnce()
        {

            var dnsClient = new FakeDNSClient();
            dnsClient.IPv4Addresses.Add(IPv4Address.Parse("93.184.216.34"));

            var addresses = await dnsClient.Query_IPv4Addresses(URL.Parse("https://www.example.org/some/path"));

            Assert.That(dnsClient.QueryCount,                     Is.EqualTo(1));
            Assert.That(dnsClient.QueriedNames.Single().FullName,  Is.EqualTo("www.example.org."));
            Assert.That(addresses.Select(a => a.ToString()),      Is.EqualTo(new[] { "93.184.216.34" }));

        }

        #endregion

        #region Query_IPAddresses_ADomainNameIsResolvedForBothFamilies()

        /// <summary>
        /// The combined lookup asks for A and AAAA and merges both.
        /// </summary>
        [Test]
        public async Task Query_IPAddresses_ADomainNameIsResolvedForBothFamilies()
        {

            var dnsClient = new FakeDNSClient();
            dnsClient.IPv4Addresses.Add(IPv4Address.Parse("93.184.216.34"));
            dnsClient.IPv6Addresses.Add(IPv6Address.Parse("2606:2800:220:1:248:1893:25c8:1946"));

            var addresses = await dnsClient.Query_IPAddresses(URL.Parse("https://www.example.org/"));

            Assert.That(dnsClient.QueryCount, Is.EqualTo(2));
            Assert.That(dnsClient.QueriedNames.Select(name => name.FullName).Distinct().Single(),
                        Is.EqualTo("www.example.org."));
            Assert.That(addresses.Count(),    Is.EqualTo(2));

        }

        #endregion

        #region Query_IPv4Addresses_TheURLPortAndPathDoNotLeakIntoTheQuery()

        /// <summary>
        /// Only the host is resolved, never the host including its port.
        /// </summary>
        [Test]
        public async Task Query_IPv4Addresses_TheURLPortAndPathDoNotLeakIntoTheQuery()
        {

            var dnsClient = new FakeDNSClient();

            await dnsClient.Query_IPv4Addresses(URL.Parse("https://www.example.org:8443/a/b?c=d#e"));

            Assert.That(dnsClient.QueriedNames.Single().FullName, Is.EqualTo("www.example.org."));

        }

        #endregion

    }

}
