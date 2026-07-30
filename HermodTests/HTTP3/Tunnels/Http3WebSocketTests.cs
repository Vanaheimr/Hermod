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
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Tunnels;

/// <summary>
/// WebSockets over HTTP/3 (RFC 9220): Extended CONNECT (RFC 8441) with :protocol = "websocket",
/// SETTINGS_ENABLE_CONNECT_PROTOCOL (0x08), tunnel bytes in DATA frames (RFC 9114 §4.4) and on top
/// the unchanged RFC 6455 framing (copy from Hermod.HTTP2 — only the namespace was swapped).
/// </summary>
[TestFixture]
public class Http3WebSocketTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Test]
    public void ExtendedConnect_WithoutServerSetting_IsRefusedLocally()
    {
        // A server WITHOUT a connectHandler does not announce the setting ⇒ RFC 8441 §3 MUST NOT.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] });
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        Assert.That(client.ServerEnablesConnectProtocol, Is.False);
        Assert.Throws<InvalidOperationException>(() => client.SendExtendedConnect("localhost", "/chat", "websocket"));
    }

    [Test]
    public void ExtendedConnect_WithoutSetting_IsMalformedOnServer()
    {
        // A raw client ignores the missing setting — the server treats the request as
        // malformed (400 + H3_MESSAGE_ERROR), because the client was NOT allowed to send it (RFC 8441 §3).
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        int handled = 0;
        using var server = new Http3ServerConnection(cert, _ => { handled++; return new Http3Response { Status = 200, Body = [] }; });
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);

        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        QuicStream request = client.OpenBidirectionalStream();
        request.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeConnect("websocket")));

        var received = new List<byte>();
        for (int round = 0; round < 15; round++)
        {
            Pump(client, server);
            received.AddRange(request.Read());
        }

        Assert.That(handled, Is.EqualTo(0));
        Assert.That(Http3Frames.TryReadAll(received.ToArray(), out List<Http3Frame> frames, out _), Is.True);
        Assert.That(QpackDecoder.Decode(frames.First(f => f.Type == Http3FrameType.Headers).Payload.Span, out List<HeaderField> headers), Is.EqualTo(QpackResult.Ok));
        Assert.That(headers.First(h => h.Name == ":status").Value, Is.EqualTo("400"));
        Assert.That(request.PeerStopSendingErrorCode, Is.EqualTo(Http3Error.MessageError));
    }

    [Test]
    public void UnknownProtocol_IsAnsweredWith501()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = WebSocketServerPair(out _);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong streamId = client.SendExtendedConnect("localhost", "/", "unknown-protocol");
        int status = 0;
        Http3Tunnel? tunnel = null;
        for (int round = 0; round < 20 && status == 0; round++)
        {
            Pump(client, server);
            client.TryGetConnectResponse(streamId, out status, out _, out tunnel);
        }

        Assert.That(status, Is.EqualTo(501)); // RFC 9220 §3 SHOULD
        Assert.That(tunnel, Is.Null);
        Assert.That(client.IsClosing, Is.False);
    }

    [Test]
    public void WebSocketEcho_TextBinaryAndCloseHandshake_EndToEnd()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = WebSocketServerPair(out Func<bool> serverLoopEnded);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        (Http3Tunnel tunnel, IReadOnlyList<HeaderField> _) = OpenWebSocket(client, server, offerDeflate: false);
        var ws = new WebSocketConnection(tunnel, WebSocketRole.Client);

        // Text echo.
        ws.SendTextAsync("Hello WebSocket over HTTP/3!", None);
        WebSocketMessage? echo = AwaitMessage(ws, client, server);
        Assert.That(echo, Is.Not.Null);
        Assert.That(echo!.Opcode, Is.EqualTo(WebSocketOpcode.Text));
        Assert.That(Encoding.UTF8.GetString(echo.Payload), Is.EqualTo("Hello WebSocket over HTTP/3!"));

        // Binary echo.
        byte[] binary = [1, 2, 3, 250, 251, 252];
        ws.SendBinaryAsync(binary, None);
        echo = AwaitMessage(ws, client, server);
        Assert.That(echo, Is.Not.Null);
        Assert.That(echo!.Opcode, Is.EqualTo(WebSocketOpcode.Binary));
        Assert.That(echo.Payload, Is.EqualTo(binary));

        // Close handshake (RFC 6455 §5.5.1): the server mirrors the close, both ends end cleanly.
        ws.CloseAsync(1000, "done", None);
        WebSocketMessage? closed = AwaitMessage(ws, client, server);
        Assert.That(closed, Is.Null); // ReceiveAsync returns null after a completed close handshake
        for (int round = 0; round < 10 && !serverLoopEnded(); round++)
            Pump(client, server);
        Assert.That(serverLoopEnded(), Is.True, "The server echo loop must end after the close.");
        Assert.That(client.IsClosing, Is.False);
        Assert.That(server.IsClosing, Is.False);
    }

    [Test]
    public void ATunnelWriteGoesOutEvenWhenThePeerIsSilent()
    {
        // Tunnel writes are queued and drained by the pump, so that an application may write from
        // any thread. The drain used to happen only inside ProcessDatagram — which made outgoing
        // data depend on incoming data: with a quiet peer the write simply sat in the queue.
        // Acknowledgment traffic hid it, because something was always arriving.
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = WebSocketServerPair(out _);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        (Http3Tunnel tunnel, IReadOnlyList<HeaderField> _) = OpenWebSocket(client, server, offerDeflate: false);
        var ws = new WebSocketConnection(tunnel, WebSocketRole.Client);

        // Drain whatever the CONNECT exchange left over, so the next pass starts quiet.
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        Task<WebSocketMessage?> echo = ws.ReceiveAsync(None);
        ws.SendTextAsync("anyone there?", None);

        // Exactly ONE round, and it starts with the client's own timer — nothing has arrived that
        // could have driven the pump. If the queued write only left on an incoming datagram, the
        // server would have nothing to echo and this round would come back empty-handed.
        Pump(client, server);

        Assert.That(echo.IsCompleted, Is.True,
                    "A queued tunnel write must not wait for the peer to send something first.");
        Assert.That(Encoding.UTF8.GetString(echo.Result!.Payload), Is.EqualTo("anyone there?"));
    }

    [Test]
    public void PerMessageDeflate_IsNegotiatedAndRoundTrips()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = WebSocketServerPair(out _);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        (Http3Tunnel tunnel, IReadOnlyList<HeaderField> headers) = OpenWebSocket(client, server, offerDeflate: true);
        Assert.That(headers, Has.Some.Matches<HeaderField>(h => h.Name == "sec-websocket-extensions" &&
                                      h.Value.Contains(WebSocketDeflate.ExtensionName)));
        var ws = new WebSocketConnection(tunnel, WebSocketRole.Client, PerMessageDeflate: true);

        string text = string.Concat(Enumerable.Repeat("Compressible content! ", 200)); // ~4.4 KB, compresses well
        ws.SendTextAsync(text, None);
        WebSocketMessage? echo = AwaitMessage(ws, client, server);
        Assert.That(echo, Is.Not.Null);
        Assert.That(Encoding.UTF8.GetString(echo!.Payload), Is.EqualTo(text));
    }

    [Test]
    public void NonDataFrame_OnEstablishedTunnel_IsFrameUnexpected()
    {
        // Raw client: establish the tunnel, then illicitly send a HEADERS frame (RFC 9114 §4.4).
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        using var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] },
            connectHandler: request => new Http3ConnectResult { Status = 200, OnTunnel = _ => { } });
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);

        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));
        QuicStream request = client.OpenBidirectionalStream();
        request.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeConnect("websocket")));
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        request.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeConnect("websocket"))); // forbidden
        for (int round = 0; round < 10 && !server.IsClosing; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True);
        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.ErrorCode, Is.EqualTo(Http3Error.FrameUnexpected));
    }

    [Test]
    public void Validator_ProtocolPseudoHeaderRules()
    {
        // :protocol on a non-CONNECT ⇒ malformed (RFC 8441 §4).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "h"), new(":path", "/"), new(":protocol", "websocket")]), Is.Not.Null);
        // Extended CONNECT without :path ⇒ malformed (RFC 8441 §4: :scheme and :path MUST be present).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "CONNECT"), new(":protocol", "websocket"), new(":scheme", "https"), new(":authority", "h")]), Is.Not.Null);
        // Well-formed Extended CONNECT.
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "CONNECT"), new(":protocol", "websocket"), new(":scheme", "https"), new(":authority", "h"), new(":path", "/chat")]), Is.Null);
        // Classic CONNECT stays valid (only :authority).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "CONNECT"), new(":authority", "h:443")]), Is.Null);
    }

    // ---- Helpers --------------------------------------------------------------------------

    /// <summary>
    /// Client + server with a WebSocket echo connectHandler (RFC 8441 §5 handshake: acceptance only
    /// for :protocol "websocket", permessage-deflate accepted when offered), handshake pumped.
    /// </summary>
    private static (Http3ClientConnection, Http3ServerConnection, ServerCertificate) WebSocketServerPair(out Func<bool> serverLoopEnded)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        bool loopEnded = false;

        Http3ConnectResult ConnectHandler(Http3Request request)
        {
            if (request.Protocol != "websocket")
                return new Http3ConnectResult { Status = 501 }; // RFC 9220 §3 SHOULD

            var responseHeaders = new List<HeaderField>();
            bool deflate = WebSocketDeflate.ShouldAccept(
                request.AdditionalHeaders.FirstOrDefault(h => h.Name == "sec-websocket-extensions").Value,
                out string? extensionsResponse);
            if (deflate)
                responseHeaders.Add(new HeaderField("sec-websocket-extensions", extensionsResponse!));

            return new Http3ConnectResult
            {
                Status = 200,
                Headers = responseHeaders,
                OnTunnel = tunnel =>
                {
                    var ws = new WebSocketConnection(tunnel, WebSocketRole.Server, deflate);
                    _ = EchoLoop(ws, tunnel, () => loopEnded = true);
                },
            };
        }

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] },
                                               connectHandler: ConnectHandler);
        var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();
        for (int round = 0; round < 5; round++)
            Pump(client, server); // let the SETTINGS (ENABLE_CONNECT_PROTOCOL) arrive
        Assert.That(client.ServerEnablesConnectProtocol, Is.True);

        serverLoopEnded = () => loopEnded;
        return (client, server, cert);
    }

    private static async Task EchoLoop(WebSocketConnection ws, Http3Tunnel tunnel, Action onEnded)
    {
        while (await ws.ReceiveAsync(None) is { } message)
        {
            if (message.Opcode == WebSocketOpcode.Text)
                await ws.SendTextAsync(Encoding.UTF8.GetString(message.Payload), None);
            else
                await ws.SendBinaryAsync(message.Payload, None);
        }
        tunnel.Complete(); // orderly tunnel end (FIN, RFC 9220 §3)
        onEnded();
    }

    /// <summary>
    /// Opens a WebSocket via Extended CONNECT (RFC 8441 §5: sec-websocket-version 13,
    /// optional permessage-deflate offer) and pumps until the 2xx acceptance.
    /// </summary>
    private static (Http3Tunnel, IReadOnlyList<HeaderField>) OpenWebSocket(Http3ClientConnection client, Http3ServerConnection server, bool offerDeflate)
    {
        var requestHeaders = new List<HeaderField> { new("sec-websocket-version", "13") };
        if (offerDeflate)
            requestHeaders.Add(new HeaderField("sec-websocket-extensions", WebSocketDeflate.Offer));

        ulong streamId = client.SendExtendedConnect("localhost", "/chat", "websocket", requestHeaders);
        int status = 0;
        IReadOnlyList<HeaderField> headers = [];
        Http3Tunnel? tunnel = null;
        for (int round = 0; round < 20 && tunnel is null; round++)
        {
            Pump(client, server);
            client.TryGetConnectResponse(streamId, out status, out headers, out tunnel);
        }
        Assert.That(status, Is.EqualTo(200));
        Assert.That(tunnel, Is.Not.Null);
        return (tunnel!, headers);
    }

    /// <summary>
    /// Starts a ReceiveAsync and pumps until it completes (single-threaded event loop:
    /// the task continuations run inline in the pump).
    /// </summary>
    private static WebSocketMessage? AwaitMessage(WebSocketConnection ws, Http3ClientConnection client, Http3ServerConnection server)
    {
        Task<WebSocketMessage?> task = ws.ReceiveAsync(None);
        for (int round = 0; round < 100 && !task.IsCompleted; round++)
            Pump(client, server);
        Assert.That(task.IsCompleted, Is.True, "The WebSocket message must arrive within the pump rounds.");
        return task.Result;
    }

    private static byte[] EncodeConnect(string protocol)
        => QpackEncoder.Encode(
        [
            new HeaderField(":method", "CONNECT"),
            new HeaderField(":scheme", "https"),
            new HeaderField(":authority", "localhost"),
            new HeaderField(":path", "/chat"),
            new HeaderField(":protocol", protocol),
        ]);

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump(QuicClientConnection client, Http3ServerConnection server)
    {
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
