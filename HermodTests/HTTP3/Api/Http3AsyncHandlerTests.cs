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
using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;
using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Api;

/// <summary>
/// Asynchronous request handler and streaming response bodies. The deterministic pump model stays
/// intact: the pump never awaits, it polls the handler task and pulls the next body chunk — so
/// these tests remain fully synchronous and reproducible.
/// </summary>
[TestFixture]
public class Http3AsyncHandlerTests
{
    /// <summary>
    /// Client/server pair with a completed handshake and initialised HTTP/3.
    /// </summary>
    private static (Http3ClientConnection Client, Http3ServerConnection Server, ServerCertificate Cert) Pair(
        Func<Http3Request, CancellationToken, Task<Http3Response>> handler)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var server = new Http3ServerConnection(cert, handler);
        var client = new Http3ClientConnection("localhost", certificateValidation: validation);
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
                                      ulong stream, int maxRounds = 4000)
    {
        Http3Response? response = null;
        for (int round = 0; round < maxRounds && response is null; round++)
        {
            Pump(client, server);
            client.TryGetResponse(stream, out response);
        }
        return response;
    }

    // ---- Asynchronous handler ------------------------------------------------------------------

    [Test]
    public void AsyncHandler_Response_ArrivesOnceTheTaskCompletes()
    {
        var gate = new TaskCompletionSource<Http3Response>();
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((_, _) => gate.Task);
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong stream = client.SendRequest(Http3Request.Get("localhost", "/slow"));

        // While the handler is still running there is no response — but the connection lives on.
        for (int round = 0; round < 20; round++)
            Pump(client, server);
        Assert.That(client.TryGetResponse(stream, out _), Is.False, "Nothing may be sent yet.");
        Assert.That(server.HasPendingRequests, Is.True, "The request is still in flight.");

        gate.SetResult(new Http3Response { Status = 200, Body = "done"u8.ToArray() });

        Http3Response? response = Run(client, server, stream);
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("done"));
        Assert.That(server.HasPendingRequests, Is.False, "And the request counts as finished.");
    }

    [Test]
    public void AsyncHandler_DoesNotBlockOtherRequests_OnTheSameConnection()
    {
        // The point of the whole exercise: a slow request must not hold up the others.
        var slow = new TaskCompletionSource<Http3Response>();
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((request, _) => request.Path == "/slow"
                ? slow.Task
                : Task.FromResult(new Http3Response { Status = 200, Body = "fast"u8.ToArray() }));
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong slowStream = client.SendRequest(Http3Request.Get("localhost", "/slow"));
        ulong fastStream = client.SendRequest(Http3Request.Get("localhost", "/fast"));

        Http3Response? fast = Run(client, server, fastStream);

        Assert.That(fast, Is.Not.Null, "The fast request must overtake the stalled one.");
        Assert.That(fast!.BodyText, Is.EqualTo("fast"));
        Assert.That(client.TryGetResponse(slowStream, out _), Is.False, "The slow one is still open.");

        slow.SetResult(new Http3Response { Status = 200, Body = "slow"u8.ToArray() });
        Assert.That(Run(client, server, slowStream)!.BodyText, Is.EqualTo("slow"));
    }

    [Test]
    public void FailingHandler_Becomes500_AndKeepsTheConnectionAlive()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((request, _) => request.Path == "/boom"
                ? Task.FromException<Http3Response>(new InvalidOperationException("handler blew up"))
                : Task.FromResult(new Http3Response { Status = 200 }));
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        Http3Response? boom = Run(client, server, client.SendRequest(Http3Request.Get("localhost", "/boom")));
        Assert.That(boom!.Status, Is.EqualTo(500), "A handler exception must not kill the connection.");

        Http3Response? ok = Run(client, server, client.SendRequest(Http3Request.Get("localhost", "/ok")));
        Assert.That(ok!.Status, Is.EqualTo(200), "The connection stays usable.");
    }

    [Test]
    public void SynchronousHandlerOverload_KeepsWorking()
    {
        // The simple overload must remain unchanged — the whole test suite depends on it.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert, request => new Http3Response
        {
            Status = 200,
            Body = System.Text.Encoding.UTF8.GetBytes($"sync {request.Path}"),
        });
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();

        Http3Response? response = Run(client, server, client.SendRequest(Http3Request.Get("localhost", "/x")));
        Assert.That(response!.BodyText, Is.EqualTo("sync /x"));
    }

    // ---- Streaming response body ---------------------------------------------------------------

    [Test]
    public void StreamingBody_ArrivesByteExact()
    {
        byte[] payload = RandomNumberGenerator.GetBytes(300_000);
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((_, _) => Task.FromResult(new Http3Response
            {
                Status = 200,
                Headers = [new HeaderField("content-type", "application/octet-stream")],
                BodyStream = new MemoryStream(payload, writable: false),
            }));
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        Http3Response? response = Run(client, server, client.SendRequest(Http3Request.Get("localhost", "/big")));

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.Body, Is.EqualTo(payload), "A streamed body must arrive byte-exactly.");
    }

    [Test]
    public void StreamingBody_StopsReading_WhenTheReceiverDoesNotDrain()
    {
        // The decisive property: when the peer grants no more flow-control credit, the server must
        // STOP pulling from the source instead of slurping the whole body into memory. A 4 MB body
        // against a 32 KB stream window: reading has to stall at roughly window + watermark + chunk.
        const long bodySize = 4 * 1024 * 1024;
        var source = new CountingStream(bodySize);

        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert,
            (_, _) => Task.FromResult(new Http3Response { Status = 200, BodyStream = source }));
        using var client = new Http3ClientConnection("localhost", new TransportParameters
        {
            InitialMaxDataValue = 65_536,
            InitialMaxStreamDataBidiLocalValue = 32_768,
        }, validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();
        client.SendRequest(Http3Request.Get("localhost", "/big"));

        // Deliver the client's datagrams, but throw the server's away: nothing is acknowledged,
        // no MAX_STREAM_DATA comes back ⇒ the window stays shut.
        for (int round = 0; round < 500; round++)
        {
            client.CheckTimeouts();
            server.CheckTimeouts();
            foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
            server.GetDatagramsToSend(); // discarded — the receiver never drains
        }

        Assert.That(source.BytesRead, Is.GreaterThan(0), "Something must have been produced.");
        Assert.That(source.BytesRead, Is.LessThan(512 * 1024),
                    $"{source.BytesRead} of {bodySize} bytes read although the peer grants no credit — " +
                    "the body is being buffered instead of streamed.");
    }

    [Test]
    public void StreamingBody_IsProducedIncrementally_NotAllAtOnce()
    {
        var source = new CountingStream(400_000);
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((_, _) => Task.FromResult(new Http3Response { Status = 200, BodyStream = source }));
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong stream = client.SendRequest(Http3Request.Get("localhost", "/big"));

        for (int round = 0; round < 12; round++)
            Pump(client, server);
        Assert.That(source.BytesRead, Is.GreaterThan(0), "Something must have started.");
        Assert.That(source.BytesRead, Is.LessThan(400_000), "But not everything at once.");

        Http3Response? response = Run(client, server, stream);
        Assert.That(response!.Body, Has.Length.EqualTo(400_000), "Complete in the end though.");
    }

    [Test]
    public void StreamingBody_SendsTrailersAndIsDisposed()
    {
        var source = new CountingStream(50_000);
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((_, _) => Task.FromResult(new Http3Response
            {
                Status = 200,
                BodyStream = source,
                Trailers = [new HeaderField("checksum", "abc123")],
            }));
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        Http3Response? response = Run(client, server, client.SendRequest(Http3Request.Get("localhost", "/big")));

        Assert.That(response!.Body, Has.Length.EqualTo(50_000));
        Assert.That(response.Trailers, Has.Some.Matches<HeaderField>(f => f.Name == "checksum" && f.Value == "abc123"),
                    "The trailer section must follow the streamed body.");
        Assert.That(source.Disposed, Is.True, "The server disposes the body source.");
    }

    [Test]
    public void EmptyStreamingBody_ProducesAValidResponse()
    {
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((_, _) => Task.FromResult(new Http3Response
            {
                Status = 204,
                BodyStream = new MemoryStream([], writable: false),
            }));
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        Http3Response? response = Run(client, server, client.SendRequest(Http3Request.Get("localhost", "/empty")));

        Assert.That(response!.Status, Is.EqualTo(204));
        Assert.That(response.Body, Is.Empty);
    }

    [Test]
    public void CancelledRequest_CancelsTheHandlerToken()
    {
        // RFC 9114 §4.1.1: when the client aborts, the handler should learn about it so it can stop
        // work that has become pointless.
        var observed = new TaskCompletionSource<bool>();
        var gate = new TaskCompletionSource<Http3Response>();
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair((_, token) =>
            {
                token.Register(() => observed.TrySetResult(true));
                return gate.Task;
            });
        using ServerCertificate c0 = cert;
        using Http3ClientConnection c = client;
        using Http3ServerConnection s = server;

        ulong stream = client.SendRequest(Http3Request.Get("localhost", "/slow"));
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        client.CancelRequest(stream);
        for (int round = 0; round < 20; round++)
            Pump(client, server);

        Assert.That(observed.Task.IsCompleted, Is.True, "The handler's token must be cancelled.");
        gate.TrySetResult(new Http3Response { Status = 200 });
    }

    /// <summary>
    /// A body source that reports how much has been read — the yardstick for backpressure.
    /// </summary>
    private sealed class CountingStream(long length) : Stream
    {
        public long BytesRead { get; private set; }
        public bool Disposed { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = (int)Math.Min(count, length - BytesRead);
            if (n <= 0)
                return 0;
            Array.Fill(buffer, (byte)0xAB, offset, n);
            BytesRead += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int n = (int)Math.Min(buffer.Length, length - BytesRead);
            if (n <= 0)
                return ValueTask.FromResult(0);
            buffer.Span[..n].Fill(0xAB);
            BytesRead += n;
            return ValueTask.FromResult(n);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
