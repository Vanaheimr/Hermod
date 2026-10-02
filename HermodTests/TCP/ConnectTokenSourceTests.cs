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

using System.Net;
using System.Net.Sockets;
using System.Reflection;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.TCP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A connect leaves nothing registered on the token source of its client.
    /// </summary>
    /// <remarks>
    /// Every connect made a token source for its timeout, linked a second one
    /// to it and to the client's own token source, and disposed of neither. A
    /// linked token source stays registered on the ones it is linked to until
    /// it is disposed, and the client's lives as long as the client. So every
    /// connect left a registration there, and both of its token sources with
    /// it, until the client was closed - whether the connect went through or
    /// timed out. An HTTP client connects anew for every request to a server
    /// that closes its connections, and a long-lived one gained a registration
    /// with every such request: 20 GETs took the count from 1 to 21.
    ///
    /// No public API counts the registrations on a token source. They are
    /// counted by reflection into CancellationTokenSource, through fields that
    /// a .NET version is free to rename. Where one of them is not found, a test
    /// is inconclusive rather than passed, and says which one it missed.
    /// </remarks>
    [TestFixture]
    public class ConnectTokenSourceTests
    {

        #region Data

        /// <summary>
        /// How many connects are counted. Every check parts at half of them, as
        /// the timer tests do.
        /// </summary>
        private const           Int32     Connects      = 20;

        /// <summary>
        /// The timeout of the connects that go through: far beyond any of them.
        /// </summary>
        private static readonly TimeSpan  LongTimeout   = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The timeout of the connects that time out.
        /// </summary>
        private static readonly TimeSpan  ShortTimeout  = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// How long a test waits for a connect to report.
        /// </summary>
        private static readonly TimeSpan  Patience      = TimeSpan.FromSeconds(10);

        #endregion

        #region (private) TokenSourceTCPClient

        /// <summary>
        /// A TCP client that shows its own token source.
        /// </summary>
        private sealed class TokenSourceTCPClient(URL         URL,
                                                  TimeSpan    ConnectTimeout,
                                                  IDNSClient  DNSClient)

            : TCPClient(URL,
                        ConnectTimeout:  ConnectTimeout,
                        DNSClient:       DNSClient)

        {

            public CancellationTokenSource TokenSource
                => clientCancellationTokenSource;

        }

        #endregion

        #region (private static) RegistrationsOn(TokenSource)

        /// <summary>
        /// The number of callbacks registered on the given token source: the
        /// nodes of the list that starts at the Callbacks of its _registrations
        /// and goes on through each node's Next. Nothing registers on the token
        /// source of a test's client while the test counts.
        /// </summary>
        private static Int32 RegistrationsOn(CancellationTokenSource TokenSource)
        {

            // Made with the first registration.
            var registrations  = Field(typeof(CancellationTokenSource), "_registrations").GetValue(TokenSource);

            if (registrations is null)
                return 0;

            var count          = 0;
            var node           = Field(registrations.GetType(), "Callbacks").GetValue(registrations);

            while (node is not null)
            {
                count++;
                node = Field(node.GetType(), "Next").GetValue(node);
            }

            return count;

        }

        private static FieldInfo Field(Type    Type,
                                       String  Name)
        {

            var field = Type.GetField(Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field is null)
                Assert.Inconclusive($"{Type.Name}.{Name} was not found by reflection: this .NET version keeps the registrations of a CancellationTokenSource elsewhere, and the test cannot count them.");

            return field;

        }

        #endregion

        #region (private static) CheckTheCount(TokenSource)

        /// <summary>
        /// The count is worth something only where it sees registrations come
        /// and go: a callback registered on the token source for the purpose
        /// must be counted, and no longer once it is disposed of. Where the
        /// fields are found but hold something else, the test is inconclusive
        /// as well, rather than passed on a count that never moves.
        /// </summary>
        private static void CheckTheCount(CancellationTokenSource TokenSource)
        {

            var before  = RegistrationsOn(TokenSource);
            var with    = 0;

            using (TokenSource.Token.Register(() => { }))
                with    = RegistrationsOn(TokenSource);

            var after   = RegistrationsOn(TokenSource);

            if (with != before + 1 || after != before)
                Assert.Inconclusive($"The count read {before} before a callback was registered for the purpose, {with} with it, and {after} once it was disposed of: reflection finds the fields, but not the registrations in them.");

        }

        #endregion

        #region (private static) Describe(Result)

        private static String Describe(TCPConnectionResult Result)

            => Result.IsSuccess
                   ? "success"
                   : Result.Errors.Select(error => error.ToString()).AggregateWith(" | ");

        #endregion


        #region ReconnectsLeaveNothingRegisteredOnTheClientsTokenSource()

        /// <summary>
        /// A client connects, and then reconnects again and again, as an HTTP
        /// client does for every request to a server that closes its
        /// connections. Every one of these connects goes through.
        /// </summary>
        [Test]
        public async Task ReconnectsLeaveNothingRegisteredOnTheClientsTokenSource()
        {

            // Never accepts: the operating system completes a connect on its
            // own, as long as somebody listens.
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            try
            {

                var url = URL.Parse($"tcp://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}");

                // Lent to the client: one it makes itself outlives it.
                await using var dnsClient  = new DNSClient(ManualDNSServers: []);
                await using var client     = new TokenSourceTCPClient(url, LongTimeout, dnsClient);

                Assert.That(Describe(await client.ConnectAsync().WaitAsync(Patience)), Is.EqualTo("success"), "what the first connect reported");

                var tokenSource  = client.TokenSource;

                CheckTheCount(tokenSource);

                var before       = RegistrationsOn(tokenSource);
                var results      = new List<String>();

                for (var i = 0; i < Connects; i++)
                    results.Add(Describe(await client.ReconnectAsync(CancellationToken.None).WaitAsync(Patience)));

                var left         = RegistrationsOn(tokenSource) - before;

                Assert.Multiple(() => {

                    Assert.That(results,             Is.All.EqualTo("success"),  "what the reconnects reported");
                    Assert.That(client.TokenSource,  Is.SameAs(tokenSource),     "the client's token source, after the reconnects");

                    Assert.That(left,                Is.LessThan(Connects / 2),  $"registrations left on the client's token source after {Connects} reconnects");

                });

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

        #region ConnectsThatTimeOutLeaveNothingRegisteredOnTheClientsTokenSource()

        /// <summary>
        /// A client connects again and again to a listener that has no room for
        /// it, and every connect times out.
        /// </summary>
        /// <remarks>
        /// The listener never accepts, and one connection fills its backlog of
        /// none. Linux drops the SYN of every connect after that one; Windows
        /// answers it with a reset, and the connect tries again for about two
        /// seconds before it reports the refusal. Either way, the connect is
        /// still pending when its timeout ends it.
        /// </remarks>
        [Test]
        public async Task ConnectsThatTimeOutLeaveNothingRegisteredOnTheClientsTokenSource()
        {

            using var listener  = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using var filler    = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            listener.Listen(0);

            // There is room for this one, and none for any after it.
            await filler.ConnectAsync(listener.LocalEndPoint!).WaitAsync(Patience);

            var url = URL.Parse($"tcp://127.0.0.1:{((IPEndPoint) listener.LocalEndPoint!).Port}");

            await using var dnsClient  = new DNSClient(ManualDNSServers: []);
            await using var client     = new TokenSourceTCPClient(url, ShortTimeout, dnsClient);

            var tokenSource  = client.TokenSource;

            CheckTheCount(tokenSource);

            var before       = RegistrationsOn(tokenSource);
            var results      = new List<String>();

            for (var i = 0; i < Connects; i++)
                results.Add(Describe(await client.ConnectAsync().WaitAsync(Patience)));

            var left         = RegistrationsOn(tokenSource) - before;

            Assert.Multiple(() => {

                Assert.That(results,             Is.All.EqualTo("Connection timeout!"),  "what the connects reported");
                Assert.That(client.TokenSource,  Is.SameAs(tokenSource),                 "the client's token source, after the connects");

                Assert.That(left,                Is.LessThan(Connects / 2),              $"registrations left on the client's token source after {Connects} connects that timed out");

            });

        }

        #endregion

    }

}
