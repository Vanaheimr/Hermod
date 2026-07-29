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

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Recovery;

/// <summary>
/// Path MTU discovery (RFC 8899, applied to QUIC by RFC 9000 §14.3): the search itself, and what a
/// real connection does with it.
/// </summary>
[TestFixture]
public class PathMtuDiscoveryTests
{

    #region The search

    [Test]
    public void BeforeItStarts_NothingIsProbedAndTheFloorHolds()
    {
        var pmtu = new PathMtuDiscovery();

        Assert.That(pmtu.MaxDatagramSize, Is.EqualTo(PathMtuDiscovery.BasePlpmtu));
        Assert.That(pmtu.NextProbeSize(), Is.Zero, "§14.3.1: not before the handshake is complete.");
    }

    [Test]
    public void AnAcknowledgedProbeRaisesTheDatagramSize()
    {
        var pmtu = new PathMtuDiscovery();
        pmtu.Start(peerMaxUdpPayloadSize: 65527);

        int probe = pmtu.NextProbeSize();
        Assert.That(probe, Is.GreaterThan(PathMtuDiscovery.BasePlpmtu));

        pmtu.OnProbeSent();
        pmtu.OnProbeAcknowledged(probe);
        Assert.That(pmtu.MaxDatagramSize, Is.EqualTo(probe));
    }

    [Test]
    public void ASingleLostProbeIsNotAVerdict()
    {
        // RFC 8899 §5.1.2: MAX_PROBES exists because one loss is far more likely to be ordinary loss
        // than a too-large packet. Giving up immediately would leave most of the gain undiscovered.
        var pmtu = new PathMtuDiscovery();
        pmtu.Start(65527);

        int probe = pmtu.NextProbeSize();
        pmtu.OnProbeSent();
        pmtu.OnProbeLost(probe);

        Assert.That(pmtu.NextProbeSize(), Is.EqualTo(probe), "The same size must be retried.");
    }

    [Test]
    public void AfterMaxProbesTheSizeIsRuledOut()
    {
        var pmtu = new PathMtuDiscovery();
        pmtu.Start(65527);

        int probe = pmtu.NextProbeSize();
        for (int attempt = 0; attempt < PathMtuDiscovery.MaxProbesPerSize; attempt++)
        {
            pmtu.OnProbeSent();
            pmtu.OnProbeLost(probe);
        }

        int next = pmtu.NextProbeSize();
        Assert.That(next, Is.LessThan(probe), "The search must come back down.");
        Assert.That(pmtu.MaxDatagramSize, Is.EqualTo(PathMtuDiscovery.BasePlpmtu),
                    "Nothing above the floor is proven yet.");
    }

    [Test]
    public void TheSearchConvergesOnTheRealLimit()
    {
        // A path that carries exactly 1400 bytes: everything up to it is acknowledged, everything
        // above is lost. The search must end at 1400 and stop.
        const int actualPathMtu = 1400;
        var pmtu = new PathMtuDiscovery(searchCeiling: 1452);
        pmtu.Start(65527);

        for (int guard = 0; guard < 100 && !pmtu.SearchComplete; guard++)
        {
            int probe = pmtu.NextProbeSize();
            if (probe == 0)
                break;
            pmtu.OnProbeSent();
            if (probe <= actualPathMtu)
                pmtu.OnProbeAcknowledged(probe);
            else
                for (int attempt = 0; attempt < PathMtuDiscovery.MaxProbesPerSize - 1; attempt++)
                {
                    pmtu.OnProbeLost(probe);
                    pmtu.OnProbeSent();
                }
            pmtu.OnProbeLost(probe);
        }

        Assert.That(pmtu.MaxDatagramSize, Is.EqualTo(actualPathMtu));
        Assert.That(pmtu.SearchComplete, Is.True);
        Assert.That(pmtu.ProbesSent, Is.LessThan(40), "A binary search, not a linear crawl.");
    }

    [Test]
    public void ThePeersMaxUdpPayloadSizeCapsTheSearch()
    {
        // §14: the peer's max_udp_payload_size "might act as an additional limit on the maximum
        // datagram size" — discovering a size the peer refuses to receive is pointless.
        var pmtu = new PathMtuDiscovery(searchCeiling: 9000);
        pmtu.Start(peerMaxUdpPayloadSize: 1300);

        int probe = pmtu.NextProbeSize();
        Assert.That(probe, Is.LessThanOrEqualTo(1300));
    }

    [Test]
    public void ACeilingAtTheFloorDisablesTheSearch()
    {
        // The documented off switch — and the one the two packet-size tests rely on.
        var pmtu = new PathMtuDiscovery(searchCeiling: PathMtuDiscovery.BasePlpmtu);
        pmtu.Start(65527);

        Assert.That(pmtu.NextProbeSize(), Is.Zero);
        Assert.That(pmtu.MaxDatagramSize, Is.EqualTo(PathMtuDiscovery.BasePlpmtu));
    }

    [Test]
    public void TheCeilingIsClampedToTheFloor()
    {
        // A caller asking for less than QUIC's minimum does not get a broken connection.
        var pmtu = new PathMtuDiscovery(searchCeiling: 500);
        Assert.That(pmtu.SearchCeiling, Is.EqualTo(PathMtuDiscovery.BasePlpmtu));
        Assert.That(pmtu.MaxDatagramSize, Is.EqualTo(PathMtuDiscovery.BasePlpmtu));
    }

    #endregion

    #region Over a real connection

    private static (QuicClientConnection, QuicServerConnection, ServerCertificate) Pair(int ceiling)
    {
        var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var client = new QuicClientConnection("localhost", certificateValidation: validation,
                                              maxDatagramSizeCeiling: ceiling);
        var server = new QuicServerConnection(cert, new TransportParameters
        {
            InitialMaxDataValue                 = 4 * 1024 * 1024,
            InitialMaxStreamDataBidiRemoteValue = 4 * 1024 * 1024,
        }, maxDatagramSizeCeiling: ceiling);
        client.Start();
        return (client, server, cert);
    }

    [Test]
    public void OnALosslessPath_TheConnectionGrowsToTheCeiling()
    {
        // In process there is no MTU at all, so every probe arrives: the search should walk all the
        // way up and settle on the ceiling.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair(1452);
        using (cert) using (client) using (server)
        {
            for (int round = 0; round < 40; round++)
            {
                foreach (byte[] datagram in client.GetDatagramsToSend())
                    server.ProcessDatagram(datagram);
                foreach (byte[] datagram in server.GetDatagramsToSend())
                    client.ProcessDatagram(datagram);
            }

            Assert.That(client.HandshakeConfirmed, Is.True);
            Assert.That(client.CurrentMaxDatagramSize, Is.EqualTo(1452),
                        "Every probe was delivered, so the ceiling must be reached.");
            Assert.That(client.PathMtu.SearchComplete, Is.True);
        }
    }

    [Test]
    public void NoProbeIsSentBeforeTheHandshakeIsConfirmed()
    {
        // §14.3.1: the search may enter the BASE state only once the handshake is complete.
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair(1452);
        using (cert) using (client) using (server)
        {
            foreach (byte[] datagram in client.GetDatagramsToSend())
            {
                Assert.That(datagram.Length, Is.LessThanOrEqualTo(PathMtuDiscovery.BasePlpmtu));
                server.ProcessDatagram(datagram);
            }
            Assert.That(client.PathMtu.ProbesSent, Is.Zero);
        }
    }

    [Test]
    public void WithDiscoveryOff_NothingEverExceedsTheFloor()
    {
        (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) =
            Pair(QuicEndpoint.MaxDatagramSize);
        using (cert) using (client) using (server)
        {
            for (int round = 0; round < 20; round++)
            {
                foreach (byte[] datagram in client.GetDatagramsToSend())
                {
                    Assert.That(datagram.Length, Is.LessThanOrEqualTo(QuicEndpoint.MaxDatagramSize));
                    server.ProcessDatagram(datagram);
                }
                foreach (byte[] datagram in server.GetDatagramsToSend())
                {
                    Assert.That(datagram.Length, Is.LessThanOrEqualTo(QuicEndpoint.MaxDatagramSize));
                    client.ProcessDatagram(datagram);
                }
            }
            Assert.That(client.PathMtu.ProbesSent, Is.Zero);
            Assert.That(client.CurrentMaxDatagramSize, Is.EqualTo(QuicEndpoint.MaxDatagramSize));
        }
    }

    [Test]
    public void ALargerMtuMeansFewerPacketsForTheSameData()
    {
        // The reason the whole thing exists. Same payload, two connections, one allowed to discover
        // a larger datagram size — the larger one must need measurably fewer packets.
        int PacketsFor(int ceiling)
        {
            (QuicClientConnection client, QuicServerConnection server, ServerCertificate cert) = Pair(ceiling);
            using (cert) using (client) using (server)
            {
                // Let the handshake finish and, where enabled, the search settle.
                for (int round = 0; round < 60; round++)
                {
                    foreach (byte[] datagram in client.GetDatagramsToSend())
                        server.ProcessDatagram(datagram);
                    foreach (byte[] datagram in server.GetDatagramsToSend())
                        client.ProcessDatagram(datagram);
                }

                QuicStream stream = client.OpenBidirectionalStream();
                stream.Write(new byte[200_000]);

                int packets = 0;
                for (int round = 0; round < 400; round++)
                {
                    foreach (byte[] datagram in client.GetDatagramsToSend())
                    {
                        packets++;
                        server.ProcessDatagram(datagram);
                    }
                    foreach (byte[] datagram in server.GetDatagramsToSend())
                        client.ProcessDatagram(datagram);
                }
                return packets;
            }
        }

        int atTheFloor = PacketsFor(QuicEndpoint.MaxDatagramSize);
        int discovered = PacketsFor(1452);

        Assert.That(discovered, Is.LessThan(atTheFloor),
                    $"A larger MTU must cost fewer packets ({discovered} vs {atTheFloor}).");
    }

    #endregion

}
