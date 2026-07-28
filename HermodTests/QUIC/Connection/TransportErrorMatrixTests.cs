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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.QUIC.Connection;

/// <summary>
/// The rest of the transport-error matrix (RFC 9000 §11/§20.1): TRANSPORT_PARAMETER_ERROR
/// (§7.3 authentication + §7.4/§18.2 value ranges) and the CONNECTION flow-control limit
/// (§4.1, FLOW_CONTROL_ERROR over the sum of all streams).
/// </summary>
[TestFixture]
public class TransportErrorMatrixTests
{
    // ---- Unit: TryDecode hardening (§7.4/§18.2) --------------------------------------------

    [Test]
    public void TryDecode_RejectsInvalidParameterValues()
    {
        // Duplicates (§7.4 MUST NOT — also applies to unknown IDs).
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x01, [0x44, 0x00]), (0x01, [0x44, 0x00])), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x77, []), (0x77, [])), out _), Is.False);

        // max_udp_payload_size < 1200 is invalid (§18.2); 1200 itself is valid.
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x03, VarIntBytes(1199))), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x03, VarIntBytes(1200))), out _), Is.True);

        // active_connection_id_limit MUST be at least 2 (§18.2).
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x0e, VarIntBytes(1))), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x0e, VarIntBytes(2))), out _), Is.True);

        // Stream limits above 2^60 are not permitted (§4.6).
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x08, VarIntBytes((1UL << 60) + 1))), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x09, VarIntBytes((1UL << 60) + 1))), out _), Is.False);

        // stateless_reset_token: exactly 16 bytes (§18.2).
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x02, new byte[15])), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x02, new byte[16])), out _), Is.True);

        // Connection IDs above 20 bytes (§17.2) — must NOT throw an exception, only return false.
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x0f, new byte[21])), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x00, new byte[21])), out _), Is.False);
        Assert.That(TransportParameters.TryDecode(EncodeParams((0x0f, new byte[20])), out _), Is.True);
    }

    // ---- Unit: §7.3 authentication (validator) ---------------------------------------------

    [Test]
    public void Validator_RequiresMatchingInitialSourceConnectionId()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);

        // Missing initial_source_connection_id ⇒ error (§7.3: absence is fatal).
        Assert.That(Decode(EncodeParams((0x04, VarIntBytes(1000)))), Is.Not.Null);
        Assert.That(client.ValidatePeerTransportParameters(Decode(EncodeParams((0x04, VarIntBytes(1000))))!),
                    Does.Contain("missing initial_source_connection_id"));

        // Wrong initial_source_connection_id ⇒ mismatch.
        Assert.That(client.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, [9, 9, 9])))!),
                    Does.Contain("initial_source_connection_id mismatch"));

        // Matching ISCID + matching ODCID (before the handshake, Dcid = the original DCID) ⇒ ok.
        byte[] good = EncodeParams((0x0f, client.DcidForTest.Span.ToArray()), (0x00, client.DcidForTest.Span.ToArray()));
        Assert.That(client.ValidatePeerTransportParameters(Decode(good)!), Is.Null);
    }

    [Test]
    public void Validator_Client_ChecksOriginalDestinationAndRetryCid()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        byte[] iscid = client.DcidForTest.Span.ToArray();

        // Missing original_destination_connection_id from the server ⇒ error (§7.3).
        Assert.That(client.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid)))!),
                    Does.Contain("missing original_destination_connection_id"));

        // Wrong ODCID ⇒ mismatch (otherwise an attacker could forge the first Initial).
        Assert.That(client.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid), (0x00, [1, 2, 3])))!),
                    Does.Contain("original_destination_connection_id mismatch"));

        // retry_source_connection_id WITHOUT a Retry having happened ⇒ error (§7.3).
        Assert.That(client.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid), (0x00, iscid), (0x10, [5])))!),
                    Does.Contain("retry_source_connection_id without Retry"));
    }

    [Test]
    public void Validator_Server_RejectsServerOnlyParametersFromClient()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        using var server = new QuicServerConnection(cert);
        byte[] iscid = server.DcidForTest.Span.ToArray(); // before the handshake: empty DCID

        Assert.That(server.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid)))!), Is.Null);
        Assert.That(server.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid), (0x00, [1])))!),
                    Does.Contain("original_destination_connection_id"));
        Assert.That(server.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid), (0x10, [1])))!),
                    Does.Contain("retry_source_connection_id"));
        Assert.That(server.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid), (0x02, new byte[16])))!),
                    Does.Contain("stateless_reset_token"));
        Assert.That(server.ValidatePeerTransportParameters(Decode(EncodeParams((0x0f, iscid), (0x0d, new byte[41])))!),
                    Does.Contain("preferred_address"));
    }

    // ---- End-to-End: TRANSPORT_PARAMETER_ERROR ---------------------------------------------

    [Test]
    public void ClientSendingServerOnlyParameter_ClosesWithTransportParameterError()
    {
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };

        // An "evil" client smuggles the server-only parameter ODCID into its transport parameters.
        var badParams = new TransportParameters { OriginalDestinationConnectionIdValue = new ConnectionId([1, 2, 3]) };
        using var client = new QuicClientConnection("localhost", badParams, certificateValidation: validation);
        using var server = new QuicServerConnection(cert);

        client.Start();
        for (int round = 0; round < 20 && client.PeerCloseFrame is null; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "The server must close due to TRANSPORT_PARAMETER_ERROR.");
        Assert.That(client.PeerCloseFrame, Is.Not.Null,
            "The client must be able to read the CONNECTION_CLOSE — §10.2.3: at Initial+Handshake level before the handshake is confirmed.");
        Assert.That(client.PeerCloseFrame!.ErrorCode, Is.EqualTo((ulong)TransportError.TransportParameterError));
        Assert.That(client.HandshakeConfirmed, Is.False);
    }

    // ---- End to end: CONNECTION flow control (§4.1) ----------------------------------------

    [Test]
    public void ExceedingConnectionFlowControl_ClosesWithFlowControlError()
    {
        // The server grants only an 8 KiB CONNECTION window (stream windows stay large).
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var serverParams = new TransportParameters { InitialMaxDataValue = 8192 };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, serverParams);

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        // A well-behaved client honors the limit — the seam overrides it to provoke the violation.
        client.OverrideConnSendLimitForTest(1_000_000);
        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(new byte[40_000]);
        for (int round = 0; round < 40 && client.PeerCloseFrame is null; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.True, "The server must close due to FLOW_CONTROL_ERROR.");
        Assert.That(client.PeerCloseFrame, Is.Not.Null);
        Assert.That(client.PeerCloseFrame!.ErrorCode, Is.EqualTo((ulong)TransportError.FlowControlError));
    }

    [Test]
    public void StayingWithinConnectionFlowControl_DoesNotClose()
    {
        // Counter-check: the same amount of data WITHIN the window stays error-free.
        using var cert = ServerCertificate.CreateSelfSigned("localhost");
        var validation = new CertificateValidationOptions { CustomTrustRoots = [cert.Certificate] };
        var serverParams = new TransportParameters { InitialMaxDataValue = 65536 };
        using var client = new QuicClientConnection("localhost", certificateValidation: validation);
        using var server = new QuicServerConnection(cert, serverParams);

        client.Start();
        for (int round = 0; round < 20 && !client.HandshakeConfirmed; round++)
            Pump(client, server);
        Assert.That(client.HandshakeConfirmed, Is.True);

        QuicStream stream = client.OpenBidirectionalStream();
        stream.Write(new byte[40_000]);
        for (int round = 0; round < 60; round++)
            Pump(client, server);

        Assert.That(server.IsClosing, Is.False);
        Assert.That(client.IsClosing, Is.False);
        Assert.That(server.Streams[stream.Id.Value].Receive.HighestReceivedOffset, Is.EqualTo(40_000UL));
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static byte[] EncodeParams(params (ulong Id, byte[] Value)[] parameters)
    {
        var writer = new BufferWriter(128);
        try
        {
            foreach ((ulong id, byte[] value) in parameters)
            {
                writer.WriteVarInt(id);
                writer.WriteVarInt((ulong)value.Length);
                writer.WriteBytes(value);
            }
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    private static TransportParameters? Decode(byte[] bytes)
        => TransportParameters.TryDecode(bytes, out TransportParameters? p) ? p : null;

    private static byte[] VarIntBytes(ulong value)
    {
        var writer = new BufferWriter(8);
        try
        {
            writer.WriteVarInt(value);
            return writer.WrittenSpan.ToArray();
        }
        finally { writer.Dispose(); }
    }

    private static void Pump(QuicClientConnection client, QuicServerConnection server)
    {
        client.CheckLossDetectionTimeout();
        foreach (byte[] dg in client.GetDatagramsToSend())
            server.ProcessDatagram(dg);
        foreach (byte[] dg in server.GetDatagramsToSend())
            client.ProcessDatagram(dg);
    }
}
