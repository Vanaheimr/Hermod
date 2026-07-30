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
/// Trailer sections and interim responses (1xx) — RFC 9114 §4.1: a message consists of a header
/// section, optional content (DATA) and an optional trailer section; a final response may be
/// preceded by interim responses (1xx, e.g. 103 Early Hints), which carry neither content nor
/// trailers.
/// </summary>
[TestFixture]
public class Http3TrailerTests
{
    [Test]
    public void ResponseTrailers_EndToEnd_ArriveSeparatedFromHeaders()
    {
        Http3Response? response = RoundTrip(
            request => new Http3Response
            {
                Status = 200,
                Headers = [new HeaderField("content-type", "text/plain")],
                Body = System.Text.Encoding.UTF8.GetBytes("body"),
                Trailers = [new HeaderField("checksum", "abc123"), new HeaderField("server-timing", "app;dur=7")],
            },
            Http3Request.Get("localhost", "/"));

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("body"));
        Assert.That(response.Trailers.Count, Is.EqualTo(2));
        Assert.That(response.Trailers.First(t => t.Name == "checksum").Value, Is.EqualTo("abc123"));
        // Trailers are NOT part of the header section.
        Assert.That(response.GetHeader("checksum"), Is.Null);
    }

    [Test]
    public void ResponseTrailers_WithoutContent_AlsoWork()
    {
        Http3Response? response = RoundTrip(
            request => new Http3Response
            {
                Status = 204,
                Trailers = [new HeaderField("checksum", "empty")],
            },
            Http3Request.Get("localhost", "/"));

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(204));
        Assert.That(response.Body, Is.Empty);
        Assert.That(response.Trailers.Single().Value, Is.EqualTo("empty"));
    }

    [Test]
    public void RequestTrailers_EndToEnd_ServerSeesThem()
    {
        Http3Request? seen = null;
        Http3Response? response = RoundTrip(
            request => { seen = request; return new Http3Response { Status = 200, Body = [] }; },
            Http3Request.Post("localhost", "/upload", System.Text.Encoding.UTF8.GetBytes("data"), "text/plain") with
            {
                Trailers = [new HeaderField("upload-checksum", "xyz789")],
            });

        Assert.That(response, Is.Not.Null);
        Assert.That(seen, Is.Not.Null);
        Assert.That(System.Text.Encoding.UTF8.GetString(seen!.Body), Is.EqualTo("data"));
        Assert.That(seen.Trailers.Single(t => t.Name == "upload-checksum").Value, Is.EqualTo("xyz789"));
        // Trailers do NOT end up in the regular headers.
        Assert.That(seen.AdditionalHeaders, Has.None.Matches<HeaderField>(h => h.Name == "upload-checksum"));
    }

    [Test]
    public void InterimResponses_103EarlyHints_PrecedeFinalResponse()
    {
        Http3Response? response = RoundTrip(
            request => new Http3Response
            {
                Status = 200,
                Headers = [new HeaderField("content-type", "text/html")],
                Body = System.Text.Encoding.UTF8.GetBytes("<html/>"),
                InterimResponses =
                [
                    new Http3InterimResponse(103, [new HeaderField("link", "</style.css>; rel=preload; as=style")]),
                    new Http3InterimResponse(103, [new HeaderField("link", "</app.js>; rel=preload; as=script")]),
                ],
            },
            Http3Request.Get("localhost", "/"));

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("<html/>"));
        Assert.That(response.InterimResponses.Count, Is.EqualTo(2));
        Assert.That(response.InterimResponses, Has.All.Property("Status").EqualTo(103));
        Assert.That(response.InterimResponses[0].Headers.Single(h => h.Name == "link").Value, Does.Contain("style.css"));
        // The 1xx headers are NOT part of the final header section (§4.1).
        Assert.That(response.GetHeader("link"), Is.Null);
    }

    [Test]
    public void DataAfterInterimResponse_IsMalformed_StreamErrorMessageError()
    {
        // Interim responses carry NO content (§4.1) — an "evil" raw server sends 103 + DATA
        // anyway. The client MUST reject the response as malformed (§4.1.2): STREAM error
        // H3_MESSAGE_ERROR, the connection stays alive.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();

        QuicStream control = server.OpenUnidirectionalStream();
        control.Write([(byte)Http3StreamType.Control]);
        control.Write(Http3Frames.Build(Http3FrameType.Settings, []));

        ulong streamId = client.SendRequest(Http3Request.Get("localhost", "/"));
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        QuicStream serverStream = server.Streams[streamId];
        serverStream.Write(Http3Frames.Build(Http3FrameType.Headers,
            QpackEncoder.Encode([new HeaderField(":status", "103")])));
        serverStream.Write(Http3Frames.Build(Http3FrameType.Data, [1, 2, 3]));
        for (int round = 0; round < 10 && !client.IsResponseMalformed(streamId); round++)
            Pump(client, server);

        Assert.That(client.IsResponseMalformed(streamId), Is.True, "The response must be discarded as malformed.");
        Assert.That(client.TryGetResponse(streamId, out _), Is.False);
        Assert.That(client.IsClosing, Is.False, "Malformed is a STREAM error, not a connection error (§4.1.2).");
        // The stream was aborted with H3_MESSAGE_ERROR — visible at the raw server.
        for (int round = 0; round < 10 && !serverStream.IsResetByPeer; round++)
            Pump(client, server);
        Assert.That(serverStream.IsResetByPeer, Is.True);
        Assert.That(serverStream.PeerResetErrorCode, Is.EqualTo(Http3Error.MessageError));
    }

    // ---- Helpers --------------------------------------------------------------------------

    /// <summary>
    /// Full in-process round trip: our own client ↔ our own server, one request, one response.
    /// </summary>
    private static Http3Response? RoundTrip(Func<Http3Request, Http3Response> handler, Http3Request request)
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, handler);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        ulong requestStream = 0;
        bool requestSent = false;
        Http3Response? response = null;

        for (int round = 0; round < 40 && response is null; round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);

            if (client.HandshakeConfirmed && !requestSent)
            {
                client.InitializeHttp3();
                requestStream = client.SendRequest(request);
                requestSent = true;
            }
            if (requestSent)
                client.TryGetResponse(requestStream, out response);
        }
        return response;
    }

    private static void Pump(Http3ClientConnection client, QuicServerConnection server)
    {
        client.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
