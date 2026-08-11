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

using System.Threading;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// End to end: our HTTP/3 client talks in-process to our HTTP/3 server (both from scratch).
/// The datagrams are exchanged directly between the two (no real network). Validates the
/// complete server path: QUIC server handshake, HTTP/3 server, QPACK-encoded response.
/// </summary>
[TestFixture]
public class Http3ClientServerTests
{
    [Test]
    public void Client_Gets_ResponseFromOwnServer()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        Http3Response Handler(Http3Request request) => new()
        {
            Status = 200,
            Headers = [new HeaderField("content-type", "text/plain")],
            Body = System.Text.Encoding.UTF8.GetBytes($"Hello from scratch! You requested {request.Path}."),
        };

        // The client trusts the self-signed test certificate as a custom trust root and validates it for real.
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, Handler);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        ulong requestStream = 0;
        bool requestSent = false;
        Http3Response? response = null;

        // Shuttle datagrams directly between client and server.
        for (int round = 0; round < 20 && response is null; round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);

            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);

            if (client.HandshakeConfirmed && !requestSent)
            {
                client.InitializeHttp3();
                requestStream = client.SendRequest(Http3Request.Get("localhost", "/hello"));
                requestSent = true;
            }

            if (requestSent)
                client.TryGetResponse(requestStream, out response);
        }

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.GetHeader("content-type"), Is.EqualTo("text/plain"));
        Assert.That(response.BodyText, Does.Contain("/hello"));
        Assert.That(response.BodyText, Does.Contain("from scratch"));
    }

    [Test]
    public void LargeResponse_TransfersFully_ThroughPacedCongestionControlledSendPath()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        // ~150 KB deterministic body — big enough to drive the cwnd-/pacing-limited send path
        // across many packets (slow start, pacing budget, MTU packetization).
        byte[] body = new byte[150_000];
        for (int i = 0; i < body.Length; i++)
            body[i] = (byte)(i * 31 + 7);

        Http3Response Handler(Http3Request request) => new()
        {
            Status = 200,
            Headers = [new HeaderField("content-type", "application/octet-stream")],
            Body = body,
        };

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        // Pumped on a fake clock advanced 1 ms per round: the pacer refills its budget from ELAPSED
        // TIME (RFC 9002 §7.7), and with the real clock this fixed round budget races the machine —
        // on a JIT-warm process 2000 rounds burn down in ~3 ms, less real time than the pacer needs
        // to recover its post-handshake deficit at the handshake's compute-time-derived sRTT (~40 ms).
        var clock = new FakeTimeProvider();
        using var server = new Http3ServerConnection(cert, Handler, timeProvider: clock);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, timeProvider: clock);
        client.Start();

        ulong requestStream = 0;
        bool requestSent = false;
        Http3Response? response = null;
        int maxServerDatagram = 0;

        for (int round = 0; round < 2000 && response is null; round++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);

            foreach (byte[] dg in server.GetDatagramsToSend())
            {
                maxServerDatagram = Math.Max(maxServerDatagram, dg.Length);
                client.ProcessDatagram(dg);
            }

            if (client.HandshakeConfirmed && !requestSent)
            {
                client.InitializeHttp3();
                requestStream = client.SendRequest(Http3Request.Get("localhost", "/big"));
                requestSent = true;
            }

            if (requestSent)
                client.TryGetResponse(requestStream, out response);
        }

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.Body.Length, Is.EqualTo(body.Length));
        Assert.That(body.AsSpan().SequenceEqual(response.Body), Is.True, "The received body must match byte for byte.");

        // The MTU-limited emitter must not produce oversized datagrams.
        // Upper bound is the DPLPMTUD search ceiling, not the 1200-byte floor: once discovery has
        // proven a larger path (RFC 9000 §14.3) the send path legitimately uses it, and a PMTU
        // probe is larger still by definition (§14.2). Nothing may exceed the ceiling, though.
        Assert.That(maxServerDatagram <= PathMtuDiscovery.DefaultSearchCeiling, Is.True,
                    $"Server datagram too large: {maxServerDatagram} bytes.");
    }

    [Test]
    public void Post_WithRequestBody_ServerReceivesBodyAndContentLength()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        byte[] requestBody = System.Text.Encoding.UTF8.GetBytes("Hello server, here comes a POST body!");
        Http3Request? seenRequest = null;

        // Echo handler: mirrors the request body back (proves reception AND the response path).
        Http3Response Handler(Http3Request request)
        {
            seenRequest = request;
            return new Http3Response
            {
                Status = 200,
                Headers = [new HeaderField("content-type", "application/octet-stream")],
                Body = request.Body,
            };
        }

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, Handler);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        ulong requestStream = 0;
        bool requestSent = false;
        Http3Response? response = null;

        for (int round = 0; round < 30 && response is null; round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);

            if (client.HandshakeConfirmed && !requestSent)
            {
                client.InitializeHttp3();
                requestStream = client.SendRequest(Http3Request.Post("localhost", "/echo", requestBody, "text/plain"));
                requestSent = true;
            }

            if (requestSent)
                client.TryGetResponse(requestStream, out response);
        }

        // The server saw the method, the headers and the complete body (RFC 9114 §4.1).
        Assert.That(seenRequest, Is.Not.Null);
        Assert.That(seenRequest!.Method, Is.EqualTo(HTTPMethod.POST));
        Assert.That(seenRequest.Path,    Is.EqualTo("/echo"));
        Assert.That(seenRequest.AdditionalHeaders.FirstOrDefault(h => h.Name == "content-type").Value, Is.EqualTo("text/plain"));
        Assert.That(seenRequest.AdditionalHeaders.FirstOrDefault(h => h.Name == "content-length").Value, Is.EqualTo(requestBody.Length.ToString()));
        Assert.That(requestBody.AsSpan().SequenceEqual(seenRequest.Body), Is.True, "The request body must arrive byte for byte.");

        // And the echo came back completely.
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(requestBody.AsSpan().SequenceEqual(response.Body), Is.True, "The echo must match byte for byte.");
    }

    [Test]
    public void LargeRequestBody_UploadsFully_ThroughClientSendPath()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");

        // ~120 KB upload — drives the CLIENT send path (cwnd, pacing, MTU packetization,
        // flow control) across many packets for the first time; so far only downloads tested the reverse direction.
        byte[] requestBody = new byte[120_000];
        for (int i = 0; i < requestBody.Length; i++)
            requestBody[i] = (byte)(i * 17 + 3);

        int seenBodyLength = -1;
        Http3Response Handler(Http3Request request)
        {
            seenBodyLength = request.Body.Length;
            // Respond with a checksum instead of an echo (keeps the response small and proves byte accuracy).
            byte[] hash = System.Security.Cryptography.SHA256.HashData(request.Body);
            return new Http3Response { Status = 200, Body = hash };
        }

        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, Handler);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        ulong requestStream = 0;
        bool requestSent = false;
        Http3Response? response = null;
        int maxClientDatagram = 0;

        for (int round = 0; round < 2000 && response is null; round++)
        {
            client.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend())
            {
                maxClientDatagram = Math.Max(maxClientDatagram, dg.Length);
                server.ProcessDatagram(dg);
            }
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);

            if (client.HandshakeConfirmed && !requestSent)
            {
                client.InitializeHttp3();
                requestStream = client.SendRequest(Http3Request.Post("localhost", "/upload", requestBody));
                requestSent = true;
            }

            if (requestSent)
                client.TryGetResponse(requestStream, out response);
        }

        Assert.That(seenBodyLength, Is.EqualTo(requestBody.Length));
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(System.Security.Cryptography.SHA256.HashData(requestBody).AsSpan().SequenceEqual(response.Body), Is.True, "The SHA-256 checksum of the upload must match — the body arrived byte for byte.");

        // The client emitter too stays MTU-limited.
        // Upper bound is the DPLPMTUD search ceiling, not the 1200-byte floor: once discovery has
        // proven a larger path (RFC 9000 §14.3) the send path legitimately uses it, and a PMTU
        // probe is larger still by definition (§14.2). Nothing may exceed the ceiling, though.
        Assert.That(maxClientDatagram <= PathMtuDiscovery.DefaultSearchCeiling, Is.True,
                    $"Client datagram too large: {maxClientDatagram} bytes.");
    }

    [Test]
    public void IdleTimeout_SilentlyClosesConnection_AfterInactivity()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        Http3Response Handler(Http3Request request) => new() { Status = 200, Body = [] };

        // The server announces a short idle timeout (300 ms), but on the real clock that is NOT the
        // instant the connection dies: RFC 9000 §10.1 raises the effective bound to at least 3·PTO,
        // and on an in-process pair the RTT estimate measures the handshake's own signature and
        // verification work rather than any network delay. Measured here, 25–90 ms of "RTT" put
        // 3·PTO anywhere between roughly 250 ms and 750 ms — the upper end being a cold process.
        // Hence a deadline instead of a fixed sleep; the exact timing is pinned down deterministically
        // by TimeProviderTests.IdleTimeout_Expires_WhenOnlyTheFakeClockAdvances.
        var serverParams = new TransportParameters { MaxIdleTimeoutMs = 300 };
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, Handler, serverParams);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must come about.");
        Assert.That(server.IsIdleTimedOut, Is.False);

        // Without further packet exchange the connection falls silent and must die on its own.
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (!server.IsIdleTimedOut && DateTimeOffset.UtcNow < deadline)
        {
            Thread.Sleep(50);
            server.CheckTimeouts();
        }

        Assert.That(server.IsIdleTimedOut, Is.True, "The server must close the connection after the idle timeout.");
        Assert.That(server.GetDatagramsToSend(), Is.Empty); // closed silently ⇒ no more datagrams
    }
}
