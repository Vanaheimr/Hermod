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
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.WebTransport;

/// <summary>
/// WebTransport lets go of what is over (draft-ietf-webtrans-http3-13): a session's streams once
/// they are done, a session once it has ended, and streams that arrive for a session that has. Both
/// connections used to keep every session and every stream of one for as long as they lived.
/// </summary>
[TestFixture]
public class WebTransportReleaseTests
{

    #region (helpers)

    public enum Closer { Client, Server }

    private sealed class Pair : IDisposable
    {

        public required ServerCertificate      Certificate    { get; init; }
        public required Http3ClientConnection  Client         { get; init; }
        public required Http3ServerConnection  Server         { get; init; }
        public          WebTransportSession?   ServerSession  { get; set; }

        public void Pump(int rounds = 1)
        {
            for (int round = 0; round < rounds; round++)
            {
                Client.CheckTimeouts();
                foreach (byte[] datagram in Client.GetDatagramsToSend())
                    Server.ProcessDatagram(datagram);
                foreach (byte[] datagram in Server.GetDatagramsToSend())
                    Client.ProcessDatagram(datagram);
            }
        }

        /// <summary>
        /// Opens a session and pumps until both ends have it.
        /// </summary>
        public (ulong SessionId, WebTransportSession Client, WebTransportSession Server) Open()
        {
            ServerSession = null;
            ulong id = Client.ConnectWebTransport("localhost", "/wt");
            WebTransportSession? session = null;
            for (int round = 0; round < 20 && (session is null || ServerSession is null); round++)
            {
                Pump();
                Client.TryGetWebTransportSession(id, out session);
            }
            Assert.That(session, Is.Not.Null, "the client's session");
            Assert.That(ServerSession, Is.Not.Null, "the server's session");
            return (id, session!, ServerSession!);
        }

        public void Dispose()
        {
            Client.Dispose();
            Server.Dispose();
            Certificate.Dispose();
        }

    }

    private static Pair OpenPair(ulong serverMaxSessions = 4)
    {

        var cert = ServerCertificate.CreateSelfSigned("localhost");
        Pair? pair = null;

        var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] },
                                               webTransportMaxSessions: serverMaxSessions,
                                               webTransportHandler: _ => session => pair!.ServerSession = session);

        var client = new Http3ClientConnection("localhost",
                                               certificateValidation: new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] },
                                               webTransportMaxSessions: 4);

        pair = new Pair { Certificate = cert, Client = client, Server = server };

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            pair.Pump();
        client.InitializeHttp3();
        pair.Pump(5);
        Assert.That(client.ServerSupportsWebTransport, Is.True, "the server's SETTINGS");
        return pair;

    }

    private static string ReadAll(Pair pair, WebTransportStream stream)
    {
        var received = new List<byte>();
        for (int round = 0; round < 30 && !stream.IsReceiveComplete; round++)
        {
            received.AddRange(stream.Read());
            pair.Pump();
        }
        received.AddRange(stream.Read());
        Assert.That(stream.IsReceiveComplete, Is.True, "the stream's FIN");
        return Encoding.UTF8.GetString([.. received]);
    }

    /// <summary>
    /// A uni stream the client opens by hand, with the WebTransport header for
    /// <paramref name="sessionId"/> (draft §4.1) — as a peer would whose session has ended.
    /// </summary>
    private static QuicStream RawUniStream(Pair pair, ulong sessionId)
    {
        QuicStream stream = pair.Client.Quic.OpenUnidirectionalStream();
        var writer = new BufferWriter(16);
        try
        {
            writer.WriteVarInt(WebTransportConstants.UniStreamType);
            writer.WriteVarInt(sessionId);
            stream.Write(writer.WrittenSpan);
        }
        finally { writer.Dispose(); }
        stream.Write("late"u8);
        return stream;
    }

    #endregion


    [Test]
    public void FinishedStreams_AreLetGo_WhileTheSessionGoesOn()
    {

        using Pair pair = OpenPair();
        (_, WebTransportSession client, WebTransportSession server) = pair.Open();
        int clientStreamsBefore = pair.Client.Quic.Streams.Count;
        int serverStreamsBefore = pair.Server.Quic.Streams.Count;

        for (int i = 0; i < 20; i++)
        {
            // The client sends one way, and asks and is answered.
            WebTransportStream uni = client.OpenUnidirectionalStream()!;
            uni.Write(Encoding.UTF8.GetBytes($"uni {i}"));
            uni.Finish();
            WebTransportStream? accepted = null;
            for (int round = 0; round < 10 && accepted is null; round++) { pair.Pump(); accepted = server.AcceptUnidirectionalStream(); }
            Assert.That(ReadAll(pair, accepted!), Is.EqualTo($"uni {i}"));

            WebTransportStream ask = client.OpenBidirectionalStream()!;
            ask.Write(Encoding.UTF8.GetBytes($"ping {i}"));
            ask.Finish();
            WebTransportStream? asked = null;
            for (int round = 0; round < 10 && asked is null; round++) { pair.Pump(); asked = server.AcceptBidirectionalStream(); }
            Assert.That(ReadAll(pair, asked!), Is.EqualTo($"ping {i}"));
            asked!.Write(Encoding.UTF8.GetBytes($"pong {i}"));
            asked.Finish();
            Assert.That(ReadAll(pair, ask), Is.EqualTo($"pong {i}"));

            // And the server asks the client.
            WebTransportStream question = server.OpenBidirectionalStream()!;
            question.Write(Encoding.UTF8.GetBytes($"who {i}"));
            question.Finish();
            WebTransportStream? heard = null;
            for (int round = 0; round < 10 && heard is null; round++) { pair.Pump(); heard = client.AcceptBidirectionalStream(); }
            Assert.That(ReadAll(pair, heard!), Is.EqualTo($"who {i}"));
            heard!.Finish();
            ReadAll(pair, question);
        }
        pair.Pump(5);

        Assert.That(client.IsClosed, Is.False);
        Assert.That(server.IsClosed, Is.False);
        Assert.That(pair.Client.Quic.Streams.Count, Is.EqualTo(clientStreamsBefore), "the client's QUIC streams");
        Assert.That(pair.Server.Quic.Streams.Count, Is.EqualTo(serverStreamsBefore), "the server's QUIC streams");
        Assert.That(pair.Client.RememberedWebTransportStreamsForTest, Is.Zero, "streams the client remembers to skip");
        Assert.That(pair.Server.RememberedWebTransportStreamsForTest, Is.Zero, "streams the server remembers to skip");

    }

    [Test]
    public void AnEndedSession_IsLetGo([Values] Closer closer)
    {

        using Pair pair = OpenPair();
        (ulong id, WebTransportSession client, WebTransportSession server) = pair.Open();
        WebTransportStream open = client.OpenBidirectionalStream()!; // aborted with the session (§6)
        pair.Pump(3);

        if (closer == Closer.Client)
            client.Close(7, "done");
        else
            server.Close(7, "done");
        pair.Pump(5);

        Assert.That(client.IsClosed && server.IsClosed, Is.True);
        Assert.That(pair.Server.TrackedRequestsForTest, Is.Zero, "the server's request states");
        Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.True, "the server's CONNECT stream");
        Assert.That(pair.Server.Quic.IsStreamClosed(open.StreamId), Is.True, "the server's end of the session's stream");

        pair.Client.ForgetRequest(id);
        pair.Pump(3);
        Assert.That(pair.Client.TrackedRequestsForTest, Is.Zero, "the client's request states");
        Assert.That(pair.Client.Quic.IsStreamClosed(id), Is.True, "the client's CONNECT stream");
        Assert.That(pair.Client.Quic.IsStreamClosed(open.StreamId), Is.True, "the client's end of the session's stream");

        Assert.That(pair.Server.WebTransportSessionsForTest, Is.Zero, "the server's sessions");
        Assert.That(pair.Client.WebTransportSessionsForTest, Is.Zero, "the client's sessions");

    }

    [Test]
    public void ASessionsConnectStream_IsNoLongerRead_OnceTheSessionIsOver([Values] Closer closer)
    {

        // The side that closes lets go of the session once its FIN is out. When the other side has
        // not closed its own by then — here because the FIN was lost — it stops reading
        // (WT_SESSION_GONE, draft §6): the other side's reset then ends the stream as its FIN would.
        using Pair pair = OpenPair();
        (ulong id, WebTransportSession client, WebTransportSession server) = pair.Open();
        QuicStream other = closer == Closer.Server ? pair.Client.Quic.Streams[id] : pair.Server.Quic.Streams[id];

        if (closer == Closer.Server)
        {
            server.Close();
            _ = pair.Server.GetDatagramsToSend(); // the FIN goes out, and is lost
            pair.Server.CheckTimeouts();
            Assert.That(pair.Server.TrackedRequestsForTest, Is.Zero, "the server's request states");
        }
        else
        {
            client.Close();
            pair.Client.ForgetRequest(id);
            _ = pair.Client.GetDatagramsToSend();
            pair.Client.CheckTimeouts();
            Assert.That(pair.Client.TrackedRequestsForTest, Is.Zero, "the client's request states");
        }
        pair.Pump(3);

        Assert.That(other.Send.IsReset, Is.True, "the STOP_SENDING's reset");
        Assert.That(other.Send.ResetErrorCode, Is.EqualTo(WebTransportConstants.SessionGone));

    }

    [Test]
    public void AResetConnectStream_EndsTheSession([Values] Closer closer)
    {

        using Pair pair = OpenPair();
        (ulong id, WebTransportSession client, WebTransportSession server) = pair.Open();

        if (closer == Closer.Client)
            pair.Client.Quic.Streams[id].Reset(Http3Error.RequestCancelled);
        else
            pair.Server.Quic.Streams[id].Reset(Http3Error.RequestCancelled);
        pair.Pump(5);

        Assert.That(closer == Closer.Client ? server.IsClosed : client.IsClosed, Is.True,
                    "the session at the other end");
        Assert.That(pair.Client.IsClosing || pair.Server.IsClosing, Is.False);

    }

    [Test]
    public void SessionsOneAfterAnother_AreNotLimitedLikeSessionsAtOnce()
    {

        // WT_MAX_SESSIONS limits sessions at once (draft §5.2), not in a connection's life.
        using Pair pair = OpenPair(serverMaxSessions: 1);
        for (int i = 0; i < 3; i++)
        {
            (ulong id, WebTransportSession client, _) = pair.Open();
            client.Close();
            pair.Pump(5);
            pair.Client.ForgetRequest(id);
        }

        (_, WebTransportSession open, _) = pair.Open();
        ulong second = pair.Client.ConnectWebTransport("localhost", "/wt");
        pair.Pump(5);
        Assert.That(open.IsClosed, Is.False);
        Assert.That(pair.Client.Quic.Streams[second].PeerResetErrorCode, Is.EqualTo(Http3Error.RequestRejected),
                    "a second session at once");

    }

    [Test]
    public void WhatArrivesForAnEndedSession_IsTurnedAway([Values] bool connectStreamGone)
    {

        using Pair pair = OpenPair();
        (ulong id, WebTransportSession client, WebTransportSession server) = pair.Open();

        client.Close();
        if (connectStreamGone)
        {
            pair.Pump(5);
            Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.True, "the server's CONNECT stream");
        }
        else
        {
            // The server learns of the end, and its CONNECT stream is still there.
            foreach (byte[] datagram in pair.Client.GetDatagramsToSend())
                pair.Server.ProcessDatagram(datagram);
            Assert.That(server.IsClosed, Is.True);
            Assert.That(pair.Server.Quic.IsStreamClosed(id), Is.False, "the server's CONNECT stream");
        }

        QuicStream late = RawUniStream(pair, id);
        pair.Client.TrySendHttpDatagram(id, "late"u8.ToArray()); // the session's quarter stream ID (§4.4)
        pair.Pump(3);

        Assert.That(late.Send.IsReset, Is.True, "the server's STOP_SENDING");
        Assert.That(late.Send.ResetErrorCode, Is.EqualTo(WebTransportConstants.SessionGone));
        Assert.That(server.AcceptUnidirectionalStream(), Is.Null);
        Assert.That(server.TryReceiveDatagram(out _), Is.False, "a datagram for the session");
        Assert.That(pair.Client.IsClosing, Is.False);

    }

    [Test]
    public void AStreamBeyondTheBuffer_IsTurnedAway_WithoutEndingTheConnection()
    {

        // Streams for a session still to come are buffered, up to 16 (draft §4.5). The one beyond
        // is turned away — with a STOP_SENDING alone: a reset of a stream the client only sends on
        // is the client's STREAM_STATE_ERROR, which used to end the connection.
        using Pair pair = OpenPair();
        const ulong NotYetOpened = 4_000;

        var streams = new List<QuicStream>();
        for (int i = 0; i < 17; i++)
            streams.Add(RawUniStream(pair, NotYetOpened));
        pair.Pump(3);

        Assert.That(pair.Client.IsClosing, Is.False);
        Assert.That(streams.Count(s => s.Send.IsReset), Is.EqualTo(1));
        Assert.That(streams.Single(s => s.Send.IsReset).Send.ResetErrorCode,
                    Is.EqualTo(WebTransportConstants.BufferedStreamRejected));

    }

    [Test]
    public void BufferedStreams_ForAStreamThatEndedWithoutASession_AreTurnedAway()
    {

        // A stream that names an ordinary request as its session waits for a session that will not
        // come. Once the request is over and its stream gone, it is turned away; it used to wait for
        // ever, in one of the 16 places of the buffer.
        using Pair pair = OpenPair();
        ulong request = pair.Client.SendRequest(Http3Request.Get("localhost", "/"));
        QuicStream waiting = RawUniStream(pair, request);
        for (int round = 0; round < 10 && !pair.Client.TryGetResponse(request, out _); round++)
            pair.Pump();
        pair.Client.ForgetRequest(request);
        pair.Pump(5);

        Assert.That(pair.Server.Quic.IsStreamClosed(request), Is.True, "the request's stream");
        Assert.That(waiting.Send.IsReset, Is.True, "the server's STOP_SENDING");
        Assert.That(waiting.Send.ResetErrorCode, Is.EqualTo(WebTransportConstants.SessionGone));

    }

}
