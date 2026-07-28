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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Checks sending large CRYPTO data across multiple packets (RFC 9000 §12.2): a ClientHello with the
/// PQ hybrid X25519MLKEM768 (1216-byte key share ⇒ ~1450-byte ClientHello) does not fit into a single
/// 1200-byte Initial and must be spread offset-correctly across multiple Initials. The server side
/// reassembles them and the handshake completes — over the real datagram path (not in-process).
/// </summary>
[TestFixture]
public class MultiInitialSendTests
{
    [Test]
    public void LargePqClientHello_IsSplitAcrossMultipleMtuSizedInitials_AndHandshakeCompletes()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation,
            keyExchangeGroups: [NamedGroup.X25519MlKem768]);
        using var server = new QuicServerConnection(cert, preferredGroups: [NamedGroup.X25519MlKem768]);

        client.Start();

        // First flight of the client: the large ClientHello must be split across ≥2 Initials, and NO
        // datagram may exceed the QUIC-safe MTU (otherwise fragmentation/drops loom in the network).
        IReadOnlyList<byte[]> firstFlight = client.GetDatagramsToSend();
        Assert.That(firstFlight.Count >= 2, Is.True, $"The PQ ClientHello should be spread across ≥2 Initials, was {firstFlight.Count}.");
        foreach (byte[] datagram in firstFlight)
            Assert.That(datagram.Length <= 1252, Is.True, $"Initial datagram of {datagram.Length} bytes exceeds the MTU (1252).");

        foreach (byte[] dg in firstFlight)
            server.ProcessDatagram(dg);

        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
        {
            foreach (byte[] dg in server.GetDatagramsToSend())
                client.ProcessDatagram(dg);
            foreach (byte[] dg in client.GetDatagramsToSend())
                server.ProcessDatagram(dg);
        }

        Assert.That(client.HandshakeConfirmed, Is.True, "Handshake must complete despite the split ClientHello.");
        Assert.That(client.NegotiatedGroup, Is.EqualTo(NamedGroup.X25519MlKem768));
    }
}
