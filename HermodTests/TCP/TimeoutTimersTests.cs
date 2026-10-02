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
using System.Net.Security;
using System.Net.Sockets;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// The timeout of a TLS handshake, and that of a connect, end with the
    /// handshake or the connect, and leave no timer running.
    /// </summary>
    /// <remarks>
    /// Both raced the work against a Task.Delay of the timeout, with
    /// Task.WhenAny, and nothing cancelled the delay when the work won. Its
    /// timer then stayed in the timer queue for the whole timeout: 30 seconds
    /// for every TLS connection a server took, by default, and 5 seconds for
    /// every connect of a TCP client, whether the connect went through or not.
    /// Under load that comes to connections per second times the timeout: a
    /// server taking a hundred TLS connections a second kept 3,000 of them.
    ///
    /// Timer.ActiveCount counts the timers of the whole process, and the rest
    /// of a test run goes on ticking beside the handshakes or connects being
    /// counted. So twenty of them are made, and every check parts at half of
    /// them: what each one leaves behind stands out from the few timers that
    /// anything else starts or stops meanwhile. The timeouts are long, so that
    /// a timer left behind is still running when it is counted.
    /// </remarks>
    [TestFixture]
    public class TimeoutTimersTests
    {

        #region Data

        /// <summary>
        /// How many handshakes or connects are counted.
        /// </summary>
        private const           Int32     Operations   = 20;

        /// <summary>
        /// The timeout of the handshakes and connects counted.
        /// </summary>
        private static readonly TimeSpan  LongTimeout  = TimeSpan.FromMinutes(5);

        /// <summary>
        /// How long a test waits for the server to be done with a connection.
        /// </summary>
        private static readonly TimeSpan  Patience     = TimeSpan.FromSeconds(10);

        #endregion

        #region (private static) SelfSignedServerCertificate()

        /// <summary>
        /// A certificate for localhost, signed by itself.
        /// </summary>
        private static X509Certificate2 SelfSignedServerCertificate()
        {

            using var key = RSA.Create(2048);

            var request = new CertificateRequest("CN=localhost",
                                                 key,
                                                 HashAlgorithmName.SHA256,
                                                 RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid("1.3.6.1.5.5.7.3.1") ], false));

            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());

            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),
                                                             DateTimeOffset.UtcNow.AddDays(1));

            // Through PKCS#12, or the key stays ephemeral and SslStream on
            // Windows refuses it.
            return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);

        }

        #endregion

        #region (private) TLSServer

        /// <summary>
        /// A TCP server that does TLS and hangs up once its handshake is done,
        /// and counts what becomes of its connections.
        /// </summary>
        private sealed class TLSServer : IAsyncDisposable
        {

            public TCPServer                    Server      { get; }
            public X509Certificate2             Certificate { get; } = SelfSignedServerCertificate();

            public Int32                        Accepted    => accepted;
            public ConcurrentQueue<Exception>   Failures    { get; } = new();
            public SemaphoreSlim                Closed      { get; } = new (0);

            private Int32 accepted;
            private readonly ConcurrentDictionary<String, Byte> closed = new();

            public TLSServer()
            {

                Server = new TCPServer(
                             IPAddress:                  IPv4Address.Localhost,
                             TCPPort:                    IPPort.Zero,
                             // The timeout of every TLS handshake the server does.
                             ReceiveTimeout:             LongTimeout,
                             ServerCertificateSelector:  (tcpServer, tcpClient) => Certificate,
                             // Not the default, the two sockets: a client's port
                             // may come round again.
                             ConnectionIdBuilder:        (sender, timestamp, localSocket, remoteSocket) => Guid.NewGuid().ToString(),
                             DisableMaintenanceTasks:    true,
                             DisableWardenTasks:         true
                         );

                Server.OnNewTCPConnection     += (_, _, _, _, _, _)         => { Interlocked.Increment(ref accepted); return Task.CompletedTask; };
                Server.OnTCPConnectionFailed  += (_, _, _, _, _, exception) => { Failures.Enqueue(exception);       return Task.CompletedTask; };

                // Each connection is reported closed once, when the server is done
                // with it - one whose handshake failed as well. Counted by
                // connection all the same: reported twice, as every connection
                // was before ConnectionClosedTests, ten connections would pass
                // for twenty, while the handshakes of the other ten were still
                // under way, with their timers running.
                Server.OnTCPConnectionClosed  += (_, _, _, _, connectionId, _) => {
                                                     if (closed.TryAdd(connectionId, 0))
                                                         Closed.Release();
                                                     return Task.CompletedTask;
                                                 };

            }

            /// <summary>
            /// Wait until the server has closed the given number of connections.
            /// </summary>
            public async Task HasClosed(Int32 Connections)
            {
                for (var i = 0; i < Connections; i++)
                    Assert.That(await Closed.WaitAsync(Patience), Is.True, $"the server closed {i} of {Connections} connections in time");
            }

            public async ValueTask DisposeAsync()
            {
                await Server.DisposeAsync();
                Certificate.Dispose();
                Closed.Dispose();
            }

        }

        #endregion

        #region (private static) HandshakeAsync(Server)

        /// <summary>
        /// Open a TLS connection, and wait until the server is done with it.
        /// </summary>
        private static async Task HandshakeAsync(TLSServer Server)
        {

            using (var client = new TcpClient())
            {

                await client.ConnectAsync(System.Net.IPAddress.Loopback, Server.Server.TCPPort.ToUInt16());

                using var tls = new SslStream(
                                    client.GetStream(),
                                    leaveInnerStreamOpen:               false,
                                    // Signed by itself and trusted nowhere:
                                    // whether it validates is not the question.
                                    userCertificateValidationCallback:  (sender, certificate, chain, errors) => true
                                );

                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
                              TargetHost                      = "localhost",
                              CertificateRevocationCheckMode  = X509RevocationMode.NoCheck
                          });

                // Until the server hangs up, which it does once its own part of
                // the handshake is done. A client that closed first could have
                // the connection reset under that part.
                try
                {
                    while (await tls.ReadAsync(new Byte[1]) > 0) { }
                }
                catch (IOException)
                { }

            }

            await Server.HasClosed(1);

        }

        #endregion


        #region ATLSHandshakeLeavesNoTimerRunning()

        /// <summary>
        /// The handshake wins, and the delay it was raced against ends with it.
        /// </summary>
        [Test]
        public async Task ATLSHandshakeLeavesNoTimerRunning()
        {

            await using var server = new TLSServer();

            await server.Server.Start();

            // One beforehand: what the first handshake of a process starts once
            // for the whole process is not what this counts.
            await HandshakeAsync(server);

            var before = Timer.ActiveCount;

            for (var i = 0; i < Operations; i++)
                await HandshakeAsync(server);

            var left = Timer.ActiveCount - before;

            Assert.Multiple(() => {

                // Or the handshakes failed early, before anything was raced.
                Assert.That(server.Failures, Is.Empty,                      "connections the server could not handle");
                Assert.That(server.Accepted, Is.EqualTo(Operations + 1),    "TLS handshakes the server completed");

                Assert.That(left,            Is.LessThan(Operations / 2),   $"timers still running after {Operations} TLS handshakes");

            });

        }

        #endregion

        #region ATLSHandshakeRunsItsTimeoutUntilTheClientGivesUp()

        /// <summary>
        /// A client that never says hello: the server's handshake waits for it,
        /// with its timeout running, until the client hangs up. Then the
        /// handshake fails, and the delay it was raced against ends with it.
        /// </summary>
        /// <remarks>
        /// The first check is what makes the second one mean something: without
        /// a timer while the handshakes wait, nothing could be left behind.
        /// </remarks>
        [Test]
        public async Task ATLSHandshakeRunsItsTimeoutUntilTheClientGivesUp()
        {

            await using var server = new TLSServer();

            await server.Server.Start();

            async Task<TcpClient> SayNothing()
            {
                var client = new TcpClient();
                await client.ConnectAsync(System.Net.IPAddress.Loopback, server.Server.TCPPort.ToUInt16());
                return client;
            }

            // One beforehand, as above.
            using (await SayNothing())
            { }

            await server.HasClosed(1);

            var before   = Timer.ActiveCount;
            var clients  = new List<TcpClient>();

            for (var i = 0; i < Operations; i++)
                clients.Add(await SayNothing());

            // The server takes the connections on a thread of its own, and makes
            // a handshake's timer as it takes its connection.
            var waited = Stopwatch.StartNew();

            while (Timer.ActiveCount - before < Operations && waited.Elapsed < Patience)
                await Task.Delay(10);

            var alive = Timer.ActiveCount - before;

            foreach (var client in clients)
                client.Dispose();

            await server.HasClosed(Operations);

            var left = Timer.ActiveCount - before;

            Assert.Multiple(() => {

                Assert.That(alive, Is.GreaterThanOrEqualTo(Operations / 2), $"timers running while {Operations} TLS handshakes waited for a client hello");
                Assert.That(left,  Is.LessThan(Operations / 2),             $"timers still running after {Operations} TLS handshakes failed");

            });

        }

        #endregion


        #region AConnectLeavesNoTimerRunning()

        /// <summary>
        /// The connect wins, and the delay it was raced against ends with it.
        /// </summary>
        [Test]
        public async Task AConnectLeavesNoTimerRunning()
        {

            // Never accepts: the operating system completes a connect on its
            // own, as long as somebody listens.
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            try
            {

                var url = URL.Parse($"tcp://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}");

                // Lent to every client: one that makes its own also makes a timer
                // for its cache, and that is not what this counts.
                await using var dnsClient = new DNSClient(ManualDNSServers: []);

                async Task ConnectAsync()
                {

                    var client = new TCPClient(url,
                                               ConnectTimeout:  LongTimeout,
                                               DNSClient:       dnsClient);

                    var result = await client.ConnectAsync();

                    await client.DisposeAsync();

                    Assert.That(result.IsSuccess, Is.True, $"the connect failed: {result}");

                }

                // One beforehand, as above.
                await ConnectAsync();

                var before = Timer.ActiveCount;

                for (var i = 0; i < Operations; i++)
                    await ConnectAsync();

                Assert.That(Timer.ActiveCount - before, Is.LessThan(Operations / 2), $"timers still running after {Operations} connects");

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

    }

}
