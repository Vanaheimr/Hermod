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
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP3;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Security;

/// <summary>
/// Hardening of the server facade: a bounded number of connections, a demux that does not get more
/// expensive with every client, and a limit on buffered request bodies.
/// </summary>
[TestFixture]
public class Http3ServerHardeningTests
{
    [Test]
    public async Task ConnectionLimit_RefusesFurtherConnections_AndKeepsServingTheExistingOnes()
    {
        // Without a cap every Initial packet creates a connection — including a certificate
        // signature. An attacker gets unbounded memory and CPU out of that.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(0,
            () => new Http3ServerConnection(cert, _ => new Http3Response { Status = 200, Body = "ok"u8.ToArray() }),
            maxConnections: 2);
        server.Start();

        await using var first = new Http3Client("localhost", server.Port, validation);
        await first.ConnectAsync(TimeSpan.FromSeconds(10));
        await using var second = new Http3Client("localhost", server.Port, validation);
        await second.ConnectAsync(TimeSpan.FromSeconds(10));
        Assert.That(server.ConnectionCount, Is.EqualTo(2));

        // The third one must not get through — dropped silently, so it runs into its timeout.
        await using var third = new Http3Client("localhost", server.Port, validation);
        Assert.ThrowsAsync<TimeoutException>(async () => await third.ConnectAsync(TimeSpan.FromSeconds(2)));

        Assert.That(server.ConnectionCount, Is.EqualTo(2), "No connection may be created beyond the limit.");
        Assert.That(server.ConnectionsRefused, Is.GreaterThan(0), "And the rejection is counted.");

        // Decisive: the existing connections keep working.
        Http3Response response = await first.GetAsync("/still-alive").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task ManyConnections_AreDemultiplexedWithoutAScanPerDatagram()
    {
        // The index replaces the linear scan over all connections. This test would also pass with
        // the scan — it guards the FUNCTION (correct routing with many clients), while the index is
        // what keeps the per-packet cost independent of the client count.
        const int clients = 12;
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        await using var server = new Http3Server(cert,
            request => new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes(request.Path) }, port: 0);
        server.Start();

        var connections = new List<Http3Client>();
        try
        {
            for (int i = 0; i < clients; i++)
            {
                var client = new Http3Client("localhost", server.Port, validation);
                await client.ConnectAsync(TimeSpan.FromSeconds(10));
                connections.Add(client);
            }
            Assert.That(server.ConnectionCount, Is.EqualTo(clients));

            // Every client must get exactly ITS answer — proof the routing stays correct.
            Http3Response[] responses = await Task.WhenAll(
                connections.Select((c, i) => c.GetAsync($"/client-{i}").WaitAsync(TimeSpan.FromSeconds(15))));
            for (int i = 0; i < clients; i++)
                Assert.That(responses[i].BodyText, Is.EqualTo($"/client-{i}"),
                            "A datagram was routed to the wrong connection.");
        }
        finally
        {
            foreach (Http3Client client in connections)
                await client.DisposeAsync();
        }
    }

    [Test]
    public void BufferedBodyAboveTheLimit_Yields413()
    {
        // A buffered body grows in memory ⇒ refuse it early instead of collecting it to the end.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert,
            request => new Http3Response { Status = 200, Body = request.Body },
            maxRequestBodySize: 32 * 1024);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();

        ulong big = client.SendRequest(Http3Request.Post("localhost", "/upload",
            RandomNumberGenerator.GetBytes(200_000), "application/octet-stream"));
        Http3Response? response = Run(client, server, big);

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(413), "Oversized body ⇒ 413 Content Too Large.");

        // And the connection stays usable for a request within the limit.
        byte[] small = RandomNumberGenerator.GetBytes(1000);
        ulong ok = client.SendRequest(Http3Request.Post("localhost", "/upload", small, "application/octet-stream"));
        Assert.That(Run(client, server, ok)!.Body, Is.EqualTo(small));
    }

    [Test]
    public void StreamingHandlers_AreExemptFromTheBufferedLimit()
    {
        // A streaming handler decides for itself how much it consumes — the buffered limit must not
        // cut off exactly the case that was built for large uploads.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var server = new Http3ServerConnection(cert,
            async (_, body, token) =>
            {
                long total = 0;
                byte[] buffer = new byte[8192];
                while (await body.ReadAsync(buffer, token).ConfigureAwait(false) is > 0 and var read)
                    total += read;
                return new Http3Response { Status = 200, Body = Encoding.UTF8.GetBytes(total.ToString()) };
            },
            maxRequestBodySize: 32 * 1024);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        client.InitializeHttp3();

        ulong stream = client.SendRequest(Http3Request.Post("localhost", "/upload",
            RandomNumberGenerator.GetBytes(200_000), "application/octet-stream"));
        Http3Response? response = Run(client, server, stream);

        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.BodyText, Is.EqualTo("200000"), "The streaming handler sees the whole body.");
    }

    private static void Pump(Http3ClientConnection client, Http3ServerConnection server)
    {
        client.CheckTimeouts();
        server.CheckTimeouts();
        foreach (byte[] dg in client.GetDatagramsToSend()) server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend()) client.ProcessDatagram(dg);
    }

    private static Http3Response? Run(Http3ClientConnection client, Http3ServerConnection server, ulong stream)
    {
        Http3Response? response = null;
        for (int round = 0; round < 4000 && response is null; round++)
        {
            Pump(client, server);
            client.TryGetResponse(stream, out response);
        }
        return response;
    }
}
