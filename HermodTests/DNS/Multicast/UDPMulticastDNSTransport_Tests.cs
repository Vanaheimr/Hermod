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

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Multicast
{

    /// <summary>
    /// Multicast DNS over real UDP sockets (category "Multicast": needs a network interface
    /// supporting multicast and is skipped where none exists). Two transports of one process
    /// share a private port, so that the host's own Multicast DNS responder stays out of the way.
    /// </summary>
    [TestFixture]
    [Category("Multicast")]
    public class UDPMulticastDNSTransport_Tests
    {

        #region Helpers

        private static UDPMulticastDNSTransportOptions TransportOptions(IPPort Port)
            => new () {
                   Port         = Port,
                   EnableIPv6   = false
               };

        private static async Task<UDPMulticastDNSTransport> StartTransportAsync(IPPort Port)
        {

            var transport = new UDPMulticastDNSTransport(TransportOptions(Port));

            try
            {
                await transport.StartAsync();
            }
            catch (InvalidOperationException e)
            {
                await transport.DisposeAsync();
                Assert.Ignore($"No multicast capable network interface: {e.Message}");
            }

            return transport;

        }

        private static IPPort TestPort()
            => IPPort.Parse((UInt16) (5400 + Random.Shared.Next(0, 100)));

        #endregion


        #region Transport_StartsAndReportsAddresses()

        [Test]
        public async Task Transport_StartsAndReportsAddresses()
        {

            await using var transport = await StartTransportAsync(TestPort());

            Assert.Multiple(() => {
                Assert.That(transport.IsRunning,          Is.True);
                Assert.That(transport.LocalAddresses,     Is.Not.Empty);
                Assert.That(transport.InterfaceIndexes,   Is.Not.Empty);
            });

            await transport.StopAsync();

            Assert.That(transport.IsRunning, Is.False);

        }

        #endregion

        #region Responder_And_Client_OnSeparateSockets_ResolveAndBrowse()

        [Test]
        public async Task Responder_And_Client_OnSeparateSockets_ResolveAndBrowse()
        {

            var port = TestPort();

            await using var serverTransport = await StartTransportAsync(port);
            await using var clientTransport = await StartTransportAsync(port);

            var responderOptions = new MulticastDNSResponderOptions {
                                       ProbeInterval               = TimeSpan.FromMilliseconds(50),
                                       MaxInitialProbeDelay        = TimeSpan.Zero,
                                       AnnouncementInterval        = TimeSpan.FromMilliseconds(50),
                                       MinRecordMulticastInterval  = TimeSpan.Zero
                                   };

            // Two sockets bound to the same port receive every multicast, but a unicast reaches
            // only one of them, so the client asks for multicast responses here.
            var clientOptions    = new MulticastDNSClientOptions {
                                       QueryTimeout               = TimeSpan.FromSeconds(2),
                                       ResponseGracePeriod        = TimeSpan.FromMilliseconds(100),
                                       RetransmissionInterval     = TimeSpan.FromMilliseconds(300),
                                       RequestUnicastResponses    = false,
                                       CacheSweepInterval         = TimeSpan.FromMilliseconds(100),
                                       GoodbyeDelay               = TimeSpan.Zero,
                                       BrowseMaintenanceInterval  = TimeSpan.FromMilliseconds(100),
                                       InitialBrowseInterval      = TimeSpan.FromMilliseconds(200)
                                   };

            await using var responder  = new MulticastDNSResponder(serverTransport, responderOptions);
            await using var client     = new MulticastDNSClient   (clientTransport, clientOptions);

            await responder.StartAsync();
            await client.   StartAsync();

            var address      = serverTransport.LocalAddresses.OfType<IPv4Address>().First();
            var publication  = await responder.PublishAsync(
                                   MulticastDNS_SmokeTests.ServiceRecords("mdnstest-host.local.", address.ToString(), 8443, "_mdnstest._tcp.local.")
                               );

            Assert.That(publication.State, Is.EqualTo(MulticastDNSPublicationState.Published));

            var addresses = (await client.Query_IPv4Addresses(DomainName.Parse("mdnstest-host.local."))).ToArray();
            Assert.That(addresses, Is.EqualTo(new[] { address }));

            var browser = await client.BrowseAsync(DNSServiceName.Parse("_mdnstest._tcp.local."));

            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            while (browser.Instances.Count == 0 && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50);

            Assert.That(browser.Instances, Has.Count.EqualTo(1));

            var instance = browser.Instances[0];

            Assert.Multiple(() => {
                Assert.That(instance.InstanceName.FullName,   Is.EqualTo("mdnstest-host._mdnstest._tcp.local."));
                Assert.That(instance.HostName?.FullName,      Is.EqualTo("mdnstest-host.local."));
                Assert.That(instance.Port?.ToUInt16(),        Is.EqualTo((UInt16) 8443));
                Assert.That(instance.TXT?.KeyValues["txtver"], Is.EqualTo("1"));
            });

            await publication.WithdrawAsync();

            deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            while (browser.Instances.Count > 0 && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(50);

            Assert.That(browser.Instances, Is.Empty);

        }

        #endregion

    }

}
