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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// Whether a DNS client searches the machine's network configuration for
    /// resolvers is a default each constructor declares for itself: with
    /// manual servers it does not, without them it does. An unstated wish must
    /// mean what the constructor the caller used says it means.
    /// </summary>
    /// <remarks>
    /// It did not. Both constructors forwarded to one body reading `?? true`,
    /// so a caller who named manual servers and passed null - as a caller
    /// forwarding an optional setting does - got the machine's resolvers added
    /// to the ones it had named, although the signature it read says false.
    /// Found while making the client's DNS client lazy, which is H-27 in
    /// HTTP1ConformanceTests.
    /// </remarks>
    [TestFixture]
    public class DNSServerSearchDefaultTests
    {

        #region Data

        /// <summary>
        /// A documentation address (RFC 5737, TEST-NET-1), so that what the
        /// tests below count is only what they put in themselves.
        /// </summary>
        private static readonly DNSServerConfig manual = new (
                                                             IPv4Address.Parse("192.0.2.53"),
                                                             IPPort.DNS
                                                         );

        #endregion

        #region (private) WhatTheSearchFinds()

        /// <summary>
        /// The DNS servers the machine's network configuration names, found by
        /// a client told to search in so many words.
        /// </summary>
        private static IReadOnlySet<DNSServerConfig> WhatTheSearchFinds()
        {

            using var searching = new DNSClient(
                                      SearchForIPv4DNSServers:  true,
                                      SearchForIPv6DNSServers:  true
                                  );

            return searching.DNSServers;

        }

        #endregion

        #region (private) SomethingToFind()

        /// <summary>
        /// What the search finds, or no test at all: these tests tell
        /// "searched" from "did not search" by whether the machine's resolvers
        /// turn up, and on a machine naming none - a container without
        /// resolvers, for one - the two cannot be told apart. Saying so is the
        /// honest outcome; a green check would be one that could not have gone
        /// red.
        /// </summary>
        private static IReadOnlySet<DNSServerConfig> SomethingToFind()
        {

            var found = WhatTheSearchFinds();

            if (found.Count == 0)
                Assert.Ignore("This machine's network configuration names no DNS servers, so searching and not searching look the same here.");

            return found;

        }

        #endregion


        #region AnExplicitNullWithManualServersDoesNotSearch()

        /// <summary>
        /// The case that was wrong: null is what the constructor declares, and
        /// that constructor declares false.
        /// </summary>
        [Test]
        public void AnExplicitNullWithManualServersDoesNotSearch()
        {

            SomethingToFind();

            using var client = new DNSClient(
                                   ManualDNSServers:         [ manual ],
                                   SearchForIPv4DNSServers:  null,
                                   SearchForIPv6DNSServers:  null
                               );

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(new[] { manual }),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion

        #region NothingSaidWithManualServersDoesNotSearch()

        [Test]
        public void NothingSaidWithManualServersDoesNotSearch()
        {

            SomethingToFind();

            using var client = new DNSClient(ManualDNSServers: [ manual ]);

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(new[] { manual }),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion

        #region AnExplicitFalseWithManualServersDoesNotSearch()

        [Test]
        public void AnExplicitFalseWithManualServersDoesNotSearch()
        {

            SomethingToFind();

            using var client = new DNSClient(
                                   ManualDNSServers:         [ manual ],
                                   SearchForIPv4DNSServers:  false,
                                   SearchForIPv6DNSServers:  false
                               );

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(new[] { manual }),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion

        #region AnExplicitTrueWithManualServersSearchesAsWell()

        /// <summary>
        /// Manual servers and the search are not exclusive: asked for both, a
        /// client queries both.
        /// </summary>
        [Test]
        public void AnExplicitTrueWithManualServersSearchesAsWell()
        {

            var found        = SomethingToFind();

            using var client = new DNSClient(
                                   ManualDNSServers:         [ manual ],
                                   SearchForIPv4DNSServers:  true,
                                   SearchForIPv6DNSServers:  true
                               );

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(found.Append(manual)),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion


        #region NothingSaidWithoutManualServersSearches()

        /// <summary>
        /// The other constructor's default, which is the opposite one and must
        /// stay that way: without manual servers a client that does not search
        /// has no servers at all.
        /// </summary>
        [Test]
        public void NothingSaidWithoutManualServersSearches()
        {

            var found        = SomethingToFind();

            using var client = new DNSClient();

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(found),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion

        #region AnExplicitNullWithoutManualServersSearches()

        /// <summary>
        /// And an unstated wish there means what that constructor declares,
        /// which is why the default is resolved before it is forwarded.
        /// </summary>
        [Test]
        public void AnExplicitNullWithoutManualServersSearches()
        {

            var found        = SomethingToFind();

            using var client = new DNSClient(
                                   SearchForIPv4DNSServers:  null,
                                   SearchForIPv6DNSServers:  null
                               );

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(found),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion

        #region OneNamedServerIsTheOnlyOne()

        /// <summary>
        /// The overload naming a single server forwards no wish at all, and so
        /// takes the default of the constructor it forwards to. A caller asking
        /// for one resolver must not be given others beside it.
        /// </summary>
        [Test]
        public void OneNamedServerIsTheOnlyOne()
        {

            SomethingToFind();

            using var client = new DNSClient(IPv4Address.Parse("192.0.2.53"));

            Assert.That(
                client.DNSServers,
                Is.EquivalentTo(new[] { manual }),
                String.Join(", ", client.DNSServers)
            );

        }

        #endregion

    }

}
