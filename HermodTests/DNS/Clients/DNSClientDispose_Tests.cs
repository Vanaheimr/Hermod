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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.DNS.Clients
{

    /// <summary>
    /// What a query in flight says when its client is disposed under it, and
    /// how long a query for two types may take against a server that never
    /// answers.
    /// </summary>
    /// <remarks>
    /// The stream clients disposed their lock in DisposeAsync while a query still
    /// held it, and the query's release on the way out threw
    /// ObjectDisposedException in place of its answer. The UDP client did not
    /// end its queries at all: each went on to its deadline and reported a
    /// timeout. And since a query for two types became two queries (RFC 9619 §4),
    /// TCP served them one after the other, each with a deadline of its own.
    /// </remarks>
    [TestFixture]
    public class DNSClientDispose_Tests
    {

        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(6);


        #region (private) Abandon(Client, Arrived)

        /// <summary>
        /// Start a query, wait until the server has it, dispose the client, and
        /// return what the query came back with and how long that took.
        /// </summary>
        private static async Task<(DNSInfo? Info, Exception? Thrown, TimeSpan Elapsed)> Abandon(IDNSClient   Client,
                                                                                                    Task         Arrived)
        {

            var clock = Stopwatch.StartNew();
            var query = Client.Query(DNSServiceName.Parse("abandoned.example."), [ DNSResourceRecordTypes.A ], Deadline);

            await Arrived.WaitAsync(TimeSpan.FromSeconds(3));
            await Client.DisposeAsync();

            try
            {
                return (await query, null, clock.Elapsed);
            }
            catch (Exception e)
            {
                return (null, e, clock.Elapsed);
            }

        }

        #endregion

        #region (private) AssertAFailureAtOnce(Result)

        private static void AssertAFailureAtOnce((DNSInfo? Info, Exception? Thrown, TimeSpan Elapsed) Result)

            => Assert.Multiple(() => {
                   Assert.That(Result.Thrown,          Is.Null,                  "the query came back rather than throwing");
                   Assert.That(Result.Info?.IsTimeout, Is.False,                 "as a failure, not a timeout");
                   Assert.That(Result.Elapsed,         Is.LessThan(Deadline / 2), "and at once rather than at the deadline");
               });

        #endregion

        #region (private) AbandonMany(NewClient)

        /// <summary>
        /// Abandon a query as the tests below do, eight at a time and fifty times
        /// over, and collect every one that did not end as a failure at once, or
        /// that connected again on its way out.
        /// </summary>
        /// <remarks>
        /// DisposeAsync closed the connection before it cancelled the client's
        /// token, and a single abandoned query met that gap about once in sixty
        /// runs of the whole suite: a reconnect inside it replaced the cancelled
        /// token source, and the query, asking the field, reported a timeout.
        /// Eight at a time, 22 of 800 TCP queries and 34 of 800 DoH queries
        /// did, and every one of them had connected again - which is why the
        /// reconnect is asked about too, as a second connection left waiting
        /// on the listener.
        /// </remarks>
        private static async Task AbandonMany(Func<Int32, IDNSClient> NewClient)
        {

            const Int32 rounds = 50;
            const Int32 width  = 8;

            // How many came back wrong, and how - counted rather than listed, so
            // that a failure reads as "11 came back as a timeout" and not as the
            // first few lines of a list cut short.
            var wrong = new Dictionary<String, Int32>();

            void Count(String What)
                => wrong[What] = wrong.GetValueOrDefault(What) + 1;

            for (var round = 0; round < rounds; round++)
            {

                var outcomes = await Task.WhenAll(
                                   Enumerable.Range(0, width).Select(async _ => {

                                       var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
                                       listener.Start();

                                       var arrived  = FirstByte(listener);

                                       try
                                       {

                                           var result = await Abandon(NewClient(((IPEndPoint) listener.LocalEndpoint).Port), arrived);

                                           return (Result: result, Reconnected: listener.Pending());

                                       }
                                       finally
                                       {
                                           await Stop(listener, arrived);
                                       }

                                   })
                               );

                foreach (var (result, reconnected) in outcomes)
                {

                    if      (result.Thrown is not null)
                        Count($"threw {result.Thrown.GetType().Name}");

                    else if (result.Info?.IsTimeout != false)
                        Count("came back as a timeout");

                    else if (result.Elapsed >= Deadline / 2)
                        Count("failed, but not at once");

                    if (reconnected)
                        Count("connected again while being disposed");

                }

            }

            Assert.That(wrong,
                        Is.Empty,
                        $"of {rounds * width} queries abandoned, " +
                        String.Join(", ", wrong.Select(kv => $"{kv.Value} {kv.Key}")));

        }

        #endregion


        #region AQueryOverUDP_EndsWithItsClient()

        [Test]
        public async Task AQueryOverUDP_EndsWithItsClient()
        {

            using var server  = new UdpClient(new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            var       port    = IPPort.Parse(((IPEndPoint) server.Client.LocalEndPoint!).Port);
            var       arrived = server.ReceiveAsync();

            var client = new DNSUDPClient(IPv4Address.Localhost, Port: port, QueryTimeout: Deadline);

            AssertAFailureAtOnce(await Abandon(client, arrived));

        }

        #endregion

        #region AQueryOverTCP_EndsWithItsClient()

        [Test]
        public async Task AQueryOverTCP_EndsWithItsClient()
        {

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var arrived  = FirstByte(listener);

            try
            {

                var port    = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);
                var client  = new DNSTCPClient(IPv4Address.Localhost, Port: port, QueryTimeout: Deadline);

                AssertAFailureAtOnce(await Abandon(client, arrived));

            }
            finally
            {
                await Stop(listener, arrived);
            }

        }

        #endregion

        #region AQueryOverHTTPS_EndsWithItsClient()

        [Test]
        public async Task AQueryOverHTTPS_EndsWithItsClient()
        {

            // http:// rather than https://: what is under test is the client's
            // lock and its lifetime, which do not care whether there is TLS.
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var arrived  = FirstByte(listener);

            try
            {

                var port    = ((IPEndPoint) listener.LocalEndpoint).Port;
                var client  = new DNSHTTPSClient(URL.Parse($"http://127.0.0.1:{port}/dns-query"), QueryTimeout: Deadline);

                AssertAFailureAtOnce(await Abandon(client, arrived));

            }
            finally
            {
                await Stop(listener, arrived);
            }

        }

        #endregion


        #region ManyQueriesOverTCP_EndWithTheirClients()

        [Test]
        public Task ManyQueriesOverTCP_EndWithTheirClients()

            => AbandonMany(port => new DNSTCPClient(IPv4Address.Localhost, Port: IPPort.Parse(port), QueryTimeout: Deadline));

        #endregion

        #region ManyQueriesOverHTTPS_EndWithTheirClients()

        [Test]
        public Task ManyQueriesOverHTTPS_EndWithTheirClients()

            => AbandonMany(port => new DNSHTTPSClient(URL.Parse($"http://127.0.0.1:{port}/dns-query"), QueryTimeout: Deadline));

        #endregion


        #region TwoTypesOverTCP_KeepToOneDeadline()

        [Test]
        public async Task TwoTypesOverTCP_KeepToOneDeadline()
        {

            // Two queries, one connection, one at a time - and one deadline. The
            // second used to begin its own once the first was done with the
            // connection: 2.0 s for a 1 s query.
            var deadline = TimeSpan.FromSeconds(1);

            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var arrived  = FirstByte(listener);

            try
            {

                var port = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);

                await using var client = new DNSTCPClient(IPv4Address.Localhost, Port: port, QueryTimeout: deadline);

                var clock = Stopwatch.StartNew();
                var info  = await client.Query(DNSServiceName.Parse("two.example."),
                                               [ DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA ],
                                               deadline);
                clock.Stop();

                Assert.Multiple(() => {
                    Assert.That(info.IsTimeout, Is.True,                                           "neither type was answered");
                    Assert.That(clock.Elapsed,  Is.LessThan(deadline + TimeSpan.FromSeconds(0.5)), "and that was known once, at the deadline");
                });

            }
            finally
            {
                await Stop(listener, arrived);
            }

        }

        #endregion


        #region (private) FirstByte(Listener)

        /// <summary>
        /// Accept one connection and read until the first byte arrives; the
        /// connection is then kept open, unanswered, until the test stops it.
        /// </summary>
        private static async Task<TcpClient> FirstByte(TcpListener Listener)
        {
            var connection = await Listener.AcceptTcpClientAsync();
            await connection.GetStream().ReadExactlyAsync(new Byte[1]);
            return connection;
        }

        #endregion

        #region (private) Stop(Listener, Arrived)

        private static async Task Stop(TcpListener Listener, Task<TcpClient> Arrived)
        {

            Listener.Stop();

            try
            {
                (await Arrived.WaitAsync(TimeSpan.FromSeconds(1))).Dispose();
            }
            catch
            { }

        }

        #endregion

    }

}
