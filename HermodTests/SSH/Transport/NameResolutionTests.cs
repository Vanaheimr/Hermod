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

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.SSH;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Client;
using org.GraphDefined.Vanaheimr.Hermod.SSH.Server;
using org.GraphDefined.Vanaheimr.Hermod.Tests;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH.Tests
{

    /// <summary>
    /// How the SSH client finds the address of a host: an IP address is itself, "localhost" the loopback,
    /// and a host name what the DNS client it was given says - never the resolver of the operating system.
    /// </summary>
    /// <remarks>
    /// The client used to hand a host name to the socket, which asked the operating system. A node whose
    /// names are resolved securely - its own name servers, DNS over TLS, DNSSEC - was dialled around them,
    /// and nothing said so. A host name without a DNS client is now refused.
    /// </remarks>
    [TestFixture]
    public class NameResolutionTests
    {

        #region AnIPAddressIsItself

        [Test]
        public async Task AnIPAddressIsItself()
        {

            Assert.That((await SshTcp.ResolveAsync("127.0.0.1", null)).Select(address => address.ToString()), Is.EqualTo(new[] { "127.0.0.1" }));
            Assert.That((await SshTcp.ResolveAsync("[::1]",     null)).Select(address => address.ToString()), Is.EqualTo(new[] { "::1" }));
            Assert.That((await SshTcp.ResolveAsync("localhost", null)).Select(address => address.ToString()), Is.EqualTo(new[] { "127.0.0.1", "::1" }));

        }

        #endregion

        #region AHostNameWithoutADNSClientIsRefused

        [Test]
        public async Task AHostNameWithoutADNSClientIsRefused()
        {

            var refused = await Assert.ThrowsAsync<SshNameResolutionException>(async () => await SshTcp.ResolveAsync("csms.example.org", null));

            Assert.That(refused!.Host,     Is.EqualTo("csms.example.org"));
            Assert.That(refused.Message,   Does.Contain("no DNS client"));

        }

        #endregion

        #region AHostNameIsAskedOfTheDNSClient

        [Test]
        public async Task AHostNameIsAskedOfTheDNSClient()
        {

            var dnsClient = new FakeDNSClient();

            dnsClient.IPv4Addresses.Add(IPv4Address.Parse("192.0.2.7"));
            dnsClient.IPv6Addresses.Add(IPv6Address.Parse("2001:db8::7"));

            var addresses = await SshTcp.ResolveAsync("csms.example.org", dnsClient);

            Assert.That(addresses.Select(address => address.ToString()), Is.EqualTo(new[] { "2001:db8::7", "192.0.2.7" }),
                        "Not the DNS client's addresses, IPv6 first.");

            Assert.That(dnsClient.QueriedNames.Select(name => name.FullName.TrimEnd('.')).Distinct(), Is.EqualTo(new[] { "csms.example.org" }));

        }

        #endregion

        #region ANameWithoutAnAddressSaysSo

        [Test]
        public async Task ANameWithoutAnAddressSaysSo()
        {

            var refused = await Assert.ThrowsAsync<SshNameResolutionException>(async () => await SshTcp.ResolveAsync("csms.example.org", new FakeDNSClient()));

            Assert.That(refused!.Message, Does.Contain("no address"));

        }

        #endregion

        #region TheClientDialsAHostNameThroughItsDNSClient

        /// <summary>
        /// The whole way: an SshClient told to connect to a host name connects to the address its DNS client
        /// gives - a name the operating system does not know - and without a DNS client, it does not.
        /// </summary>
        [Test]
        [CancelAfter(25000)]
        public async Task TheClientDialsAHostNameThroughItsDNSClient(CancellationToken CancellationToken)
        {

            var hostKey  = SshHostKey.GenerateEd25519();
            var userKey  = SshHostKey.GenerateEd25519();

            var server   = new SshServer(new SshServerOptions {
                               HostKeys       = [ hostKey ],
                               Authenticator  = SshUserAuthenticator.ForAuthorizedKeys(userKey.PublicKeyBlob),
                               ExecHandler    = async (ctx, ct) => { await ctx.WriteAsync("here\n", ct); return 0; }
                           });

            try
            {

                await server.StartAsync(new IPSocket(IPv4Address.Localhost, IPPort.Auto), CancellationToken);

                var port       = (UInt16) server.LocalEndPoint.Port.ToInt32();
                var dnsClient  = new FakeDNSClient();

                dnsClient.IPv4Addresses.Add(IPv4Address.Localhost);

                var login      = new SshClientOptions {
                                     Username       = "cs001",
                                     VerifyHostKey  = blob => blob.AsSpan().SequenceEqual(hostKey.PublicKeyBlob),
                                     Credentials    = [ userKey ],
                                     DNSClient      = dnsClient
                                 };

                // .test is a name no public name server answers, and one a resolver sends to its
                // configured server like any other (RFC 6761 §6.2) - here, the stand-in DNS client.
                await using (var client = await SshClient.ConnectAsync("csms.test", port, login, CancellationToken))
                {
                    var result = await client.ExecuteAsync("where", CancellationToken);
                    Assert.That(result.StandardOutput, Is.EqualTo("here\n"));
                }

                Assert.That(dnsClient.QueryCount, Is.GreaterThan(0), "The DNS client was not asked.");

                await Assert.ThrowsAsync<SshNameResolutionException>(async () =>
                    await SshClient.ConnectAsync("csms.test", port, login with { DNSClient = null }, CancellationToken));

            }
            finally
            {
                await server.DisposeAsync();
            }

        }

        #endregion

    }

}
