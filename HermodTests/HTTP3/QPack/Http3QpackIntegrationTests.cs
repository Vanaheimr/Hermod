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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.QPack;

/// <summary>
/// End to end: the dynamic QPACK table (RFC 9204) over the complete HTTP/3 stack. Client and
/// server each announce a capacity, exchange encoder-stream instructions and reference
/// dynamic entries — run against each other in-process.
/// </summary>
[TestFixture]
public class Http3QpackIntegrationTests
{
    [Test]
    public void DynamicTable_RequestAndResponse_OverFullHttp3Stack()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // "x-served-by" is not a static-table name ⇒ forces a dynamic insert in the response.
        Http3Response Handler(Http3Request request) => new()
        {
            Status = 200,
            Headers = [new HeaderField("content-type", "text/plain"), new HeaderField("x-served-by", "http3-from-scratch")],
            Body = Encoding.UTF8.GetBytes($"dynamic ok {request.Path}"),
        };

        using var server = new Http3ServerConnection(cert, Handler, qpackMaxTableCapacity: 4096);
        using var client = new Http3ClientConnection("localhost", certificateValidation: validation, qpackMaxTableCapacity: 4096);
        client.Start();

        void Pump()
        {
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
        }

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump();
        Assert.That(client.HandshakeConfirmed, Is.True);

        client.InitializeHttp3();
        // Let SETTINGS and stream-type prefixes flow both ways (the client learns the server capacity).
        for (int round = 0; round < 6; round++)
            Pump();

        ulong requestStream = client.SendRequest(Http3Request.Get("localhost", "/hello"));
        Http3Response? response = null;
        for (int round = 0; round < 40 && response is null; round++)
        {
            Pump();
            client.TryGetResponse(requestStream, out response);
        }

        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo(200));
        Assert.That(response.GetHeader("content-type"), Is.EqualTo("text/plain"));
        Assert.That(response.GetHeader("x-served-by"), Is.EqualTo("http3-from-scratch"));
        Assert.That(response.BodyText, Does.Contain("dynamic ok /hello"));

        // The dynamic table was actually used: the client inserted request headers,
        // the server replayed the same instructions via the encoder stream.
        Assert.That(client.QpackEncoderInsertCount > 0, Is.True, "The client must use the dynamic table.");
        Assert.That(server.QpackDecoderInsertCount, Is.EqualTo(client.QpackEncoderInsertCount));

        // Likewise for the response headers (server encoder ↔ client decoder).
        Assert.That(server.QpackEncoderInsertCount > 0, Is.True, "The server must use the dynamic table for the response.");
        Assert.That(client.QpackDecoderInsertCount, Is.EqualTo(server.QpackEncoderInsertCount));

        // Shuttle a bit more so the client's section acknowledgment reaches the server.
        for (int round = 0; round < 4; round++)
            Pump();
        Assert.That(server.QpackEncoderKnownReceivedCount > 0, Is.True, "The server must have received the client's section acknowledgment (RFC 9204 §4.4.1).");
    }
}
