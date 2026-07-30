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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Api;

/// <summary>
/// The Task-based async API (<see cref="Http3Client"/>/<see cref="Http3Server"/>): real UDP
/// sockets over loopback, background pump, awaitable requests — the deterministic core underneath
/// remains unchanged.
/// </summary>
[TestFixture]
public class Http3AsyncApiTests
{
    [Test]
    public async Task AsyncHandler_SlowRequest_DoesNotBlockTheServerLoop()
    {
        // Over REAL sockets: while one request is parked in the handler, another connection must
        // still be served. With the synchronous handler this would deadlock the whole loop.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var gate = new TaskCompletionSource();

        await using var server = new Http3Server(cert, async (request, token) =>
        {
            if (request.Path == "/slow")
                await gate.Task.WaitAsync(token).ConfigureAwait(false);
            return new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes($"ok {request.Path}") };
        }, port: 0);
        server.Start();

        await using var slowClient = new Http3Client("localhost", server.Port, validation);
        await slowClient.ConnectAsync(TimeSpan.FromSeconds(10));
        Task<Http3Response> slow = slowClient.GetAsync("/slow");

        await using var fastClient = new Http3Client("localhost", server.Port, validation);
        await fastClient.ConnectAsync(TimeSpan.FromSeconds(10));
        Http3Response fast = await fastClient.GetAsync("/fast").WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(fast.BodyText, Is.EqualTo("ok /fast"), "The second connection must be served meanwhile.");
        Assert.That(slow.IsCompleted, Is.False, "The slow request is still parked.");

        gate.SetResult();
        Http3Response slowResponse = await slow.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(slowResponse.BodyText, Is.EqualTo("ok /slow"));
    }

    [Test]
    public async Task LargeTransfer_OverRealSockets_CompletesInReasonableTime()
    {
        // Regression guard for the facade I/O loops. Both pump loops used to process ONE datagram
        // per await cycle (plus an abandoned Task.Delay timer each pass), which capped throughput
        // at roughly 150 KB/s — a 3 MB transfer did not finish within 60 s at all. Draining the
        // socket per pass fixed that; the bound below is generous (measured: well under a second)
        // but still catches a return to per-datagram async round trips.
        const int size = 3_000_000;
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        byte[] payload = new byte[size];
        new Random(7).NextBytes(payload);

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = request.Body.Length > 0 ? [1] : payload }, port: 0);
        server.Start();
        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Http3Response download = await client.GetAsync("/big").WaitAsync(TimeSpan.FromSeconds(30));
        long downloadMs = watch.ElapsedMilliseconds;
        watch.Restart();
        Http3Response upload = await client.PostAsync("/up", payload, "application/octet-stream")
                                           .WaitAsync(TimeSpan.FromSeconds(30));
        long uploadMs = watch.ElapsedMilliseconds;

        Assert.That(download.Body, Has.Length.EqualTo(size));
        Assert.That(upload.Status, Is.EqualTo(200));
        Assert.That(downloadMs, Is.LessThan(15_000), $"3 MB download took {downloadMs} ms.");
        Assert.That(uploadMs, Is.LessThan(15_000), $"3 MB upload took {uploadMs} ms.");
        TestContext.Out.WriteLine($"3 MB down {downloadMs} ms / up {uploadMs} ms");
    }

    [Test]
    public async Task StreamingBody_OverRealLoopbackUdp_ArrivesByteExact()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        byte[] payload = new byte[200_000];
        new Random(42).NextBytes(payload);

        await using var server = new Http3Server(cert,
            (_, _) => Task.FromResult(new Http3Response
            {
                Status = 200,
                BodyStream = new MemoryStream(payload, writable: false),
            }), port: 0);
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Http3Response response = await client.GetAsync("/stream").WaitAsync(TimeSpan.FromSeconds(60));
        watch.Stop();

        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Body, Is.EqualTo(payload), "streamed over real UDP, byte-exact.");
        TestContext.Out.WriteLine($"200 KB streamed over real UDP in {watch.ElapsedMilliseconds} ms");
    }

    [Test]
    public async Task GetAsync_OverRealLoopbackUdp_Returns200()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes($"hello {request.Path}") },
            port: 0);
        server.Start();
        Assert.That(server.Port, Is.GreaterThan(0));

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        Http3Response response = await client.GetAsync("/async");
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("hello /async"));
        Assert.That(server.ConnectionCount, Is.EqualTo(1));

        await client.CloseAsync();
    }

    [Test]
    public async Task ParallelRequests_OverOneConnection_AllSucceed()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes(request.Path) },
            port: 0);
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        // Several requests at once — the pump serializes the core, the tasks run in parallel.
        Http3Response[] responses = await Task.WhenAll(
            client.GetAsync("/one"), client.GetAsync("/two"), client.GetAsync("/three"));

        Assert.That(responses.Select(r => r.Status), Is.All.EqualTo(200));
        Assert.That(responses.Select(r => r.BodyText), Is.EquivalentTo(new[] { "/one", "/two", "/three" }));
    }

    [Test]
    public async Task PostAsync_EchoesBody()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = request.Body },
            port: 0);
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        byte[] body = Encoding.UTF8.GetBytes("async-POST-body");
        Http3Response response = await client.PostAsync("/echo", body, "text/plain");
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Body, Is.EqualTo(body));
    }

    [Test]
    public async Task ConnectAsync_AgainstDeadPort_ThrowsTimeout()
    {
        // No server: the handshake can never be confirmed ⇒ TimeoutException instead of a hang.
        await using var client = new Http3Client("localhost", 1, CertificateValidationOptions.Insecure);
        Assert.ThrowsAsync<TimeoutException>(() => client.ConnectAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Test]
    public async Task QueryAndPerformAsync_GiveSerializedAccessToTheConnection()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert, _ => new Http3Response { Status = 204 }, port: 0);
        server.Start();

        await using var client = new Http3Client("localhost", server.Port, validation);
        await client.ConnectAsync(TimeSpan.FromSeconds(10));

        Assert.That(await client.QueryAsync(c => c.HandshakeConfirmed), Is.True);
        // WaitUntilAsync polls while the pump keeps running (trivially satisfied immediately here).
        Assert.That(await client.WaitUntilAsync(c => c.HandshakeConfirmed, TimeSpan.FromSeconds(1)), Is.True);
    }
}
