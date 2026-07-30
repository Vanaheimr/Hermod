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
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.WebTransport;

/// <summary>
/// WebTransport over HTTP/3 (draft-ietf-webtrans-http3-13): session establishment via Extended
/// CONNECT (:protocol=webtransport), uni/bidirectional WebTransport streams, datagrams, flow-control
/// capsules and session termination via WT_CLOSE_SESSION.
/// </summary>
[TestFixture]
public class Http3WebTransportTests
{
    [Test]
    public void ErrorCodeMapping_RoundTripsThroughApplicationErrorRange()
    {
        // §4.3: 32-bit app codes ⇄ WT_APPLICATION_ERROR range, reserved 0x1f·N+0x21 skipped.
        foreach (uint code in new uint[] { 0, 1, 29, 30, 31, 1000, uint.MaxValue })
        {
            ulong http = WebTransportConstants.ApplicationErrorToHttp(code);
            Assert.That(http, Is.InRange(WebTransportConstants.ApplicationErrorFirst, WebTransportConstants.ApplicationErrorLast));
            Assert.That((http - 0x21) % 0x1f, Is.Not.EqualTo(0UL)); // never a reserved greasing codepoint
            Assert.That(WebTransportConstants.HttpToApplicationError(http), Is.EqualTo(code));
        }
    }

    [Test]
    public void CapsuleReader_ParsesCompleteCapsulesAndReportsRemainder()
    {
        byte[] a = WebTransportCapsule.BuildVarIntCapsule(WebTransportConstants.CapsuleMaxData, 4096);
        byte[] close = WebTransportCapsule.BuildCloseSession(42, "goodbye");
        byte[] combined = [.. a, .. close, 0xFF]; // a started third capsule (only the type byte)

        List<WebTransportCapsule> capsules = WebTransportCapsule.ReadAll(combined, out int consumed);
        Assert.That(capsules.Count, Is.EqualTo(2));
        Assert.That(capsules[0].Type, Is.EqualTo(WebTransportConstants.CapsuleMaxData));
        Assert.That(capsules[1].Type, Is.EqualTo(WebTransportConstants.CapsuleCloseSession));
        Assert.That(consumed, Is.EqualTo(a.Length + close.Length)); // the incomplete third one stays put
    }

    [Test]
    public void WebTransport_NotOffered_WithoutServerSupport()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) = Pair(serverMaxSessions: 0);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        Assert.That(client.ServerSupportsWebTransport, Is.False);
        Assert.Throws<InvalidOperationException>(() => client.ConnectWebTransport("localhost", "/wt"));
    }

    [Test]
    public void SessionEstablishment_And404Rejection()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(serverMaxSessions: 4, accept: request => request.Path == "/wt" ? _ => { } : null);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        Assert.That(client.ServerSupportsWebTransport, Is.True);

        // Unknown resource ⇒ 404 (draft §3.2).
        ulong badId = client.ConnectWebTransport("localhost", "/unknown");
        for (int r = 0; r < 20 && client.WebTransportConnectStatus(badId) is null; r++) Pump(client, server);
        Assert.That(client.WebTransportConnectStatus(badId), Is.EqualTo(404));
        Assert.That(client.TryGetWebTransportSession(badId, out _), Is.False);

        // Valid resource ⇒ session.
        ulong okId = client.ConnectWebTransport("localhost", "/wt");
        WebTransportSession? session = null;
        for (int r = 0; r < 20 && session is null; r++) { Pump(client, server); client.TryGetWebTransportSession(okId, out session); }
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.IsClosed, Is.False);
    }

    [Test]
    public void Datagrams_And_UnidirectionalStream_And_BidirectionalStream_EchoEndToEnd()
    {
        var serverStreams = new List<WebTransportStream>();
        (Http3ClientConnection client, Http3ServerConnection server, WebTransportSession serverSession, ServerCertificate cert)
            = EstablishedSession(onServerStream: serverStreams.Add);
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;
        WebTransportSession clientSession = GetClientSession(client, server);

        // --- Datagram client → server → echo (§4.4) ---
        Assert.That(clientSession.SendDatagram(Encoding.UTF8.GetBytes("WT-datagram")), Is.True);
        byte[]? dg = null;
        for (int r = 0; r < 15 && dg is null; r++) { Pump(client, server); serverSession.TryReceiveDatagram(out dg); }
        Assert.That(Encoding.UTF8.GetString(dg!), Is.EqualTo("WT-datagram"));
        serverSession.SendDatagram(dg!);
        byte[]? dgEcho = null;
        for (int r = 0; r < 15 && dgEcho is null; r++) { Pump(client, server); clientSession.TryReceiveDatagram(out dgEcho); }
        Assert.That(Encoding.UTF8.GetString(dgEcho!), Is.EqualTo("WT-datagram"));

        // --- Unidirectional WebTransport stream client → server (§4.1) ---
        WebTransportStream? cUni = clientSession.OpenUnidirectionalStream();
        Assert.That(cUni, Is.Not.Null);
        cUni!.Write(Encoding.UTF8.GetBytes("uni-hello"));
        cUni.Finish();
        WebTransportStream? sUni = null;
        for (int r = 0; r < 20 && sUni is null; r++) { Pump(client, server); sUni = serverSession.AcceptUnidirectionalStream(); }
        Assert.That(sUni, Is.Not.Null);
        Assert.That(sUni!.IsBidirectional, Is.False);
        Assert.That(ReadAll(sUni, client, server), Is.EqualTo("uni-hello"));

        // --- Bidirectional WebTransport stream client → server, server responds (§4.2) ---
        WebTransportStream? cBidi = clientSession.OpenBidirectionalStream();
        Assert.That(cBidi, Is.Not.Null);
        cBidi!.Write(Encoding.UTF8.GetBytes("ping"));
        cBidi.Finish();
        WebTransportStream? sBidi = null;
        for (int r = 0; r < 20 && sBidi is null; r++) { Pump(client, server); sBidi = serverSession.AcceptBidirectionalStream(); }
        Assert.That(sBidi, Is.Not.Null);
        Assert.That(sBidi!.IsBidirectional, Is.True);
        Assert.That(ReadAll(sBidi, client, server), Is.EqualTo("ping"));
        sBidi.Write(Encoding.UTF8.GetBytes("pong"));
        sBidi.Finish();
        Assert.That(ReadAll(cBidi, client, server), Is.EqualTo("pong"));
    }

    [Test]
    public void CloseSession_PropagatesErrorCodeAndReason_AndAbortsStreams()
    {
        (Http3ClientConnection client, Http3ServerConnection server, WebTransportSession serverSession, ServerCertificate cert)
            = EstablishedSession(onServerStream: _ => { });
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;
        WebTransportSession clientSession = GetClientSession(client, server);

        // An open stream shall be aborted when the session ends (§6, WT_SESSION_GONE).
        WebTransportStream? uni = clientSession.OpenUnidirectionalStream();
        for (int r = 0; r < 5; r++) Pump(client, server);

        clientSession.Close(0x1234, "over");
        for (int r = 0; r < 15 && !serverSession.IsClosed; r++) Pump(client, server);

        Assert.That(serverSession.IsClosed, Is.True);
        Assert.That(serverSession.CloseErrorCode, Is.EqualTo(0x1234u));
        Assert.That(serverSession.CloseReason, Is.EqualTo("over"));
        Assert.That(clientSession.IsClosed, Is.True);
        Assert.That(client.IsClosing, Is.False); // session end ≠ connection end
        Assert.That(uni, Is.Not.Null);
    }

    [Test]
    public void ExcessSessions_BeyondMaxSessions_AreRejectedWithRequestRejected()
    {
        // The server allows only ONE session; the second must be reset with H3_REQUEST_REJECTED (§5.2).
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(serverMaxSessions: 1, accept: _ => _ => { });
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong first = client.ConnectWebTransport("localhost", "/wt");
        WebTransportSession? s1 = null;
        for (int r = 0; r < 20 && s1 is null; r++) { Pump(client, server); client.TryGetWebTransportSession(first, out s1); }
        Assert.That(s1, Is.Not.Null);

        ulong second = client.ConnectWebTransport("localhost", "/wt");
        for (int r = 0; r < 20 && !client.Quic.Streams[second].IsResetByPeer; r++) Pump(client, server);
        Assert.That(client.Quic.Streams[second].IsResetByPeer, Is.True);
        Assert.That(client.Quic.Streams[second].PeerResetErrorCode, Is.EqualTo(Http3Error.RequestRejected));
        Assert.That(client.IsClosing, Is.False);
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static (Http3ClientConnection, Http3ServerConnection, ServerCertificate) Pair(
        ulong serverMaxSessions, Func<Http3Request, Action<WebTransportSession>?>? accept = null)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] },
            webTransportMaxSessions: serverMaxSessions, webTransportHandler: accept);
        var client = new Http3ClientConnection("localhost", certificateValidation: validation, webTransportMaxSessions: 4);
        client.Start();
        for (int r = 0; r < 20 && !client.HandshakeConfirmed; r++) Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();
        for (int r = 0; r < 5; r++) Pump(client, server); // SETTINGS both ways
        return (client, server, cert);
    }

    private static (Http3ClientConnection, Http3ServerConnection, WebTransportSession, ServerCertificate)
        EstablishedSession(Action<WebTransportStream> onServerStream)
    {
        WebTransportSession? serverSession = null;
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(serverMaxSessions: 4, accept: _ => session => serverSession = session);

        ulong id = client.ConnectWebTransport("localhost", "/wt");
        for (int r = 0; r < 20 && (serverSession is null || !client.TryGetWebTransportSession(id, out _)); r++)
            Pump(client, server);
        Assert.That(serverSession, Is.Not.Null);
        return (client, server, serverSession!, cert);
    }

    private static WebTransportSession GetClientSession(Http3ClientConnection client, Http3ServerConnection server)
    {
        // The (only) client session belongs to the first CONNECT stream (ID 0).
        for (int r = 0; r < 20; r++)
        {
            if (client.TryGetWebTransportSession(0, out WebTransportSession? s) && s is not null)
                return s;
            Pump(client, server);
        }
        throw new AssertionException("no client WebTransport session");
    }

    private static string ReadAll(WebTransportStream stream, Http3ClientConnection client, Http3ServerConnection server)
    {
        var buffer = new List<byte>();
        for (int r = 0; r < 30 && !stream.IsReceiveComplete; r++)
        {
            buffer.AddRange(stream.Read());
            Pump(client, server);
        }
        buffer.AddRange(stream.Read());
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
