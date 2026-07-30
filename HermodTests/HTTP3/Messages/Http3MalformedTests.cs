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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Messages;

/// <summary>
/// Malformed detection (RFC 9114 §4.1.2, §4.2, §4.3): the server answers malformed requests with
/// 400 (MAY) and aborts reading with the stream error H3_MESSAGE_ERROR, WITHOUT invoking the
/// handler; the client MUST NOT accept malformed responses. The connection lives on in each case.
/// </summary>
[TestFixture]
public class Http3MalformedTests
{
    // ---- Validator units (edge cases) ------------------------------------------------------

    [Test]
    public void Validator_CatchesPseudoHeaderViolations()
    {
        // Uppercase letter in a field name (§4.2 MUST).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "h"), new(":path", "/"), new("X-Bad", "1")]), Is.Not.Null);
        // Pseudo-header after a regular field (§4.3).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new("accept", "*/*"), new(":scheme", "https"), new(":path", "/"), new(":authority", "h")]), Is.Not.Null);
        // Response pseudo-header in a request (§4.3).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":status", "200"), new(":method", "GET"), new(":scheme", "https"), new(":authority", "h"), new(":path", "/")]), Is.Not.Null);
        // userinfo in :authority (§4.3.1).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "user@host"), new(":path", "/")]), Is.Not.Null);
        // :authority and Host contradict each other (§4.3.1).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "a"), new(":path", "/"), new("host", "b")]), Is.Not.Null);
        // CR/LF in a field value (request-smuggling protection).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "h"), new(":path", "/"), new("x", "a\r\nb")]), Is.Not.Null);
        // Well-formed (with Host instead of :authority).
        Assert.That(Http3MessageValidator.ValidateRequestHeaders(
            [new(":method", "GET"), new(":scheme", "https"), new(":path", "/"), new("host", "h")]), Is.Null);

        // Responses: :status missing / duplicated / non-numeric (§4.3.2).
        Assert.That(Http3MessageValidator.ValidateResponseHeaders([new("content-type", "text/plain")], out _), Is.Not.Null);
        Assert.That(Http3MessageValidator.ValidateResponseHeaders([new(":status", "200"), new(":status", "204")], out _), Is.Not.Null);
        Assert.That(Http3MessageValidator.ValidateResponseHeaders([new(":status", "abc")], out _), Is.Not.Null);
        Assert.That(Http3MessageValidator.ValidateResponseHeaders([new(":status", "204")], out int ok), Is.Null);
        Assert.That(ok, Is.EqualTo(204));

        // Contradictory content-length values (§4.1.2).
        Assert.That(Http3MessageValidator.ValidateContentLength(
            [new("content-length", "5"), new("content-length", "6")], 5, contentNeverPresent: false), Is.Not.Null);
    }

    [Test]
    public void Client_RefusesToSend_MalformedOwnRequest()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = [] });
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();

        // Uppercase header ⇒ MUST NOT generate (§4.2) ⇒ refused locally.
        Assert.Throws<ArgumentException>(() => client.SendRequest(
            Http3Request.Get("localhost", "/") with { AdditionalHeaders = [new HeaderField("X-Bad", "1")] }));
        // Connection-specific field ⇒ likewise.
        Assert.Throws<ArgumentException>(() => client.SendRequest(
            Http3Request.Get("localhost", "/") with { AdditionalHeaders = [new HeaderField("connection", "keep-alive")] }));
        // Pseudo-header in a trailer ⇒ likewise (§4.3).
        Assert.Throws<ArgumentException>(() => client.SendRequest(
            Http3Request.Get("localhost", "/") with { Trailers = [new HeaderField(":method", "GET")] }));
    }

    // ---- Malformed requests ⇒ server: 400 + H3_MESSAGE_ERROR, no handler ------------------

    [Test]
    public void UppercaseFieldName_IsRejectedWith400()
        => AssertRejected400(stream => WriteHeaders(stream,
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/"), new("X-Bad", "1")]));

    [Test]
    public void PseudoHeaderAfterRegularField_IsRejectedWith400()
        => AssertRejected400(stream => WriteHeaders(stream,
            [new(":method", "GET"), new("accept", "*/*"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/")]));

    [Test]
    public void MissingPathPseudoHeader_IsRejectedWith400()
        => AssertRejected400(stream => WriteHeaders(stream,
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "localhost")]));

    [Test]
    public void ConnectionSpecificField_IsRejectedWith400()
        => AssertRejected400(stream => WriteHeaders(stream,
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/"), new("connection", "keep-alive")]));

    [Test]
    public void TeOtherThanTrailers_IsRejectedWith400()
        => AssertRejected400(stream => WriteHeaders(stream,
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/"), new("te", "gzip")]));

    [Test]
    public void ContentLengthMismatch_IsRejectedWith400()
        => AssertRejected400(stream =>
        {
            WriteHeaders(stream,
                [new(":method", "POST"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/"), new("content-length", "10")]);
            stream.Write(Http3Frames.Build(Http3FrameType.Data, [1, 2, 3])); // 3 ≠ 10 (§4.1.2)
        });

    [Test]
    public void PseudoHeaderInTrailers_IsRejectedWith400()
        => AssertRejected400(stream =>
        {
            WriteHeaders(stream,
                [new(":method", "POST"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/")]);
            stream.Write(Http3Frames.Build(Http3FrameType.Data, [1]));
            WriteHeaders(stream, [new(":status", "200")]); // pseudo-header in a trailer (§4.3)
        });

    [Test]
    public void TeTrailers_IsAccepted_RequestSucceeds()
    {
        (int status, ulong? stopCode, int handled) = RunRawRequest(stream => WriteHeaders(stream,
            [new(":method", "GET"), new(":scheme", "https"), new(":authority", "localhost"), new(":path", "/"), new("te", "trailers")]));
        Assert.That(status, Is.EqualTo(200));
        Assert.That(handled, Is.EqualTo(1));
        Assert.That(stopCode, Is.Null);
    }

    [Test]
    public void Connect_IsWellFormed_ButAnsweredWith501()
    {
        // §4.4: a valid CONNECT (only :method + :authority) — this server does not support it.
        (int status, ulong? _, int handled) = RunRawRequest(stream => WriteHeaders(stream,
            [new(":method", "CONNECT"), new(":authority", "localhost:443")]));
        Assert.That(status, Is.EqualTo(501));
        Assert.That(handled, Is.EqualTo(0));
    }

    // ---- Malformed responses ⇒ client: discard (MUST NOT accept), connection lives --------

    [Test]
    public void ResponseWithoutStatus_IsDiscardedAsMalformed()
        => AssertResponseMalformed(section: [new("content-type", "text/plain")]);

    [Test]
    public void ResponseWithUppercaseFieldName_IsDiscardedAsMalformed()
        => AssertResponseMalformed(section: [new(":status", "200"), new("X-Bad", "1")]);

    [Test]
    public void ResponseContentLengthMismatch_IsDiscardedAsMalformed()
    {
        (Http3ClientConnection client, QuicServerConnection server, ulong streamId, ServerCertificate cert) = StartRawServerExchange();
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = server.Streams[streamId];
        stream.Write(Http3Frames.Build(Http3FrameType.Headers,
            QpackEncoder.Encode([new(":status", "200"), new("content-length", "10")])));
        stream.Write(Http3Frames.Build(Http3FrameType.Data, [1, 2, 3])); // 3 ≠ 10
        stream.Finish();

        for (int round = 0; round < 10 && !client.IsResponseMalformed(streamId); round++)
            Pump2(client, server);
        Assert.That(client.IsResponseMalformed(streamId), Is.True);
        Assert.That(client.TryGetResponse(streamId, out _), Is.False);
        Assert.That(client.IsClosing, Is.False);
    }

    [Test]
    public void NoContent204_WithContentLength_IsAccepted()
    {
        // §4.1.2: responses defined as bodyless (204) may carry a content-length,
        // even though no content follows in DATA frames.
        (Http3ClientConnection client, QuicServerConnection server, ulong streamId, ServerCertificate cert) = StartRawServerExchange();
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = server.Streams[streamId];
        stream.Write(Http3Frames.Build(Http3FrameType.Headers,
            QpackEncoder.Encode([new(":status", "204"), new("content-length", "5")])));
        stream.Finish();

        Http3Response? response = null;
        for (int round = 0; round < 10 && response is null; round++)
        {
            Pump2(client, server);
            client.TryGetResponse(streamId, out response);
        }
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(204));
        Assert.That(response.Body, Is.Empty);
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static void WriteHeaders(QuicStream stream, HeaderField[] fields)
        => stream.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeSectionRaw(fields)));

    /// <summary>
    /// Encodes a field section exclusively as "Literal Field Line with Literal Name" and leaves
    /// the names UNCHANGED — unlike <see cref="QpackEncoder"/>, which lowercases names by
    /// convention. Only this way can uppercase violations (§4.2) be reproduced on the wire.
    /// </summary>
    private static byte[] EncodeSectionRaw(HeaderField[] fields)
    {
        var writer = new org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers.BufferWriter(128);
        try
        {
            writer.WriteByte(0x00); // field section prefix: Required Insert Count = 0, Base = 0
            writer.WriteByte(0x00);
            foreach (HeaderField field in fields)
            {
                QpackPrimitives.EncodeString(ref writer, field.Name, 3, 0b0010_0000);
                QpackPrimitives.EncodeString(ref writer, field.Value, 7, 0x00);
            }
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    /// <summary>
    /// A raw client sends an (evil) request; expected: 400, no handler invocation, the server's
    /// read abort with H3_MESSAGE_ERROR (§4.1.2), the connection stays open.
    /// </summary>
    private static void AssertRejected400(Action<QuicStream> writeRequest)
    {
        (int status, ulong? stopCode, int handled) = RunRawRequest(writeRequest);
        Assert.That(status, Is.EqualTo(400));
        Assert.That(handled, Is.EqualTo(0));
        Assert.That(stopCode, Is.EqualTo(Http3Error.MessageError));
    }

    /// <summary>
    /// Brings up the handshake + control stream, sends the request via <paramref name="writeRequest"/>
    /// (FIN is added) and returns (status of the response, STOP_SENDING code, handler invocations).
    /// </summary>
    private static (int Status, ulong? StopCode, int Handled) RunRawRequest(Action<QuicStream> writeRequest)
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        int handled = 0;
        using var server = new Http3ServerConnection(cert, _ => { handled++; return new Http3Response { Status = 200, Body = [] }; });
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        QuicStream control = client.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));

        QuicStream request = client.OpenBidirectionalStream();
        writeRequest(request);
        request.Finish();

        var received = new List<byte>();
        for (int round = 0; round < 15; round++)
        {
            Pump(client, server);
            received.AddRange(request.Read());
        }
        Assert.That(server.IsClosing, Is.False, "Malformed is a stream error, not a connection error.");

        Assert.That(Http3Frames.TryReadAll(received.ToArray(), out List<Http3Frame> frames, out _), Is.True);
        Http3Frame headersFrame = frames.First(f => f.Type == Http3FrameType.Headers);
        Assert.That(QpackDecoder.Decode(headersFrame.Payload.Span, out List<HeaderField> headers), Is.EqualTo(QpackResult.Ok));
        int status = int.Parse(headers.First(h => h.Name == ":status").Value);
        return (status, request.PeerStopSendingErrorCode, handled);
    }

    /// <summary>
    /// A raw server sends the given response section (+ FIN); expected: the client discards the
    /// response as malformed, the connection stays open.
    /// </summary>
    private static void AssertResponseMalformed(HeaderField[] section)
    {
        (Http3ClientConnection client, QuicServerConnection server, ulong streamId, ServerCertificate cert) = StartRawServerExchange();
        using ServerCertificate certGuard = cert;
        using Http3ClientConnection c = client;
        using QuicServerConnection s = server;

        QuicStream stream = server.Streams[streamId];
        stream.Write(Http3Frames.Build(Http3FrameType.Headers, EncodeSectionRaw(section)));
        stream.Finish();

        for (int round = 0; round < 10 && !client.IsResponseMalformed(streamId); round++)
            Pump2(client, server);
        Assert.That(client.IsResponseMalformed(streamId), Is.True, "The malformed response must be discarded.");
        Assert.That(client.TryGetResponse(streamId, out _), Is.False);
        Assert.That(client.IsClosing, Is.False);
    }

    private static (Http3ClientConnection, QuicServerConnection, ulong, ServerCertificate) StartRawServerExchange()
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump2(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        QuicStream control = server.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));

        ulong streamId = client.SendRequest(Http3Request.Get("localhost", "/"));
        for (int round = 0; round < 5; round++)
            Pump2(client, server);
        return (client, server, streamId, cert);
    }

    private static void Pump(QuicClientConnection client, Http3ServerConnection server)
    {
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }

    private static void Pump2(Http3ClientConnection client, QuicServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
