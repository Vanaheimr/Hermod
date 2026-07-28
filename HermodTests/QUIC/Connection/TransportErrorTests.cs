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

using org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC;

using org.GraphDefined.Vanaheimr.Hermod.Quic;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// Tests of the transport-error matrix (RFC 9000 §11/§20.1): protocol violations by the peer must be
/// answered with the matching error code via CONNECTION_CLOSE. Plus frame-parser errors and the new
/// PATH_CHALLENGE/PATH_RESPONSE frames.
/// </summary>
[TestFixture]
public class TransportErrorTests
{
    [Test]
    public void FrameParser_UnknownFrameType_IsAnError()
    {
        Assert.That(FrameParser.TryParseAll([0x1f], out _), Is.EqualTo(FrameParseResult.UnknownFrameType));
    }

    [Test]
    public void FrameParser_TruncatedFrame_IsAnEncodingError()
    {
        // CRYPTO frame type (0x06) without offset/length/data ⇒ incomplete.
        Assert.That(FrameParser.TryParseAll([0x06], out _), Is.EqualTo(FrameParseResult.EncodingError));
    }

    [Test]
    public void PathChallengeAndResponse_RoundTrip()
    {
        byte[] bytes = FrameParser.Serialize([new PathChallengeFrame(0x0123456789abcdef), new PathResponseFrame(0x00ff00ff00ff00ff)]);
        Assert.That(FrameParser.TryParseAll(bytes, out List<Frame> parsed), Is.EqualTo(FrameParseResult.Ok));
        Assert.That(Expect.Type<PathChallengeFrame>(parsed[0]).Data, Is.EqualTo(0x0123456789abcdefUL));
        Assert.That(Expect.Type<PathResponseFrame>(parsed[1]).Data, Is.EqualTo(0x00ff00ff00ff00ffUL));
    }

    [Test]
    public void StreamReceiveBuffer_DataBeyondFlowControlWindow_IsFlowControlError()
    {
        var buffer = new StreamReceiveBuffer { MaxData = 4 };
        Assert.That(buffer.Receive(0, new byte[5], fin: false), Is.EqualTo(StreamReceiveResult.FlowControlError));
    }

    [Test]
    public void StreamReceiveBuffer_InconsistentFinalSize_IsFinalSizeError()
    {
        var buffer = new StreamReceiveBuffer();
        Assert.That(buffer.Receive(0, new byte[4], fin: true), Is.EqualTo(StreamReceiveResult.Ok));       // final size = 4
        Assert.That(buffer.Receive(4, new byte[2], fin: false), Is.EqualTo(StreamReceiveResult.FinalSizeError)); // beyond it
    }

    // ---- Integration: STREAM_LIMIT_ERROR end-to-end --------------------------------------

    [Test]
    public void PeerExceedingStreamLimit_IsClosedWithStreamLimitError()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // The server allows the client only ONE bidirectional stream (index 0).
        var serverParams = new TransportParameters { InitialMaxStreamsBidiValue = 1 };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, serverParams);
        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        // The client opens TWO streams (0 and 1) and sends on both — stream 1 violates the limit.
        client.OpenBidirectionalStream().Write([1]);
        QuicStream second = client.OpenBidirectionalStream();
        second.Write([2]);
        for (int round = 0; round < 10; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "The server must close the connection due to the stream-limit violation.");
        // The client receives the CONNECTION_CLOSE with the correct error code.
        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.ErrorCode, Is.EqualTo((ulong)TransportError.StreamLimitError));
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
