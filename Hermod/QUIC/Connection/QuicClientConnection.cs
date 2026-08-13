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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// A client QUIC connection (RFC 9000/9001). Chooses DCID and SCID, drives the
/// <see cref="TlsClientHandshake"/> and confirms the handshake on receiving HANDSHAKE_DONE.
/// The shared transport logic lives in <see cref="QuicEndpoint"/>.
/// </summary>
public sealed class QuicClientConnection : QuicEndpoint
{
    private readonly TlsClientHandshake _tls;
    private readonly ConnectionId _originalDcid;
    private bool _dcidLearned;
    private bool _retryHandled;
    private ConnectionId _retryScid; // SCID of the Retry packet (for the §7.3 check of retry_source_connection_id)
    private List<uint> _offeredVersions = [];
    private readonly List<Frame> _applicationFrames = [];

    protected override bool IsServer => false;

    // Handshake confirmed once the client has received HANDSHAKE_DONE ⇒ Handshake keys discardable (RFC 9001 §4.9.2).
    protected override bool HandshakeIsConfirmed => HandshakeConfirmed;

    public QuicClientConnection(
        string serverName,
        TransportParameters? transportParameters = null,
        uint version = 0x0000_0001,
        CertificateValidationOptions? certificateValidation = null,
        IReadOnlyList<CipherSuite>? cipherSuites = null,
        IReadOnlyList<NamedGroup>? keyExchangeGroups = null,
        ResumptionTicket? resumptionTicket = null,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        QlogWriter? qlog = null,
        ServerCertificate? clientCertificate = null,
        byte[]? addressValidationToken = null,
        int maxDatagramSizeCeiling = PathMtuDiscovery.DefaultSearchCeiling)
        : base(transportParameters, version, timeProvider, qlog,
               maxDatagramSizeCeiling: maxDatagramSizeCeiling)
    {
        LocalParams.InitialSourceConnectionIdValue = Scid;

        // A token kept from an earlier connection to this server (RFC 9000 §8.1.3). It rides in every
        // Initial we send — "The client MUST include the token in all Initial packets it sends,
        // unless a Retry replaces the token with a newer one", and ApplyRetry does exactly that.
        if (addressValidationToken is { Length: > 0 })
            InitialToken = addressValidationToken;

        // The random DCID of the first client Initial derives the Initial keys.
        _originalDcid = new ConnectionId(RandomNumberGenerator.GetBytes(8));
        Dcid = _originalDcid;

        // Offered named groups (null ⇒ TLS default X25519+P-256); the same list as key shares and in
        // supported_groups, so e.g. X448 is offered directly (no HelloRetryRequest needed).
        // A resumptionTicket enables session resumption (PSK) for this connection.
        _tls = new TlsClientHandshake(serverName, LocalParams.Encode(), certificateValidation: certificateValidation,
            cipherSuites: cipherSuites, keyShareGroups: keyExchangeGroups, supportedGroups: keyExchangeGroups,
            resumptionTicket: resumptionTicket, timeProvider: TimeProvider, keyLog: keyLog,
            clientCertificate: clientCertificate);
        TlsHandshake = _tls;
        InstallInitialKeys(_originalDcid);
    }

    /// <summary>
    /// <c>true</c> once the client has produced its Finished.
    /// </summary>
    public bool HandshakeComplete => _tls.IsComplete;

    /// <summary>
    /// <c>true</c> when the server requested client authentication (RFC 8446 §4.3.2).
    /// </summary>
    public bool ClientCertificateRequested => _tls.ClientCertificateRequested;

    /// <summary>
    /// <c>true</c> when we answered that request with an actual certificate rather than the empty
    /// Certificate that declines it.
    /// </summary>
    public bool ClientCertificateSent => _tls.ClientCertificateSent;

    /// <summary>
    /// <c>true</c> once a server HANDSHAKE_DONE has been received.
    /// </summary>
    public bool HandshakeConfirmed { get; private set; }

    private readonly List<byte[]> _newTokens = [];

    /// <summary>
    /// Address-validation tokens the server issued for FUTURE connections (RFC 9000 §8.1.3), oldest
    /// first. §8.1.3: "sending the most recent unused token is most likely to be effective", so a
    /// caller that keeps these should take the last one.
    /// <para>
    /// Explicitly NOT the token of a Retry: §8.1.3 forbids using that one for future connections, and
    /// it never reaches this list because it arrives in a Retry packet rather than a NEW_TOKEN frame.
    /// </para>
    /// </summary>
    public IReadOnlyList<byte[]> NewTokens => _newTokens;

    /// <summary>
    /// Collects a NEW_TOKEN. §19.7: a lost packet can make the server repeat a frame, and "Clients
    /// are responsible for discarding duplicate values, which might be used to link connection
    /// attempts" — so an identical token is dropped rather than stored twice.
    /// </summary>
    protected override void OnNewTokenReceived(ReadOnlyMemory<byte> token)
    {
        ReadOnlySpan<byte> incoming = token.Span;
        foreach (byte[] known in _newTokens)
            if (known.AsSpan().SequenceEqual(incoming))
                return;

        _newTokens.Add(incoming.ToArray());
    }

    /// <summary>
    /// The address the server would rather serve this connection from (RFC 9000 §9.6), or
    /// <c>null</c> when it offered none. Only meaningful once the peer transport parameters are in.
    /// <para>
    /// §9.6.2: these addresses "are only valid for the connection in which they are provided. A
    /// client MUST NOT use these for other connections, including connections that are resumed from
    /// the current connection" — so this is not something to cache alongside a session ticket.
    /// </para>
    /// </summary>
    public PreferredAddress? ServerPreferredAddress => PeerTransportParameters?.PreferredAddressValue;

    /// <summary>
    /// Begins the move to the server's preferred address (RFC 9000 §9.6.1): switches to the
    /// connection ID that came with it and starts path validation. Returns the endpoint the caller
    /// must send to from now on, or <c>null</c> when there is nothing to migrate to.
    /// <para>
    /// The socket is the caller's, here as everywhere in this core — so this call changes the
    /// connection's view (CID, PATH_CHALLENGE) and hands back the address; redirecting the datagrams
    /// is the caller's part.
    /// </para>
    /// <para>
    /// §9.6.1 requires the handshake to be confirmed first, and the migration to complete only on
    /// successful validation: "As soon as path validation succeeds, the client SHOULD begin sending
    /// all future packets to the new server address … If path validation fails, the client MUST
    /// continue sending all future packets to the server's original IP address." Watch
    /// <see cref="QuicEndpoint.PathValidated"/> for the verdict and fall back if it never arrives.
    /// </para>
    /// </summary>
    /// <param name="family">
    /// Which address family to move to. §9.6.3: a client that has itself migrated "SHOULD use a
    /// preferred address from the same address family for the server".
    /// </param>
    public IPEndPoint? MigrateToPreferredAddress(AddressFamily family = AddressFamily.InterNetwork)
    {
        if (!HandshakeConfirmed)
            return null; // §9.6.1: "Once the handshake is confirmed" — not before
        if (ServerPreferredAddress is not { } preferred)
            return null;
        if (preferred.EndPointFor(family) is not { } endpoint)
            return null;

        // §9.6.1: the client "constructs packets using any previously unused active connection ID".
        // The CID from the parameter was registered with sequence 1 when the parameters arrived.
        Dcid = preferred.ConnectionId;
        InitiatePathValidation();
        return endpoint;
    }

    /// <summary>
    /// <c>true</c> when the server Finished MAC was verified and matched.
    /// </summary>
    public bool ServerFinishedValid => _tls.ServerFinishedValid;

    /// <summary>
    /// <c>true</c> once the server certificate incl. the CertificateVerify signature was validated.
    /// </summary>
    public bool ServerCertificateValid => _tls.ServerCertificateValid;

    /// <summary>
    /// The server's validated leaf certificate (diagnostics).
    /// </summary>
    public System.Security.Cryptography.X509Certificates.X509Certificate2? ServerCertificate => _tls.ServerCertificate;

    public CipherSuite? NegotiatedCipherSuite => _tls.NegotiatedCipherSuite;

    /// <summary>
    /// The named group negotiated in the handshake (X25519 or P-256), after a possible HRR.
    /// </summary>
    public NamedGroup? NegotiatedGroup => _tls.NegotiatedGroup;

    /// <summary>
    /// The session tickets issued by the server (RFC 8446 §4.6.1) for later resumption.
    /// </summary>
    public IReadOnlyList<ResumptionTicket> NewSessionTickets => _tls.NewSessionTickets;

    /// <summary>
    /// Diagnostics: number of received NewSessionTicket messages.
    /// </summary>
    public int NewSessionTicketMessagesSeen => _tls.NewSessionTicketMessagesSeen;

    /// <summary>
    /// <c>true</c> when this connection was established via session resumption (PSK) instead of a certificate.
    /// </summary>
    public bool ResumptionAccepted => _tls.ResumptionAccepted;

    /// <summary>
    /// <c>true</c> when 0-RTT (early_data) was accepted by the server.
    /// </summary>
    public bool EarlyDataAccepted => _tls.EarlyDataAccepted;

    /// <summary>
    /// Frames received in 1-RTT (e.g. HANDSHAKE_DONE, the HTTP/3 control stream) for inspection.
    /// </summary>
    public IReadOnlyList<Frame> ApplicationFrames => _applicationFrames;

    /// <summary>
    /// <c>true</c> when a version-negotiation packet was received (no common version).
    /// </summary>
    public bool VersionNegotiationReceived { get; private set; }

    /// <summary>
    /// The versions offered by the server in the version-negotiation packet (empty when none received).
    /// </summary>
    public IReadOnlyList<uint> OfferedVersions => _offeredVersions;

    /// <summary>
    /// <c>true</c> once a Retry was processed and the ClientHello was resent.
    /// </summary>
    public bool RetryHandled => _retryHandled;

    /// <summary>
    /// Starts the handshake (builds the ClientHello).
    /// </summary>
    public void Start() => _tls.Start();

    /// <summary>
    /// Opens a client-initiated bidirectional stream (e.g. an HTTP/3 request).
    /// Throws <see cref="QuicStreamLimitException"/> when the peer grants no more (RFC 9000 §4.6).
    /// </summary>
    public QuicStream OpenBidirectionalStream() => OpenLocalStream(bidirectional: true);

    /// <summary>
    /// Opens a client-initiated unidirectional stream (e.g. HTTP/3 control/QPACK).
    /// Throws <see cref="QuicStreamLimitException"/> when the peer grants no more (RFC 9000 §4.6).
    /// </summary>
    public QuicStream OpenUnidirectionalStream() => OpenLocalStream(bidirectional: false);

    /// <summary>
    /// Opens a client-initiated bidirectional stream, or returns <c>null</c> when the peer's limit
    /// is exhausted. A STREAMS_BLOCKED frame is queued either way (§19.14).
    /// </summary>
    public QuicStream? TryOpenBidirectionalStream() => TryOpenLocalStream(bidirectional: true);

    /// <summary>
    /// Opens a client-initiated unidirectional stream, or returns <c>null</c> when the peer's limit
    /// is exhausted. A STREAMS_BLOCKED frame is queued either way (§19.14).
    /// </summary>
    public QuicStream? TryOpenUnidirectionalStream() => TryOpenLocalStream(bidirectional: false);

    protected override void OnLongHeaderPacket(LongPacketType type, LongHeaderPrefix prefix)
    {
        if (!_dcidLearned)
        {
            Dcid = prefix.SourceConnectionId; // the server SCID becomes our DCID
            _dcidLearned = true;
        }
    }

    protected override void HandleVersionNegotiationPacket(ReadOnlySpan<byte> datagram)
    {
        // RFC 9000 §6.2: discard VN when another packet was already processed or a VN was already received.
        if (AnyPacketProcessed || VersionNegotiationReceived)
            return;
        if (!VersionNegotiationPacket.TryParse(datagram, out _, out _, out List<uint> versions))
            return;
        // Discard VN when it lists the version chosen by the client (spurious/forged VN).
        if (versions.Contains(Version))
            return;

        _offeredVersions = versions;
        VersionNegotiationReceived = true; // v1-only client: no common version → give up the connection.
    }

    protected override void HandleRetryPacket(ReadOnlySpan<byte> datagram)
    {
        // Process exactly one Retry and only before a real packet arrived (RFC 9000 §17.2.5.2).
        if (_retryHandled || AnyPacketProcessed)
            return;
        if (!RetryPacket.TryParse(datagram, out uint version, out _, out ConnectionId retrySource, out byte[] token, out _))
            return;
        if (version != Version)
            return;
        // Discard a Retry with SCID == our own DCID (loop protection, RFC 9000 §17.2.5.2).
        if (retrySource.Span.SequenceEqual(_originalDcid.Span))
            return;
        // Verify the integrity tag over the original DCID (RFC 9001 §5.8).
        if (!RetryPacket.Verify(datagram, _originalDcid))
            return;

        _retryHandled = true;
        _retryScid = retrySource;            // for the §7.3 check of retry_source_connection_id
        _dcidLearned = true;                 // change the DCID only once (RFC 9000 §7.2): now = Retry SCID
        ApplyRetry(retrySource, token);      // new Initial keys + token + CRYPTO offset 0
        _tls.ResendClientHello();
    }

    /// <summary>
    /// Client side of the parameter authentication (RFC 9000 §7.3): in addition to the base check,
    /// the server MUST send original_destination_connection_id (= the DCID of our very first Initial)
    /// and retry_source_connection_id EXACTLY when a Retry took place (with the SCID of the Retry
    /// packet) — this prevents an attacker from forging or suppressing Retry packets.
    /// </summary>
    internal override string? ValidatePeerTransportParameters(TransportParameters p)
    {
        if (base.ValidatePeerTransportParameters(p) is { } baseProblem)
            return baseProblem;
        if (p.OriginalDestinationConnectionIdValue is not { } odcid)
            return "missing original_destination_connection_id"; // §7.3: absence from the server is fatal
        if (!odcid.Span.SequenceEqual(_originalDcid.Span))
            return "original_destination_connection_id mismatch";
        if (_retryHandled)
        {
            if (p.RetrySourceConnectionIdValue is not { } rscid)
                return "missing retry_source_connection_id after Retry";
            if (!rscid.Span.SequenceEqual(_retryScid.Span))
                return "retry_source_connection_id mismatch";
        }
        else if (p.RetrySourceConnectionIdValue is not null)
            return "retry_source_connection_id without Retry";
        return null;
    }

    protected override void OnHandshakeDoneReceived() => HandshakeConfirmed = true;

    // RFC 9001 §4.1.2: the client may also confirm the handshake when one of its 1-RTT packets is
    // acknowledged – so the Handshake keys may be discarded even before a (lost) HANDSHAKE_DONE.
    protected override void OnOneRttPacketAcknowledged() => HandshakeConfirmed = true;

    protected override void OnApplicationFrameHandled(Frame frame) => _applicationFrames.Add(frame);
}
