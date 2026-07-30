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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP3.Connection;

/// <summary>
/// When the server puts its SETTINGS on the wire. The server may write 1-RTT data as soon as it has
/// the application keys — with its own Finished, a full flight before the client's Finished comes back
/// (RFC 9001 §4.1.1) — and an HTTP/3 server should use that window (RFC 9114 §6.2.1).
///
/// This is not cosmetic. Chrome decides whether a server supports WebTransport at the moment its
/// handshake completes; a server that waits for the client Finished has its SETTINGS arrive behind
/// HANDSHAKE_DONE, and the browser gives up with net::ERR_METHOD_NOT_SUPPORTED before ever sending
/// the CONNECT request.
/// </summary>
[TestFixture]
public class ServerSettingsTimingTests
{

    [Test]
    public void ServerSettings_ArriveInTheFirstServerFlight()
    {
        using var certificate = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [certificate.Certificate] };

        using var server = new Http3ServerConnection(
                               certificate,
                               _ => new Http3Response { Status = 200, Body = [] },
                               webTransportMaxSessions: 4,
                               webTransportHandler: _ => _ => { });
        using var client = new Http3ClientConnection("localhost",
                                                     certificateValidation: validation,
                                                     webTransportMaxSessions: 4);
        client.Start();

        // Flight 1: the client's Initial reaches the server. The client Finished is not in it, so the
        // server cannot possibly consider its handshake complete yet.
        foreach (byte[] datagram in client.GetDatagramsToSend())
            server.ProcessDatagram(datagram);
        Assert.That(server.HandshakeComplete, Is.False);

        // Flight 2: everything the server has to say so far. Nothing more is fed in afterwards.
        foreach (byte[] datagram in server.GetDatagramsToSend())
            client.ProcessDatagram(datagram);

        Assert.That(server.HandshakeComplete, Is.False,
                    "the client Finished has not been delivered, so this flight is all the server could send");
        Assert.Multiple(() =>
        {
            Assert.That(client.ServerEnablesConnectProtocol, Is.True,
                        "SETTINGS_ENABLE_CONNECT_PROTOCOL has to be known this early — see the class comment");
            Assert.That(client.ServerSupportsWebTransport, Is.True,
                        "everything WebTransport needs (SETTINGS + datagram support) travels in the same flight");
        });
    }

}
