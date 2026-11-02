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

using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Tunnels;

/// <summary>
/// A tunnel lets go of its stream once both its directions are over, on both sides, and so does
/// an Extended CONNECT that was turned down. Both connections used to keep them for as long as
/// they lived: the server every tunnel, and every turned-down CONNECT, whose client never closed
/// its side; the client every CONNECT, as it could not forget one.
/// </summary>
[TestFixture]
public class Http3TunnelReleaseTests
{

    #region (helpers)

    private static readonly CancellationToken None = CancellationToken.None;

    private sealed class Pair : IDisposable
    {

        public required ServerCertificate      Certificate   { get; init; }
        public required Http3ClientConnection  Client        { get; init; }
        public required Http3ServerConnection  Server        { get; init; }
        public          Http3Tunnel?           ServerTunnel  { get; set; }

        public void Pump(int rounds = 1)
        {
            for (int round = 0; round < rounds; round++)
            {
                Client.CheckTimeouts();
                Server.CheckTimeouts();
                foreach (byte[] datagram in Client.GetDatagramsToSend())
                    Server.ProcessDatagram(datagram);
                foreach (byte[] datagram in Server.GetDatagramsToSend())
                    Client.ProcessDatagram(datagram);
            }
        }

        /// <summary>
        /// Sends an Extended CONNECT and pumps until its response is there.
        /// </summary>
        public (ulong StreamId, int Status, Http3Tunnel? Tunnel) Connect(string protocol)
        {
            ulong id = Client.SendExtendedConnect("localhost", "/bytes", protocol, []);
            int status = 0;
            Http3Tunnel? tunnel = null;
            for (int round = 0; round < 20 && !Client.TryGetConnectResponse(id, out status, out _, out tunnel); round++)
                Pump();
            Assert.That(status, Is.Not.Zero, "the CONNECT's response");
            return (id, status, tunnel);
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
            Certificate.Dispose();
        }

    }

    /// <summary>
    /// The server accepts :protocol "bytes" and turns everything else down with a 404.
    /// </summary>
    private static Pair OpenPair()
    {

        var cert = ServerCertificate.CreateSelfSigned("localhost");
        Pair? pair = null;

        var server = new Http3ServerConnection(cert,
                                               _ => new Http3Response { Status = 200, Body = [] },
                                               connectHandler: request => request.Protocol == "bytes"
                                                   ? new Http3ConnectResult { Status = 200, OnTunnel = tunnel => pair!.ServerTunnel = tunnel }
                                                   : new Http3ConnectResult { Status = 404 });

        var client = new Http3ClientConnection("localhost",
                                               certificateValidation: new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] });

        pair = new Pair { Certificate = cert, Client = client, Server = server };

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            pair.Pump();
        client.InitializeHttp3();
        pair.Pump(5);
        Assert.That(client.ServerEnablesConnectProtocol, Is.True, "the server's SETTINGS");
        return pair;

    }

    private static async Task<string> ReadToEnd(Pair pair, Http3Tunnel tunnel)
    {
        var received = new List<byte>();
        Task<byte[]?>? read = null;
        for (int round = 0; round < 30; round++)
        {
            pair.Pump();
            read ??= tunnel.ReadAsync(None);
            if (!read.IsCompleted)
                continue; // nothing yet: a later pump completes the waiting read
            byte[]? chunk = await read;
            read = null;
            if (chunk is null)
                return Encoding.UTF8.GetString([.. received]);
            received.AddRange(chunk);
        }
        throw new AssertionException("the tunnel never ended");
    }

    #endregion


    [Test]
    public async Task AClosedTunnel_IsLetGo_OnBothSides()
    {

        using Pair pair = OpenPair();
        (ulong id, int status, Http3Tunnel? clientTunnel) = pair.Connect("bytes");
        Assert.That(status, Is.EqualTo(200));
        Assert.That(clientTunnel, Is.Not.Null);
        Assert.That(pair.ServerTunnel, Is.Not.Null);

        await pair.ServerTunnel!.WriteAsync(Encoding.UTF8.GetBytes("bye"), None);
        pair.ServerTunnel.Complete();
        Assert.That(await ReadToEnd(pair, clientTunnel!), Is.EqualTo("bye"));
        clientTunnel!.Complete();
        pair.Pump(5);

        // The server's side is the server's own: it lets go once both directions are over.
        Assert.That(pair.Server.TrackedRequestsForTest, Is.Zero, "the server's request states");
        Assert.That(pair.Server.HasPendingRequests, Is.False);

        // The client's side is the application's: it stays until forgotten, and answers until then.
        Assert.That(pair.Client.TryGetConnectResponse(id, out _, out _, out _), Is.True);
        pair.Client.ForgetRequest(id);
        pair.Pump(5);

        Assert.That(pair.Client.TrackedRequestsForTest, Is.Zero, "the client's request states");
        Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.True, "the server's QUIC stream");
        Assert.That(pair.Client.Quic.IsStreamClosed(id), Is.True, "the client's QUIC stream");

        // Nothing puts a write on the stream any more: it fails rather than vanishes.
        Assert.That(() => pair.ServerTunnel.WriteAsync([1], None), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(() => clientTunnel.WriteAsync([1], None), Throws.InstanceOf<OperationCanceledException>());

    }

    [Test]
    public async Task AForgottenTunnel_GoesOnUntilItEnds()
    {

        using Pair pair = OpenPair();
        (ulong id, _, Http3Tunnel? clientTunnel) = pair.Connect("bytes");

        pair.Client.ForgetRequest(id);
        pair.Pump(5);
        Assert.That(pair.Client.TrackedRequestsForTest, Is.EqualTo(1), "a tunnel still open stays");

        await clientTunnel!.WriteAsync(Encoding.UTF8.GetBytes("still here"), None);
        clientTunnel.Complete();
        Assert.That(await ReadToEnd(pair, pair.ServerTunnel!), Is.EqualTo("still here"));
        pair.ServerTunnel!.Complete();
        Assert.That(await ReadToEnd(pair, clientTunnel), Is.Empty);
        pair.Pump(5);

        Assert.That(pair.Client.TrackedRequestsForTest, Is.Zero);
        Assert.That(pair.Client.Quic.IsStreamClosed(id), Is.True);
        Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.True);

    }

    [Test]
    public async Task ATunnelResetByThePeer_IsLetGo_EvenWithoutAStopSending()
    {

        // A reset is the tunnel's RST (RFC 9220 §3), which ends both directions. Hermod's own
        // Abort sends a STOP_SENDING along, which resets the client's side by itself; a peer that
        // sends the reset alone left the client's side open for good.
        using Pair pair = OpenPair();
        (ulong id, _, Http3Tunnel? clientTunnel) = pair.Connect("bytes");

        pair.Server.Quic.Streams[id].Reset(Http3Error.RequestCancelled);
        Assert.That(await ReadToEnd(pair, clientTunnel!), Is.Empty);
        pair.Client.ForgetRequest(id);
        pair.Pump(5);

        Assert.That(pair.Client.TrackedRequestsForTest, Is.Zero, "the client's request states");
        Assert.That(pair.Server.TrackedRequestsForTest, Is.Zero, "the server's request states");
        Assert.That(pair.Client.Quic.IsStreamClosed(id), Is.True, "the client's QUIC stream");
        Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.True, "the server's QUIC stream");

    }

    [Test]
    public void ATurnedDownConnect_LetsGoOfItsStream()
    {

        using Pair pair = OpenPair();
        (ulong id, int status, Http3Tunnel? tunnel) = pair.Connect("something-else");
        Assert.That(status, Is.EqualTo(404));
        Assert.That(tunnel, Is.Null);
        pair.Pump(3);

        // The server stops reading what the client would still send (RFC 9114 §4.1), and its
        // answer is complete: nothing of it is pending.
        Assert.That(pair.Client.Quic.Streams[id].Send.IsReset, Is.True, "the server's STOP_SENDING");
        Assert.That(pair.Client.Quic.Streams[id].Send.ResetErrorCode, Is.EqualTo(Http3Error.NoError));
        Assert.That(pair.Server.HasPendingRequests, Is.False);
        Assert.That(pair.Server.TrackedRequestsForTest, Is.Zero, "the server's request states");

        pair.Client.ForgetRequest(id);
        pair.Pump(3);
        Assert.That(pair.Client.Quic.IsStreamClosed(id), Is.True, "the client's QUIC stream");
        Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.True, "the server's QUIC stream");

    }

}
