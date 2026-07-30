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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Api;

/// <summary>
/// Streaming request bodies: the handler starts right after the header section and reads the upload
/// while it is still arriving. The pump stays synchronous — it hands each DATA frame to the reader
/// and completes an outstanding read inline, the same bridge <see cref="Http3Tunnel"/> uses.
/// </summary>
[TestFixture]
public class Http3StreamingRequestTests
{
    private static (Http3ClientConnection Client, Http3ServerConnection Server, ServerCertificate Cert) Pair(
        Func<Http3Request, Http3RequestBody, CancellationToken, Task<Http3Response>> handler,
        TransportParameters? clientParams = null)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, handler);
        var client = new Http3ClientConnection("localhost", clientParams, validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);
        client.InitializeHttp3();
        return (client, server, cert);
    }

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        server.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
    }

    private static Http3Response? Run(Http3ClientConnection client, Http3ServerConnection server,
                                      ulong stream, int maxRounds = 6000)
    {
        Http3Response? response = null;
        for (int round = 0; round < maxRounds && response is null; round++)
        {
            Pump(client, server);
            client.TryGetResponse(stream, out response);
        }
        return response;
    }

    /// <summary>
    /// Reads the body to the end and returns its SHA-256 — the handler shape a real upload endpoint
    /// would have.
    /// </summary>
    private static async Task<byte[]> HashBodyAsync(Http3RequestBody body, CancellationToken token)
    {
        using var sha = SHA256.Create();
        byte[] buffer = new byte[8192];
        while (true)
        {
            int read = await body.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read <= 0)
                break;
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return sha.Hash!;
    }

    // ---- The reader on its own -----------------------------------------------------------------

    [Test]
    public void Reader_CompletesAPendingRead_WhenDataArrives()
    {
        var body = new Http3RequestBody();
        byte[] buffer = new byte[16];

        ValueTask<int> pending = body.ReadAsync(buffer);
        Assert.That(pending.IsCompleted, Is.False, "Nothing there yet ⇒ the read waits.");

        body.Deliver("hello"u8);

        Assert.That(pending.IsCompleted, Is.True, "The pump completes the read synchronously.");
        Assert.That(pending.Result, Is.EqualTo(5));
        Assert.That(buffer.AsSpan(0, 5).ToArray(), Is.EqualTo("hello"u8.ToArray()));
    }

    [Test]
    public void Reader_ReturnsZeroAtEndOfBody()
    {
        var body = new Http3RequestBody();
        body.Deliver("ab"u8);
        body.Complete();

        byte[] buffer = new byte[16];
        Assert.That(body.ReadAsync(buffer).Result, Is.EqualTo(2));
        Assert.That(body.ReadAsync(buffer).Result, Is.Zero, "End of stream.");
    }

    [Test]
    public void Reader_SurfacesAnAbort()
    {
        var body = new Http3RequestBody();
        ValueTask<int> pending = body.ReadAsync(new byte[16]);

        body.Fail(new OperationCanceledException("aborted"));

        Assert.That(pending.IsCompleted, Is.True);
        Assert.ThrowsAsync<OperationCanceledException>(async () => await pending);
    }

    [Test]
    public void Reader_ReportsSaturation_AboveTheWatermark()
    {
        var body = new Http3RequestBody();
        Assert.That(body.IsSaturated, Is.False);

        body.Deliver(new byte[Http3RequestBody.HighWatermark + 1]);

        Assert.That(body.IsSaturated, Is.True, "Above the watermark the connection stops reading.");
        Assert.That(body.ReadAsync(new byte[Http3RequestBody.HighWatermark + 1]).Result,
                    Is.EqualTo(Http3RequestBody.HighWatermark + 1));
        Assert.That(body.IsSaturated, Is.False, "After reading, data flows again.");
    }

    // ---- End to end ----------------------------------------------------------------------------

    [Test]
    public void StreamedUpload_ArrivesCompleteAndInOrder()
    {
        byte[] payload = RandomNumberGenerator.GetBytes(400_000);
        byte[] expected = SHA256.HashData(payload);

        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(async (_, body, token) => new Http3Response
            {
                Status = 200,
                Body = await HashBodyAsync(body, token).ConfigureAwait(false),
            });
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong stream = client.SendRequest(Http3Request.Post("localhost", "/upload", payload, "application/octet-stream"));
        Http3Response? response = Run(client, server, stream);

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.Body, Is.EqualTo(expected),
                    "The streamed body must arrive complete and in order (SHA-256 over 400 KB).");
    }

    [Test]
    public void Handler_StartsBeforeTheBodyIsComplete()
    {
        // The whole point: the handler runs while the upload is still arriving.
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        byte[] payload = RandomNumberGenerator.GetBytes(200_000);

        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(async (_, body, token) =>
            {
                started.TrySetResult();
                await release.Task.ConfigureAwait(false);
                byte[] hash = await HashBodyAsync(body, token).ConfigureAwait(false);
                return new Http3Response { Status = 200, Body = hash };
            });
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong stream = client.SendRequest(Http3Request.Post("localhost", "/upload", payload, "application/octet-stream"));

        // A few rounds are enough for the header section — the handler must already be running,
        // long before the 200 KB have arrived.
        for (int round = 0; round < 5; round++)
            Pump(client, server);
        Assert.That(started.Task.IsCompleted, Is.True, "The handler must start at the HEADERS.");

        release.SetResult();
        Http3Response? response = Run(client, server, stream);
        Assert.That(response!.Body, Is.EqualTo(SHA256.HashData(payload)));
    }

    [Test]
    public void SlowReader_ThrottlesThePeer_InsteadOfBuffering()
    {
        // A handler that does not read must not let the upload pile up in memory: above the
        // watermark the connection leaves the data on the QUIC stream, the window closes and the
        // client stops sending.
        var release = new TaskCompletionSource();
        var body = new TaskCompletionSource<Http3RequestBody>();
        byte[] payload = RandomNumberGenerator.GetBytes(2_000_000);

        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(async (_, requestBody, _) =>
            {
                body.TrySetResult(requestBody);
                await release.Task.ConfigureAwait(false); // read nothing for now
                return new Http3Response { Status = 200 };
            });
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        client.SendRequest(Http3Request.Post("localhost", "/upload", payload, "application/octet-stream"));
        for (int round = 0; round < 400; round++)
            Pump(client, server);

        Assert.That(body.Task.IsCompleted, Is.True);
        Assert.That(body.Task.Result.Buffered, Is.LessThan(4 * Http3RequestBody.HighWatermark),
                    $"{body.Task.Result.Buffered} bytes buffered although the handler reads nothing — " +
                    "the upload is being accumulated instead of throttled.");
        release.SetResult();
    }

    [Test]
    public void AbortedUpload_MakesTheReaderFail_InsteadOfHanging()
    {
        var body = new TaskCompletionSource<Http3RequestBody>();
        var readFailed = new TaskCompletionSource<bool>();
        byte[] payload = RandomNumberGenerator.GetBytes(300_000);

        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(async (_, requestBody, token) =>
            {
                body.TrySetResult(requestBody);
                try
                {
                    await HashBodyAsync(requestBody, token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    readFailed.TrySetResult(true);
                }
                return new Http3Response { Status = 200 };
            });
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong stream = client.SendRequest(Http3Request.Post("localhost", "/upload", payload, "application/octet-stream"));
        for (int round = 0; round < 5; round++)
            Pump(client, server);

        client.CancelRequest(stream); // RFC 9114 §4.1.1
        for (int round = 0; round < 40; round++)
            Pump(client, server);

        // The handler's catch runs on whichever thread completes the failed read — that may be a
        // pool thread, so wait for it briefly instead of checking IsCompleted straight away.
        Assert.That(readFailed.Task.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "A reader waiting on an aborted upload must see the error, not hang.");
    }

    [Test]
    public void BufferedHandlers_AreUnaffected()
    {
        // Without a streaming handler everything stays as before: the handler runs at the FIN and
        // sees the complete body.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert,
            request => new Http3Response { Status = 200, Body = request.Body });
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();

        byte[] payload = RandomNumberGenerator.GetBytes(50_000);
        ulong stream = client.SendRequest(Http3Request.Post("localhost", "/echo", payload, "application/octet-stream"));
        Http3Response? response = Run(client, server, stream);

        Assert.That(response!.Body, Is.EqualTo(payload));
    }
}
