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
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// A server QUIC connection (RFC 9000/9001). Derives the Initial keys from the DCID chosen by the
/// client, drives the <see cref="TlsServerHandshake"/> and sends HANDSHAKE_DONE after the completed
/// handshake. Optionally it enforces address validation via Retry (RFC 9000 §8.1) and answers
/// unsupported versions with a version-negotiation packet (§6). The shared transport logic lives in
/// <see cref="QuicEndpoint"/>.
/// </summary>
public sealed class QuicServerConnection : QuicEndpoint
{
    /// <summary>
    /// The outcome of a Retry that was carried out <b>outside</b> this connection — statelessly, by
    /// the listener, before any connection existed (RFC 9000 §8.1.2: "a server has not established
    /// any state for the connection at this point"). Both values come out of the validated Retry
    /// token: the DCID of the client's original Initial, and the source connection ID the server put
    /// into its Retry packet.
    /// </summary>
    /// <param name="OriginalDestinationConnectionId">
    /// D0 — goes into the <c>original_destination_connection_id</c> transport parameter and proves to
    /// the client that we (or a peer we cooperate with) saw its first Initial.
    /// </param>
    /// <param name="RetrySourceConnectionId">
    /// The SCID of the Retry packet. It is the client's DCID from now on (§7.2), so this connection
    /// must adopt it as its own SCID and echo it in <c>retry_source_connection_id</c>.
    /// </param>
    public sealed record ValidatedRetry(ConnectionId OriginalDestinationConnectionId,
                                        ConnectionId RetrySourceConnectionId);

    private readonly ServerCertificate _certificate;
    private readonly KeyLog? _keyLog; // optional NSS key log for Wireshark; see KeyLog
    private readonly bool _requireRetry;
    private readonly IReadOnlyList<CipherSuite>? _preferredCipherSuites;
    private readonly IReadOnlyList<NamedGroup>? _preferredGroups;
    private readonly ServerResumptionCache? _resumptionCache;
    private readonly uint _maxEarlyDataSize;
    private readonly ClientCertificateOptions? _clientCertificate; // mutual TLS (RFC 8446 §4.3.2)
    private TlsServerHandshake? _serverTls;
    private bool _handshakeDoneSent;
    private bool _retrySent;
    private bool _retryValidatedExternally; // the token was already checked by the listener
    private byte[] _retryToken = [];
    private ConnectionId _originalDcid = ConnectionId.Empty;
    private readonly List<ulong> _newlyOpenedRequestStreams = [];

    protected override bool IsServer => true;

    // The server confirms the handshake at its completion ⇒ Handshake keys discardable (RFC 9001 §4.9.2).
    protected override bool HandshakeIsConfirmed => HandshakeComplete;

    public QuicServerConnection(
        ServerCertificate certificate,
        TransportParameters? transportParameters = null,
        uint version = 0x0000_0001,
        bool requireRetry = false,
        IReadOnlyList<CipherSuite>? preferredCipherSuites = null,
        IReadOnlyList<NamedGroup>? preferredGroups = null,
        ServerResumptionCache? resumptionCache = null,
        uint maxEarlyDataSize = 0,
        StatelessResetTokenGenerator? statelessResetTokens = null,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        QlogWriter? qlog = null,
        ValidatedRetry? validatedRetry = null,
        ClientCertificateOptions? clientCertificate = null,
        int maxDatagramSizeCeiling = PathMtuDiscovery.DefaultSearchCeiling,
        PreferredAddress? preferredAddress = null)
        : base(transportParameters, version, timeProvider, qlog,
               sourceConnectionId: validatedRetry?.RetrySourceConnectionId,
               maxDatagramSizeCeiling: maxDatagramSizeCeiling)
    {
        _keyLog = keyLog;
        _certificate = certificate;
        // A Retry already carried out statelessly counts as one that happened: the token is checked,
        // the address is proven, and we must not (indeed cannot, §8.1.2) send a second one.
        _requireRetry = requireRetry || validatedRetry is not null;
        if (validatedRetry is { } retry)
        {
            _originalDcid = retry.OriginalDestinationConnectionId;
            _retrySent = true;
            _retryValidatedExternally = true;
            MarkAddressValidated(); // the returned token proved the client address (RFC 9000 §8.1)
        }
        // RFC 9000 §9.6/§18.2: only a server sends preferred_address, and it MUST NOT carry a
        // zero-length connection ID — the client would have to treat that as a connection error, so
        // it is refused here rather than put on the wire.
        if (preferredAddress is { } preferred)
        {
            if (preferred.ConnectionId.Length == 0)
                throw new ArgumentException("A preferred address requires a non-empty connection ID (RFC 9000 §18.2).",
                                            nameof(preferredAddress));
            LocalParams.PreferredAddressValue = preferred;
        }

        _preferredCipherSuites = preferredCipherSuites;
        _preferredGroups = preferredGroups;
        StatelessResetTokens = statelessResetTokens; // tokens derivable from the CID ⇒ stateless reset sendable
        _resumptionCache = resumptionCache;
        _maxEarlyDataSize = maxEarlyDataSize;
        _clientCertificate = clientCertificate;
    }

    /// <summary>
    /// The outcome of client authentication (mutual TLS, RFC 8446 §4.3.2): whether one was asked
    /// for, the validated certificate if there is one, and why validation failed if it did.
    /// </summary>
    public ClientAuthenticationResult ClientAuthentication =>
        _serverTls?.ClientAuthentication ?? ClientAuthenticationResult.NotRequested;

    /// <summary>
    /// <c>true</c> when the handshake ran via session resumption (PSK).
    /// </summary>
    public bool ResumptionAccepted => _serverTls?.ResumptionAccepted ?? false;

    /// <summary>
    /// <c>true</c> when 0-RTT (early_data) was accepted.
    /// </summary>
    public bool EarlyDataAccepted => _serverTls?.EarlyDataAccepted ?? false;

    /// <summary>
    /// <c>true</c> once the server has sent a Retry for address validation.
    /// </summary>
    public bool SentRetry => _retrySent;

    /// <summary>
    /// <c>true</c> once the client Finished was verified and the handshake is in place.
    /// </summary>
    public bool HandshakeComplete => _serverTls is { IsComplete: true, ClientFinishedValid: true };

    /// <summary>
    /// The negotiated cipher suite — the counterpart of
    /// <see cref="QuicClientConnection.NegotiatedCipherSuite"/>, so that a server can report what a
    /// foreign client actually chose.
    /// </summary>
    public CipherSuite? NegotiatedCipherSuite => _serverTls?.NegotiatedCipherSuite;

    /// <summary>
    /// The negotiated key-exchange group, counterpart of
    /// <see cref="QuicClientConnection.NegotiatedGroup"/>.
    /// </summary>
    public NamedGroup? NegotiatedGroup => _serverTls?.NegotiatedGroup;

    /// <summary>
    /// Opens a server-initiated unidirectional stream (HTTP/3 control/QPACK).
    /// </summary>
    public QuicStream OpenUnidirectionalStream() => OpenLocalStream(bidirectional: false);

    /// <summary>
    /// Opens a server-initiated bidirectional stream (e.g. a server-side WebTransport bidi stream,
    /// RFC draft webtrans-http3 §4.2).
    /// </summary>
    public QuicStream OpenBidirectionalStream() => OpenLocalStream(bidirectional: true);

    /// <summary>
    /// Bidirectional (request) streams newly opened by the client since the last call.
    /// </summary>
    public IReadOnlyList<ulong> TakeNewRequestStreams()
    {
        var result = _newlyOpenedRequestStreams.ToList();
        _newlyOpenedRequestStreams.Clear();
        return result;
    }

    protected override void OnLongHeaderPacket(LongPacketType type, LongHeaderPrefix prefix)
    {
        if (TlsHandshake is not null || type != LongPacketType.Initial)
            return;

        Dcid = prefix.SourceConnectionId; // the client SCID becomes our DCID (target for Retry/answer)

        // Address validation (RFC 9000 §8.1): answer the first tokenless Initial with a Retry.
        if (_requireRetry && !_retrySent)
        {
            _originalDcid = prefix.DestinationConnectionId; // D0 – goes into the integrity tag and the ODCID TP
            _retryToken = RandomNumberGenerator.GetBytes(16);
            // Retry: DCID = client SCID, SCID = our own Scid (remains the client's DCID from now on), tag over D0.
            EnqueueDatagram(RetryPacket.Build(Version, prefix.SourceConnectionId, Scid, _retryToken, _originalDcid));
            _retrySent = true;
            return; // no keys/TLS yet – only the renewed, token-carrying Initial counts
        }

        // After a Retry: accept only an Initial with exactly our token. After a STATELESS Retry we
        // never held one — the listener validated the token cryptographically and handed us the
        // result, so there is nothing left to compare here.
        if (_requireRetry && !_retryValidatedExternally && !prefix.Token.AsSpan().SequenceEqual(_retryToken))
            return;
        if (_requireRetry)
            MarkAddressValidated(); // a valid Retry token proves the client address (RFC 9000 §8.1)

        // After a Retry both sides derive the Initial keys from THE DCID OF THIS Initial (= our Scid).
        ConnectionId initialKeyDcid = prefix.DestinationConnectionId;

        LocalParams.InitialSourceConnectionIdValue = Scid;
        LocalParams.OriginalDestinationConnectionIdValue = _requireRetry ? _originalDcid : initialKeyDcid;
        if (_requireRetry)
            LocalParams.RetrySourceConnectionIdValue = Scid;
        // Announce the stateless-reset token for the handshake CID (RFC 9000 §10.3/§18.2).
        // Token of the handshake CID: derive it from the CID (when a generator is set), so it stays
        // recomputable for a stateless reset after state loss; otherwise random.
        LocalParams.StatelessResetTokenValue = StatelessResetTokens?.ComputeToken(Scid.Span) ?? RandomNumberGenerator.GetBytes(16);

        _serverTls = new TlsServerHandshake(_certificate, LocalParams.Encode(),
            preferredCipherSuites: _preferredCipherSuites, preferredGroups: _preferredGroups,
            resumptionCache: _resumptionCache, maxEarlyDataSize: _maxEarlyDataSize, keyLog: _keyLog,
            clientCertificate: _clientCertificate);
        TlsHandshake = _serverTls;

        InstallInitialKeys(initialKeyDcid);
    }

    /// <summary>
    /// Server side of the parameter check (RFC 9000 §18.2): a client MUST NOT send the server-only
    /// parameters (original_destination_connection_id, preferred_address, retry_source_connection_id,
    /// stateless_reset_token) — receiving them is a TRANSPORT_PARAMETER_ERROR.
    /// </summary>
    internal override string? ValidatePeerTransportParameters(TransportParameters p)
    {
        if (base.ValidatePeerTransportParameters(p) is { } baseProblem)
            return baseProblem;
        if (p.OriginalDestinationConnectionIdValue is not null)
            return "client sent original_destination_connection_id";
        if (p.RetrySourceConnectionIdValue is not null)
            return "client sent retry_source_connection_id";
        if (p.StatelessResetTokenValue is not null)
            return "client sent stateless_reset_token";
        if (p.SawPreferredAddress)
            return "client sent preferred_address";
        return null;
    }

    protected override void HandleUnsupportedVersion(ReadOnlySpan<byte> datagram)
    {
        // Anti-amplification (RFC 9000 §6.1/§14.1): no VN for a datagram smaller than the smallest
        // permissible Initial (1200 B) – otherwise the VN packet would be an amplifier for spoofed senders.
        if (datagram.Length < InitialPacketFactory.MinimumClientInitialSize)
            return;

        // RFC 9000 §6.1: answer with a version-negotiation packet listing the supported version(s).
        if (!LongHeader.TryParseInvariant(datagram, out _, out ConnectionId dcid, out ConnectionId scid))
            return;

        // Include a reserved GREASE version (pattern 0x?a?a?a?a, RFC 9000 §6.3): probes whether the
        // client correctly ignores unknown versions and prevents ossification of version negotiation.
        uint grease = (BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)) & 0xF0F0F0F0u) | 0x0A0A0A0Au;

        // Swap DCID/SCID: the client's SCID becomes the DCID of the VN packet.
        EnqueueDatagram(VersionNegotiationPacket.Build(scid, dcid, [Version, grease]));
    }

    /// <summary>
    /// Test helper: suppresses sending HANDSHAKE_DONE (to check the client's 1-RTT ACK confirmation).
    /// </summary>
    internal bool SuppressHandshakeDoneForTest { get; set; }

    /// <summary>
    /// A token for the client's NEXT connection (RFC 9000 §8.1.3), handed in by whoever knows the
    /// client's address — this class is deliberately address-free, exactly like the stateless Retry
    /// path. Sent once, after the handshake is complete; <c>null</c> means "issue none".
    /// </summary>
    public byte[]? NewTokenToSend { get; set; }

    /// <summary>
    /// Records that the client address was proven outside this object — by a valid NEW_TOKEN token
    /// (RFC 9000 §8.1.3), which the listener checks because only it knows the address. Unlike a
    /// Retry this leaves the connection IDs alone: no Retry happened, so there is no
    /// <c>original_destination_connection_id</c> to override.
    /// </summary>
    public void MarkClientAddressValidated() => MarkAddressValidated();

    private bool _newTokenSent;

    protected override void AddApplicationControlFrames(List<Frame> frames)
    {
        if (HandshakeComplete && !_handshakeDoneSent && !SuppressHandshakeDoneForTest)
        {
            frames.Add(HandshakeDoneFrame.Instance);
            _handshakeDoneSent = true;
        }

        // Only after the handshake: before that the address is not proven, and a token handed to an
        // unvalidated address would be exactly the amplification lever §8.1 exists to remove.
        if (HandshakeComplete && !_newTokenSent && NewTokenToSend is { Length: > 0 } token)
        {
            frames.Add(new NewTokenFrame(token));
            _newTokenSent = true;
        }
    }

    protected override void OnStreamOpened(StreamId id, bool isNew)
    {
        if (isNew && id.IsClientInitiated && id.IsBidirectional)
            _newlyOpenedRequestStreams.Add(id.Value);
    }
}
