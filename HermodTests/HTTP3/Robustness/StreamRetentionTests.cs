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

using System.Runtime;
using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Robustness;

/// <summary>
/// A long-lived connection must not keep what its finished responses needed.
/// </summary>
[TestFixture]
public class StreamRetentionTests
{
    [Test]
    public void ALongConnection_DoesNotKeepEveryFinishedResponse()
    {
        // Forty 300 KB responses on ONE connection. A stream's send buffer grew to 512 KB for such a
        // response and kept it, and the connection keeps its streams, so every response stayed in
        // memory for as long as the connection lived: HTTP3ConformanceTests' demo host grew by
        // ~80 MB per hundred of them. Only the server is kept alive here, as it would be serving a
        // long-lived connection; the client is disposed, so what remains is the server's.
        const int responses = 40;
        long before = Retained();
        Http3ServerConnection server = ServeOnOneConnection(responses);
        long grown = Retained() - before;
        GC.KeepAlive(server);

        Assert.That(grown, Is.LessThan(responses * 32 * 1024),
                    $"{grown / 1024} KiB still held after {responses} finished responses: {grown / responses / 1024} KiB each");
    }

    [Test]
    public void ALongClientConnection_DoesNotKeepEveryResponseItReceived()
    {
        // The receive side of the test above, with only the client kept alive. Each request is
        // forgotten once its response is taken, as Http3Client does. Before, two things kept every
        // response: the QUIC client logged every 1-RTT frame it received, STREAM data included, and
        // a request could not be forgotten, so its state kept the body twice over. Together ~1.1 MiB
        // per 300 KB response.
        const int responses = 40;
        long before = Retained();
        Http3ClientConnection client = FetchOnOneConnection(responses);
        long grown = Retained() - before;
        GC.KeepAlive(client);

        Assert.That(grown, Is.LessThan(responses * 32 * 1024),
                    $"{grown / 1024} KiB still held after {responses} received responses: {grown / responses / 1024} KiB each");
    }

    [Test]
    public void ALongConnection_DoesNotKeepEveryUploadedBody()
    {
        // The server's receive side: forty 300 KB uploads on ONE connection, with only the server
        // kept alive. A request state kept the body, and the frame buffer it was parsed from, grown
        // to 512 KiB: ~815 KiB per upload.
        const int uploads = 40;
        long before = Retained();
        Http3ServerConnection server = AcceptOnOneConnection(uploads);
        long grown = Retained() - before;
        GC.KeepAlive(server);

        Assert.That(grown, Is.LessThan(uploads * 32 * 1024),
                    $"{grown / 1024} KiB still held after {uploads} finished uploads: {grown / uploads / 1024} KiB each");
    }

    // A method of its own, so no hidden temporary of the caller keeps the client alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Http3ServerConnection ServeOnOneConnection(int responses)
    {
        byte[] body = new byte[300_000];
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(_ => new Http3Response { Status = 200, Body = body });
        for (int i = 0; i < responses; i++)
            Assert.That(RunRequest(client, server, "/big").Body.Length, Is.EqualTo(body.Length));
        client.Dispose();
        cert.Dispose();
        return server;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Http3ClientConnection FetchOnOneConnection(int responses)
    {
        byte[] body = new byte[300_000];
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(_ => new Http3Response { Status = 200, Body = body });
        for (int i = 0; i < responses; i++)
        {
            ulong id = client.SendRequest(Http3Request.Get("localhost", "/big"));
            Assert.That(AwaitResponse(client, server, id).Body.Length, Is.EqualTo(body.Length));
            client.ForgetRequest(id);
        }
        Assert.That(client.TrackedRequestsForTest, Is.Zero);
        server.Dispose();
        cert.Dispose();
        return client;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Http3ServerConnection AcceptOnOneConnection(int uploads)
    {
        byte[] body = new byte[300_000];
        (Http3ClientConnection client, Http3ServerConnection server, ServerCertificate cert) =
            Pair(request => new Http3Response { Status = request.Body.Length == body.Length ? 204 : 400 });
        for (int i = 0; i < uploads; i++)
            Assert.That(RunRequest(client, server, Http3Request.Post("localhost", "/upload", body)).Status, Is.EqualTo(204));
        client.Dispose();
        cert.Dispose();
        return server;
    }

    // Live bytes after a full, compacting collection - the large-object heap included, where these
    // arrays live and which is otherwise never compacted.
    private static long Retained()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(forceFullCollection: false);
    }

    private static (Http3ClientConnection, Http3ServerConnection, ServerCertificate) Pair(Func<Http3Request, Http3Response> handler)
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

    private static Http3Response RunRequest(Http3ClientConnection client, Http3ServerConnection server, string path)
        => RunRequest(client, server, Http3Request.Get("localhost", path));

    private static Http3Response RunRequest(Http3ClientConnection client, Http3ServerConnection server, Http3Request request)
        => AwaitResponse(client, server, client.SendRequest(request));

    private static Http3Response AwaitResponse(Http3ClientConnection client, Http3ServerConnection server, ulong id)
    {
        Http3Response? response = null;
        for (int round = 0; round < 100_000 && response is null; round++)
        {
            Pump(client, server);
            client.TryGetResponse(id, out response);
        }
        Assert.That(response, Is.Not.Null, "The response did not arrive.");
        return response!;
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
