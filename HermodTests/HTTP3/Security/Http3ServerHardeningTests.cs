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

using System.Security.Cryptography;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Security;

/// <summary>
/// Hardening of the server facade: a bounded number of connections, a demux that does not get more
/// expensive with every client, timers that keep running under steady traffic, and a limit on
/// buffered request bodies.
/// <para>
/// The first five tests speak over real UDP sockets and keep the real clock, because their subject
/// (a connection cap, a third client running into its own timeout, the server's own timer schedule)
/// is made of real timeouts.
/// The last two run both connections in process against a <see cref="FakeTimeProvider"/> — see the
/// comment on <see cref="RoundDuration"/> for why that is a requirement rather than a taste.
/// </para>
/// </summary>
[TestFixture]
public class Http3ServerHardeningTests
{
    [Test]
    public async Task ConnectionLimit_RefusesFurtherConnections_AndKeepsServingTheExistingOnes()
    {
        // Without a cap every Initial packet creates a connection — including a certificate
        // signature. An attacker gets unbounded memory and CPU out of that.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(0,
            () => new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = "ok"u8.ToArray() }),
            maxConnections: 2);
        server.Start();

        await using var first = new Http3Client("localhost", server.Port, validation);
        await first.ConnectAsync(TimeSpan.FromSeconds(10));
        await using var second = new Http3Client("localhost", server.Port, validation);
        await second.ConnectAsync(TimeSpan.FromSeconds(10));
        Assert.That(server.ConnectionCount, Is.EqualTo(2));

        // The third one must not get through — dropped silently, so it runs into its timeout.
        await using var third = new Http3Client("localhost", server.Port, validation);
        await Assert.ThrowsAsync<TimeoutException>(async () => await third.ConnectAsync(TimeSpan.FromSeconds(2)));

        Assert.That(server.ConnectionCount, Is.EqualTo(2), "No connection may be created beyond the limit.");
        Assert.That(server.ConnectionsRefused, Is.GreaterThan(0), "And the rejection is counted.");

        // Decisive: the existing connections keep working.
        Http3Response response = await first.GetAsync("/still-alive").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task ManyConnections_AreDemultiplexedWithoutAScanPerDatagram()
    {
        // The index replaces the linear scan over all connections. This test would also pass with
        // the scan — it guards the FUNCTION (correct routing with many clients), while the index is
        // what keeps the per-packet cost independent of the client count.
        const int clients = 12;
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes(request.Path) }, port: 0);
        server.Start();

        var connections = new List<Http3Client>();
        try
        {
            for (int i = 0; i < clients; i++)
            {
                var client = new Http3Client("localhost", server.Port, validation);
                await client.ConnectAsync(TimeSpan.FromSeconds(10));
                connections.Add(client);
            }
            Assert.That(server.ConnectionCount, Is.EqualTo(clients));

            // Every client must get exactly ITS answer — proof the routing stays correct.
            Http3Response[] responses = await Task.WhenAll(
                connections.Select((c, i) => c.GetAsync($"/client-{i}").WaitAsync(TimeSpan.FromSeconds(15))));
            for (int i = 0; i < clients; i++)
                Assert.That(responses[i].BodyText, Is.EqualTo($"/client-{i}"),
                            "A datagram was routed to the wrong connection.");
        }
        finally
        {
            foreach (Http3Client client in connections)
                await client.DisposeAsync();
        }
    }

    [Test]
    public async Task UnderSteadyTraffic_AFinishedHandlersResponseIsStillSent()
    {
        // The server ran its connections' timers only after 20 ms without any datagram, from
        // anyone. Those timers also send what a handler finished in the background, so while
        // datagrams kept arriving, the response waited for the client's next packet. One datagram
        // every two milliseconds that the server cannot even place was enough. Http3Client hides
        // this: its own probes reach the server soon enough. A client that only listens does not.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert, async (_, token) =>
        {
            await Task.Delay(200, token).ConfigureAwait(false);
            return new Http3Response { Status = 200, Body = "late"u8.ToArray() };
        }, port: 0);
        server.Start();

        using var client = new HandDrivenClient(server.Port, validation);
        client.Connect();

        using var chatter = new Chatter(server.Port);
        ulong stream = client.Connection.SendRequest(Http3Request.Get("localhost", "/late"));
        client.Send(); // the request, and from here on nothing

        Http3Response? response = null;
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (response is null && waited.Elapsed < TimeSpan.FromSeconds(5))
        {
            client.Listen(TimeSpan.FromMilliseconds(20));
            client.Connection.TryGetResponse(stream, out response);
        }

        Assert.That(response?.BodyText, Is.EqualTo("late"), "The finished response was never sent.");
        Assert.That(chatter.Sent, Is.GreaterThan(50), "The traffic has to have been steady meanwhile.");
    }

    [Test]
    public async Task UnderSteadyTraffic_AVanishedClientIsStillLetGo()
    {
        // The same timers drop a connection whose idle timeout has passed. While they stood still,
        // every client that vanished stayed, until the connection limit turned new ones away.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(0, () => new Http3ServerConnection(cert,
            _ => new Http3Response { Status = 204 },
            transportParameters: new TransportParameters { MaxIdleTimeoutMs = 300 }));
        server.Start();

        using var chatter = new Chatter(server.Port);
        using (var client = new HandDrivenClient(server.Port, validation))
        {
            client.Connect();
            Assert.That(server.ConnectionCount, Is.EqualTo(1));
        } // gone without a word

        Assert.That(await HoldsNoConnectionWithin(server, TimeSpan.FromSeconds(5)), Is.True,
                    "The vanished client's connection outlived its idle timeout.");
        Assert.That(chatter.Sent, Is.GreaterThan(50), "The traffic has to have been steady meanwhile.");
    }

    [Test]
    public async Task UnderSteadyTraffic_AClosedConnectionIsStillLetGo()
    {
        // And they end the draining period after a goodbye (RFC 9000 §10.2).
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert, _ => new Http3Response { Status = 204 }, port: 0);
        server.Start();

        using var chatter = new Chatter(server.Port);
        await using (var client = new Http3Client("localhost", server.Port, validation))
        {
            await client.ConnectAsync(TimeSpan.FromSeconds(10));
            Assert.That(server.ConnectionCount, Is.EqualTo(1));
        } // says goodbye with CONNECTION_CLOSE

        Assert.That(await HoldsNoConnectionWithin(server, TimeSpan.FromSeconds(5)), Is.True,
                    "The closed connection is still held.");
        Assert.That(chatter.Sent, Is.GreaterThan(50), "The traffic has to have been steady meanwhile.");
    }


    #region Steady traffic (real sockets)

    private static async Task<bool> HoldsNoConnectionWithin(Http3Server server, TimeSpan timeout)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (server.ConnectionCount > 0 && waited.Elapsed < timeout)
            await Task.Delay(20);
        return server.ConnectionCount == 0;
    }

    /// <summary>
    /// Sends a datagram the server cannot place every two milliseconds until disposed: a short
    /// header for a connection ID it never issued, which it drops without an answer. Paced by
    /// spinning, because a sleep on Windows lasts a whole 15.6 ms timer period, too close to the
    /// server's 20 ms tick to keep it from ever passing.
    /// </summary>
    private sealed class Chatter : IDisposable
    {
        private readonly System.Net.Sockets.UdpClient _udp = new();
        private readonly Thread _thread;
        private volatile bool _stop;
        private int _sent;

        public Chatter(int port)
        {
            byte[] datagram = new byte[40];
            RandomNumberGenerator.Fill(datagram);
            datagram[0] = 0x40; // short header, fixed bit set
            var server = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, port);

            _thread = new Thread(() =>
            {
                var pace = System.Diagnostics.Stopwatch.StartNew();
                while (!_stop)
                {
                    _udp.Send(datagram, datagram.Length, server);
                    Interlocked.Increment(ref _sent);
                    pace.Restart();
                    while (pace.Elapsed < TimeSpan.FromMilliseconds(2))
                        Thread.SpinWait(50);
                }
            }) { IsBackground = true, Name = "chatter" };
            _thread.Start();
        }

        public int Sent => Volatile.Read(ref _sent);

        public void Dispose()
        {
            _stop = true;
            _thread.Join();
            _udp.Dispose();
        }
    }

    /// <summary>
    /// A client on a socket of its own that sends only when told to, so it can fall silent, which
    /// Http3Client never does: its pump answers and probes on its own.
    /// </summary>
    private sealed class HandDrivenClient(int port, CertificateValidationOptions validation) : IDisposable
    {
        private readonly System.Net.Sockets.UdpClient _udp = new(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        private readonly System.Net.IPEndPoint _server = new(System.Net.IPAddress.Loopback, port);

        public Http3ClientConnection Connection { get; } = new("localhost", certificateValidation: validation);

        public void Connect()
        {
            Connection.Start();
            var waited = System.Diagnostics.Stopwatch.StartNew();
            while (!Connection.HandshakeConfirmed && waited.Elapsed < TimeSpan.FromSeconds(10))
            {
                Connection.CheckTimeouts();
                Send();
                Listen(TimeSpan.FromMilliseconds(10));
            }
            Assert.That(Connection.HandshakeConfirmed, Is.True, "The QUIC handshake did not confirm.");
            Connection.InitializeHttp3();
            Send();
        }

        public void Send()
        {
            foreach (byte[] datagram in Connection.GetDatagramsToSend())
                _udp.Send(datagram, datagram.Length, _server);
        }

        /// <summary>
        /// Takes in what arrives within <paramref name="window"/>, and answers none of it.
        /// </summary>
        public void Listen(TimeSpan window)
        {
            var listened = System.Diagnostics.Stopwatch.StartNew();
            while (listened.Elapsed < window)
            {
                if (!_udp.Client.Poll(1000, System.Net.Sockets.SelectMode.SelectRead))
                    continue;
                System.Net.IPEndPoint? from = null;
                Connection.ProcessDatagram(_udp.Receive(ref from));
            }
        }

        public void Dispose()
        {
            _udp.Dispose();
            Connection.Dispose();
        }
    }

    #endregion

    [Test]
    public void BufferedBodyAboveTheLimit_Yields413()
    {
        // A buffered body grows in memory ⇒ refuse it early instead of collecting it to the end.
        var clock = new FakeTimeProvider();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert,
            request => new Http3Response { Status = 200, Body = request.Body },
            maxRequestBodySize: 32 * 1024,
            timeProvider:       clock);
        using var client = new Http3ClientConnection("localhost",
                                                     certificateValidation: validation,
                                                     timeProvider:          clock);
        Handshake(client, server, clock);

        ulong big = client.SendRequest(Http3Request.Post("localhost", "/upload",
            RandomNumberGenerator.GetBytes(200_000), "application/octet-stream"));
        Http3Response response = Run(client, server, clock, big);

        Assert.That(response.Status, Is.EqualTo(413), "Oversized body ⇒ 413 Content Too Large.");

        // And the connection stays usable for a request within the limit.
        byte[] small = RandomNumberGenerator.GetBytes(1000);
        ulong ok = client.SendRequest(Http3Request.Post("localhost", "/upload", small, "application/octet-stream"));
        Assert.That(Run(client, server, clock, ok).Body, Is.EqualTo(small));
    }

    [Test]
    public void StreamingHandlers_AreExemptFromTheBufferedLimit()
    {
        // A streaming handler decides for itself how much it consumes — the buffered limit must not
        // cut off exactly the case that was built for large uploads.
        var clock = new FakeTimeProvider();
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert,
            async (_, body, token) =>
            {
                long total = 0;
                byte[] buffer = new byte[8192];
                while (await body.ReadAsync(buffer, token).ConfigureAwait(false) is > 0 and var read)
                    total += read;
                return new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes(total.ToString()) };
            },
            maxRequestBodySize: 32 * 1024,
            timeProvider:       clock);
        using var client = new Http3ClientConnection("localhost",
                                                     certificateValidation: validation,
                                                     timeProvider:          clock);
        Handshake(client, server, clock);

        ulong stream = client.SendRequest(Http3Request.Post("localhost", "/upload",
            RandomNumberGenerator.GetBytes(200_000), "application/octet-stream"));
        Http3Response response = Run(client, server, clock, stream);

        Assert.That(response.Status,   Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("200000"), "The streaming handler sees the whole body.");
    }


    #region In-process pump (fake clock)

    /// <summary>
    /// How far the fake clock moves per pump round.
    /// <para>
    /// This is a requirement rather than a preference, and it was paid for. Until 2026-09-23 the two
    /// in-process tests above took the SYSTEM clock. That night the HTTP/3 repository's
    /// against-hermod-master leg failed on StreamingHandlers_AreExemptFromTheBufferedLimit with a
    /// bare NullReferenceException, and a rerun of the identical commit came back green.
    /// </para>
    /// <para>
    /// Instrumented, the exchange needs 65–80 of the <see cref="MaxRounds"/> rounds. The budget was
    /// never close, so a run that exhausts it has not gone SLOW — it has stopped. What can stop it
    /// is the clock: <c>CheckTimeouts()</c> drives QUIC's idle and loss-detection timers off
    /// whatever <see cref="TimeProvider"/> the connection was handed, and on a loaded CI runner a
    /// stall of a second or two between two microsecond-long rounds fires a timeout that never
    /// fires on an idle developer machine. Every round after that pumps a dead connection.
    /// </para>
    /// <para>
    /// Every other in-process fixture under <c>HermodTests/HTTP3</c> already injects a
    /// <see cref="FakeTimeProvider"/>; this one was the exception. LossyNetworkTests records the
    /// mirror image of the same fact: with the real clock, PTO never fires in-process, because the
    /// rounds run in microseconds.
    /// </para>
    /// <para>
    /// 1 ms per round puts the whole <see cref="MaxRounds"/> budget at 4 s of fake time, well inside
    /// the 30 s default MaxIdleTimeoutMs. The budget is therefore always reached BEFORE any idle
    /// timeout, which leaves an exhausted budget meaning exactly one thing: the exchange stalled.
    /// </para>
    /// </summary>
    private static readonly TimeSpan RoundDuration = TimeSpan.FromMilliseconds(1);

    private const int MaxRounds       = 4000;
    private const int HandshakeRounds = 20;

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server, FakeTimeProvider clock)
    {
        // A fixed step per round, so how long the machine actually took between two rounds cannot
        // reach the connections' timers.
        clock.Advance(RoundDuration);

        client.CheckTimeouts();
        server.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
    }

    /// <summary>
    /// Brings the pair up and asserts that it got there. An unconfirmed handshake used to fall
    /// straight through into InitializeHttp3() and take every later assertion down with it, for a
    /// reason that has nothing to do with request-body limits.
    /// </summary>
    private static void Handshake(Http3ClientConnection client, Http3ServerConnection server, FakeTimeProvider clock)
    {
        client.Start();

        for (int round = 0; round < HandshakeRounds && !client.HandshakeConfirmed; round++)
            Pump(client, server, clock);

        Assert.That(client.HandshakeConfirmed,
                    Is.True,
                    $"The QUIC handshake did not confirm within {HandshakeRounds} pump rounds.");

        client.InitializeHttp3();
    }

    /// <summary>
    /// Pumps until the response for <paramref name="stream"/> arrives, and fails with a diagnosis if
    /// it never does — instead of returning null for the caller to dereference. That bare
    /// NullReferenceException was all the 2026-09-23 nightly left behind, and "the exchange stalled"
    /// and "the status code was wrong" have to read differently.
    /// </summary>
    private static Http3Response Run(Http3ClientConnection client,
                                     Http3ServerConnection server,
                                     FakeTimeProvider      clock,
                                     ulong                 stream)
    {
        Http3Response? response = null;
        int            rounds   = 0;

        while (rounds < MaxRounds && response is null)
        {
            rounds++;
            Pump(client, server, clock);
            client.TryGetResponse(stream, out response);
        }

        Assert.That(response,
                    Is.Not.Null,
                    $"No response on stream {stream} after {MaxRounds} pump rounds " +
                    $"({MaxRounds * RoundDuration.TotalMilliseconds:0} ms of fake time). " +
                    "A healthy exchange here needs well under 100 rounds, so this is a stall " +
                    "rather than a budget that ran out.");

        return response!;
    }

    #endregion

}
