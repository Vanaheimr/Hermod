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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Diagnostics;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Qlog;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Recovery;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// Shared base of <see cref="QuicClientConnection"/> and <see cref="QuicServerConnection"/>:
/// encryption levels, packet-number spaces, CRYPTO reassembly, key installation, streams,
/// flow control and loss recovery – all direction-independent. The role differences (key
/// direction, connection-ID assignment, stream perspective, HANDSHAKE_DONE) are mapped via
/// <see cref="IsServer"/> and the overridable hooks.
/// </summary>
public abstract class QuicEndpoint : IDisposable
{
    protected const int LevelCount = 3;

    protected readonly uint Version;
    protected readonly TransportParameters LocalParams;
    protected ConnectionId Scid;
    protected ConnectionId Dcid;

    /// <summary>
    /// The TLS engine of this role. On the server, only set after the first client Initial.
    /// </summary>
    protected ITlsHandshake? TlsHandshake;

    protected readonly PacketProtection?[] WriteKeys = new PacketProtection?[LevelCount];
    protected readonly PacketProtection?[] ReadKeys = new PacketProtection?[LevelCount];
    protected readonly PacketNumberSpace[] Spaces = [new(), new(), new()];
    protected readonly Dictionary<ulong, QuicStream> StreamMap = [];
    protected TransportParameters? PeerParams;

    private readonly CryptoStreamAssembler[] _recvCrypto = [new(), new(), new()];
    private readonly long[] _deliveredCrypto = new long[LevelCount];
    private readonly ulong[] _sendCryptoOffset = new ulong[LevelCount];
    private readonly List<byte>[] _outgoingCrypto = [[], [], []]; // CRYPTO still to send per level (stays buffered until sent)
    private bool _handshakeKeysInstalled;
    private bool _appKeysInstalled;

    // Discarding the Initial/Handshake keys after the handshake (RFC 9001 §4.9.1/§4.9.2), so that NO
    // Initial/Handshake packets are sent afterwards (otherwise a PTO wrongly probes these spaces).
    private bool _initialKeysDiscarded;
    private bool _handshakeKeysDiscarded;
    private bool _handshakePacketSent;      // client: has sent ≥1 Handshake packet
    private bool _handshakePacketReceived;  // server: has processed ≥1 Handshake packet
    private ulong? _firstOneRttPacketNumber; // PN of the first packet sent with 1-RTT keys (RFC 9001 §4.1.2)

    // 0-RTT (RFC 9001 §4): its own key set from the client_early_traffic_secret. Client→server only, so
    // on the client the write key, on the server the read key; the packets run in the application packet-number space.
    private PacketProtection? _zeroRttKeys;
    private bool _zeroRttInstalled;
    private bool _zeroRttDataSent;         // client: 0-RTT packets were sent
    private ulong _zeroRttMaxPacketNumber; // highest PN sent as 0-RTT (boundary to 1-RTT)
    private bool _zeroRttRejectionHandled;
    // Server: deadline (ticks) at which the 0-RTT read keys are discarded after the first 1-RTT packet
    // (RFC 9001 §4.9.3, RECOMMENDED 3×PTO). −1 = not yet armed.
    private long _serverZeroRttDiscardDeadlineTicks = -1;
    private bool _serverOneRttPacketReceived; // server: has received ≥1 genuine 1-RTT packet (upper bound of the 0-RTT PNs known)

    // Anti-amplification (RFC 9000 §8.1): before address validation the server may send at most 3× as
    // many bytes as it received. For the client, the address is validated by construction.
    private long _amplificationReceived;
    private long _amplificationSent;
    private bool _addressValidated;

    // 1-RTT key update (RFC 9001 §6): current traffic secrets, key phase per direction and the "next"
    // read keys prepared for the opposite direction (plus the briefly retained previous ones for reordering).
    private TrafficKeys? _appWriteTk;
    private TrafficKeys? _appReadTk;
    private TrafficKeys? _nextAppReadTk;
    private PacketProtection? _nextAppReadKeys;
    private PacketProtection? _prevAppReadKeys;
    private System.Security.Cryptography.HashAlgorithmName _appHash;
    private int _appHashLength;
    private AeadAlgorithm _appAead; // AEAD of the negotiated suite (AES-GCM or ChaCha20-Poly1305)
    private bool _sendKeyPhase;
    private bool _recvKeyPhase;
    private uint _keyUpdateCount;

    private ulong _nextLocalBidiIndex;
    private ulong _nextLocalUniIndex;
    private ulong _connSendUsed;
    private ulong _connSendLimit;
    private ulong _localConnMaxData;
    private ReceiveWindowTuner? _connWindowTuner; // auto-tuning of the connection receive window (phase 9)

    // Auto-tuning upper bounds: the receive windows may grow up to here (BDP of large, high-latency paths).
    private const ulong MaxStreamReceiveWindow = 16UL * 1024 * 1024;
    private const ulong MaxConnReceiveWindow = 24UL * 1024 * 1024;

    private readonly LossRecovery _recovery = new();
    private readonly Pacer _pacer = new();
    private readonly IdleTimeout _idle = new();
    private bool _idleTimedOut;
    private TimeSpan? _keepAliveInterval; // keep-alive PING interval (RFC 9000 §10.1.2); null = off
    private bool _pingPending;
    // Monotonic connection clock via TimeProvider (default: the system clock). All timers below run
    // on ticks elapsed since construction; a test-injected TimeProvider makes them deterministic.
    private readonly TimeProvider _timeProvider;
    private readonly long _startTimestamp;
    private readonly List<Frame>[] _retransmitQueue = [[], [], []];
    private readonly Queue<byte[]> _pendingDatagrams = new(); // raw datagrams (version negotiation / Retry)
    private bool _anyPacketProcessed;

    private ConnectionState _state = ConnectionState.Active;
    private ConnectionCloseFrame? _closeFrame;   // our own CONNECTION_CLOSE (in the closing state)
    private bool _closePacketPending;            // whether a close packet is to be sent (again)
    private long _closeDeadlineTicks = -1;       // end of the closing/draining period

    private readonly ConnectionIdManager _cids;  // connection-ID management (RFC 9000 §5.1)

    /// <summary>
    /// Optional generator for stateless-reset tokens derivable from the CID (RFC 9000 §10.3.1). When
    /// set (server), issued tokens are formed via HMAC from the CID and are thus recomputable after
    /// state loss, which is what makes sending a stateless reset possible in the first place.
    /// </summary>
    protected StatelessResetTokenGenerator? StatelessResetTokens { get; set; }
    private readonly List<Frame> _pendingControlFrames = []; // pending control frames (NEW_/RETIRE_CONNECTION_ID, PATH_CHALLENGE/RESPONSE)
    private ulong? _pathChallengePending; // the 8 PATH_CHALLENGE bytes we sent, while outstanding (RFC 9000 §8.2)
    private long _pathValidationDeadlineTicks = -1;

    /// <summary>
    /// Retry token attached to the next Initial (empty while no Retry has been received).
    /// </summary>
    protected byte[] InitialToken = [];

    /// <summary>
    /// Connection state (RFC 9000 §10.2): active, closing, draining or closed.
    /// </summary>
    private enum ConnectionState { Active, Closing, Draining, Closed }

    /// <param name="sourceConnectionId">
    /// Our own connection ID. Normally random — supplied only when it has already been announced to
    /// the peer, i.e. after a <b>stateless</b> Retry: the SCID of that Retry packet is the client's
    /// DCID from then on (RFC 9000 §7.2), so the connection created afterwards has to adopt it.
    /// </param>
    protected QuicEndpoint(TransportParameters? transportParameters, uint version, TimeProvider? timeProvider = null,
                           QlogWriter? qlog = null, ConnectionId? sourceConnectionId = null,
                           int maxDatagramSizeCeiling = PathMtuDiscovery.DefaultSearchCeiling)
    {
        _pmtu = new PathMtuDiscovery(maxDatagramSizeCeiling);
        _recovery.OnPmtuProbeAcked = _pmtu.OnProbeAcknowledged;
        _recovery.OnPmtuProbeLost  = _pmtu.OnProbeLost;

        _timeProvider = timeProvider ?? TimeProvider.System;
        _startTimestamp = _timeProvider.GetTimestamp();
        Version = version;
        LocalParams = transportParameters ?? new TransportParameters();
        _localConnMaxData = LocalParams.InitialMaxDataValue;
        if (LocalParams.InitialMaxDataValue > 0)
            _connWindowTuner = new ReceiveWindowTuner(LocalParams.InitialMaxDataValue, MaxConnReceiveWindow);
        Scid = sourceConnectionId ?? new ConnectionId(RandomNumberGenerator.GetBytes(8));
        _cids = new ConnectionIdManager(Scid); // local handshake CID = sequence 0
        _addressValidated = !IsServer;         // the client regards the server address as validated
        // RFC 9002 §6.2.2.1/§A.6: for the client the peer counts as validated only after a Handshake
        // ACK or handshake completion — until then it must keep a PTO armed even with nothing in flight.
        _recovery.PeerCompletedAddressValidation = IsServer;
        Qlog = qlog;
        // One hook for both consumers: qlog only when it is configured, the metrics/EventSource
        // always — those cost nothing while nobody listens.
        _recovery.OnPacketLost = (space, packetNumber, trigger) =>
        {
            qlog?.PacketLost(QlogTimeMs, QlogPacketType(space), packetNumber, trigger);
            if (QuicMetrics.PacketsLost.Enabled)
                QuicMetrics.PacketsLost.Add(1, QuicMetrics.RoleTag(IsServer));
            QuicEventSource.Log.PacketLost(RoleName, space, (long)packetNumber, trigger);
        };
        _idle.Negotiate(LocalParams.MaxIdleTimeoutMs, peerMs: 0); // the peer value follows after the handshake
        _idle.Start(NowTicks);

        QuicMetrics.ActiveConnections.Add(1, QuicMetrics.RoleTag(IsServer));
        QuicEventSource.Log.ConnectionStarted(RoleName, Convert.ToHexString(Scid.Span));
    }

    /// <summary>
    /// Role as it appears in events and metric tags.
    /// </summary>
    private String RoleName => IsServer ? "server" : "client";

    /// <summary>
    /// Guards the paired <see cref="QuicMetrics.ActiveConnections"/> decrement: disposal is allowed
    /// to happen more than once, the gauge is not.
    /// </summary>
    private Boolean _connectionCounted = true;

    /// <summary>
    /// The clock this connection runs on (RFC 9002 timers, idle timeout, pacing). Exposed so the
    /// facades above can share the same time source.
    /// </summary>
    public TimeProvider TimeProvider => _timeProvider;

    /// <summary>
    /// Monotonic ticks elapsed since construction, from the injected <see cref="TimeProvider"/>.
    /// </summary>
    private long NowTicks => _timeProvider.GetElapsedTime(_startTimestamp).Ticks;

    /// <summary>
    /// <c>true</c> for the server role. Controls key direction and stream perspective.
    /// </summary>
    protected abstract bool IsServer { get; }

    public ConnectionId SourceConnectionId => Scid;
    public IReadOnlyDictionary<ulong, QuicStream> Streams => StreamMap;
    public TransportParameters? PeerTransportParameters => PeerParams;

    /// <summary>
    /// The exponent to decode ACK Delay fields the PEER sends (RFC 9000 §19.3/§18.2). Until its
    /// transport parameters arrive the default of 3 applies — which is also why nothing here may be
    /// carried over from a resumed connection: §7.4.1 forbids remembering this value for 0-RTT.
    /// </summary>
    protected ulong PeerAckDelayExponent => PeerParams?.AckDelayExponentValue ?? 3;

    /// <summary>
    /// The promise we made to the peer about how long we may sit on an acknowledgment
    /// (RFC 9000 §13.2.1). Exceeding it inflates the peer's RTT estimate.
    /// </summary>
    protected TimeSpan LocalMaxAckDelay => TimeSpan.FromMilliseconds(LocalParams.MaxAckDelayMs);

    /// <summary>
    /// Whether acknowledgments may be held back per RFC 9000 §13.2.2 ("A receiver SHOULD send an ACK
    /// frame after receiving at least two ack-eliciting packets") instead of going out for every
    /// packet. Default <c>false</c>.
    /// <para>
    /// Acknowledging everything at once is fully compliant — §13.2.2 is a SHOULD, and the cost is
    /// return-path traffic, not correctness. Delaying is switched off by default because it is not
    /// yet trusted here: enabling it stalls the WebSocket-over-HTTP/3 echo path, and that has not
    /// been explained. Everything else this option touches — the measured ack_delay, the
    /// ack_delay_exponent, the peer's max_ack_delay in the probe timeout, immediate acknowledgment
    /// on reordering and ECN-CE — is always on, because none of it depends on delaying anything.
    /// </para>
    /// </summary>
    public bool DelayedAcknowledgments { get; set; }

    /// <summary>
    /// How often an otherwise ACK-only packet gets a PING so the peer acknowledges it and our ACK
    /// state can be released (RFC 9000 §13.2.1/§13.2.4). Every packet would be a feedback loop.
    /// </summary>
    private const int AckElicitingAckInterval = 4;

    private int _consecutiveAckOnlyPackets;

    /// <summary>
    /// Builds the pending ACK of a space with the delay actually measured since the largest packet
    /// arrived (§13.2.5), encoded with OUR ack_delay_exponent — the peer decodes it with the value
    /// we advertised, not with its own.
    /// </summary>
    private AckFrame? BuildAckFor(int space)
        => Spaces[space].BuildAck(Spaces[space].EncodeAckDelay(NowTicks, LocalParams.AckDelayExponentValue));

    /// <summary>
    /// Current congestion window in bytes (RFC 9002 §7). Diagnostics.
    /// </summary>
    public long CongestionWindow => _recovery.Congestion.CongestionWindow;

    /// <summary>
    /// Currently unacknowledged, congestion-controlled bytes in flight. Diagnostics.
    /// </summary>
    public long BytesInFlight => _recovery.Congestion.BytesInFlight;

    /// <summary>
    /// Number of CE-marked packets received in the application space (RFC 9000 §13.4). Diagnostics/test.
    /// </summary>
    public ulong ApplicationReceivedCeCount => Spaces[(int)EncryptionLevel.Application].ReceivedCeCount;

    /// <summary>
    /// Number of packet numbers currently held in the application space for ACK generation
    /// (RFC 9000 §13.2.4). Diagnostics/test: must stay bounded over the lifetime of a connection,
    /// no matter how many packets have flowed.
    /// </summary>
    public int ApplicationTrackedReceivedCount => Spaces[(int)EncryptionLevel.Application].TrackedReceivedCount;

    /// <summary>
    /// Optional qlog for this connection; <c>null</c> = off (then no event is built at all).
    /// </summary>
    protected QlogWriter? Qlog { get; }

    /// <summary>
    /// Connection time in milliseconds — the qlog time base (<c>relative_to_epoch</c> on our
    /// monotonic clock).
    /// </summary>
    private double QlogTimeMs => NowTicks / (double)TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// qlog packet type of an encryption level (draft-ietf-quic-qlog-quic-events, PacketType).
    /// </summary>
    private static string QlogPacketType(int level) => level switch
    {
        (int)EncryptionLevel.Initial => "initial",
        (int)EncryptionLevel.Handshake => "handshake",
        _ => "1RTT",
    };

    /// <summary>
    /// Emits <c>quic:recovery_metrics_updated</c> with the current RTT/congestion values — what
    /// qvis draws as the congestion diagram.
    /// </summary>
    private void QlogRecoveryMetrics()
    {
        Qlog?.RecoveryMetricsUpdated(QlogTimeMs,
            _recovery.Rtt.SmoothedRtt.TotalMilliseconds,
            _recovery.Rtt.LatestRtt.TotalMilliseconds,
            _recovery.Rtt.MinRtt.TotalMilliseconds,
            _recovery.Rtt.RttVar.TotalMilliseconds,
            _recovery.PtoCount,
            _recovery.Congestion.CongestionWindow,
            _recovery.Congestion.BytesInFlight);

        // Same moment, different audience: this runs on every ACK, so both are gated.
        if (QuicMetrics.SmoothedRtt.Enabled)
            QuicMetrics.SmoothedRtt.Record(_recovery.Rtt.SmoothedRtt.TotalMilliseconds, QuicMetrics.RoleTag(IsServer));
        if (QuicMetrics.CongestionWindow.Enabled)
            QuicMetrics.CongestionWindow.Record(_recovery.Congestion.CongestionWindow, QuicMetrics.RoleTag(IsServer));
    }

    /// <summary>
    /// Test seam: how often a HANDSHAKE_DONE frame went onto the wire. RFC 9000 §13.3 requires
    /// retransmission until acknowledged ⇒ after a loss this must be &gt; 1.
    /// </summary>
    internal int HandshakeDoneSentCountForTest { get; private set; }

    /// <summary>
    /// Test hook: queues an application-space control frame, so a peer's reaction to a frame we
    /// would never send ourselves can be observed.
    /// </summary>
    internal void SendApplicationFrameForTest(Frame frame) => _pendingControlFrames.Add(frame);

    /// <summary>
    /// <c>true</c> once the connection was closed silently due to the idle timeout (RFC 9000 §10.1).
    /// </summary>
    public bool IsIdleTimedOut => _idleTimedOut;

    /// <summary>
    /// Keep-alive interval (RFC 9000 §10.1.2): when set, the connection sends a PING after that much
    /// inactivity to reset the idle timeout on both sides. Should be considerably smaller than the
    /// negotiated idle timeout. <c>null</c> = disabled.
    /// </summary>
    public TimeSpan? KeepAliveInterval
    {
        get => _keepAliveInterval;
        set => _keepAliveInterval = value;
    }

    // ---- Connection-ID rotation (RFC 9000 §5.1) --------------------------------------------

    /// <summary>
    /// Number of active connection IDs issued by us (which the peer may use as DCID).
    /// </summary>
    public int LocalConnectionIdCount => _cids.LocalCount;

    /// <summary>
    /// Number of known connection IDs issued by the peer (which we can use as DCID).
    /// </summary>
    public int RemoteConnectionIdCount => _cids.RemoteCount;

    /// <summary>
    /// Destination connection ID currently used for sending.
    /// </summary>
    public ConnectionId DestinationConnectionId => Dcid;

    /// <summary>
    /// Gives the peer an additional connection ID (8 bytes) incl. stateless-reset token via
    /// NEW_CONNECTION_ID (RFC 9000 §19.15), provided its <c>active_connection_id_limit</c> permits it.
    /// Requires installed 1-RTT keys. Returns the new ID or <c>null</c> when nothing was issued.
    /// </summary>
    public ConnectionId? IssueConnectionId()
    {
        if (!_appKeysInstalled)
            return null;
        var newCid = new ConnectionId(RandomNumberGenerator.GetBytes(8));
        // Derive the token from the CID (when a generator is set), so it stays recomputable for a
        // stateless reset after state loss; otherwise random.
        byte[] token = StatelessResetTokens?.ComputeToken(newCid.Span) ?? RandomNumberGenerator.GetBytes(16);
        ulong limit = PeerParams?.ActiveConnectionIdLimitValue ?? 2;
        if (_cids.Issue(newCid, token, limit) is not { } frame)
            return null;
        _pendingControlFrames.Add(frame);
        return newCid;
    }

    /// <summary>
    /// Switches the destination connection ID to one previously offered by the peer (RFC 9000 §5.1)
    /// and retires the previous one via RETIRE_CONNECTION_ID. Returns <c>true</c> when a further ID was available.
    /// </summary>
    public bool RotateDestinationConnectionId()
    {
        if (_cids.Rotate() is not { } rotation)
            return false;
        Dcid = rotation.NewDcid;
        _pendingControlFrames.Add(rotation.Retire);
        return true;
    }

    /// <summary>
    /// <c>true</c> when <paramref name="cid"/> is one of our active local connection IDs (for CID-based demuxing).
    /// </summary>
    public bool OwnsConnectionId(ConnectionId cid) => _cids.IsLocalConnectionId(cid);

    // ---- Path validation / connection migration (RFC 9000 §8.2, §9) -------------------------

    /// <summary>
    /// A path validation is in progress (PATH_CHALLENGE sent, PATH_RESPONSE outstanding).
    /// </summary>
    public bool PathValidationPending => _pathChallengePending is not null;

    /// <summary>
    /// <c>true</c> once the last path validation was confirmed with a matching PATH_RESPONSE.
    /// </summary>
    public bool PathValidated { get; private set; }

    /// <summary>
    /// Starts a path validation (RFC 9000 §8.2): sends a PATH_CHALLENGE with 8 random bytes and
    /// expects a matching PATH_RESPONSE. The basis of every connection migration (RFC 9000 §9) – the
    /// new path only counts as reachable after successful validation.
    /// </summary>
    public void InitiatePathValidation()
    {
        ulong data = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        _pathChallengePending = data;
        PathValidated = false;
        _pendingControlFrames.Add(new PathChallengeFrame(data));
        _pathValidationDeadlineTicks = NowTicks + (3 * _recovery.Rtt.GetProbeTimeout(_recovery.MaxAckDelay)).Ticks;
    }

    /// <summary>
    /// <c>true</c> while we are in the closing state after our own CONNECTION_CLOSE (RFC 9000 §10.2.1).
    /// </summary>
    public bool IsClosing => _state == ConnectionState.Closing;

    /// <summary>
    /// <c>true</c> while we are in the draining state after a received CONNECTION_CLOSE (§10.2.2).
    /// </summary>
    public bool IsDraining => _state == ConnectionState.Draining;

    /// <summary>
    /// <c>true</c> once the closing/draining period (3·PTO) has expired and the connection is finally shut.
    /// </summary>
    public bool IsClosed => _state == ConnectionState.Closed;

    /// <summary>
    /// The CONNECTION_CLOSE received from the peer (error code + reason), if any.
    /// </summary>
    public ConnectionCloseFrame? PeerCloseFrame { get; private set; }

    /// <summary>
    /// <c>true</c> when a peer stateless reset was detected (RFC 9000 §10.3). Leads to draining.
    /// </summary>
    public bool StatelessResetReceived { get; private set; }

    /// <summary>
    /// Closes the connection immediately (RFC 9000 §10.2): sends a CONNECTION_CLOSE (transport error
    /// <paramref name="error"/>, default NO_ERROR = orderly teardown) and enters the closing state.
    /// After that only CONNECTION_CLOSE packets are sent until the closed state follows after 3·PTO.
    /// </summary>
    public void Close(TransportError error = TransportError.NoError, string reason = "")
        => EnterClosing(ConnectionCloseFrame.Transport(error, reason));

    /// <summary>
    /// Closes the connection with an APPLICATION error code (CONNECTION_CLOSE type 0x1d,
    /// RFC 9000 §19.19) — e.g. an HTTP/3 error code from RFC 9114 §8.1.
    /// </summary>
    public void CloseApplication(ulong errorCode, string reason = "")
        => EnterClosing(new ConnectionCloseFrame(errorCode, IsApplicationError: true, 0, reason));

    private void EnterClosing(ConnectionCloseFrame closeFrame)
    {
        Qlog?.ConnectionClosed(QlogTimeMs, "local", closeFrame.ErrorCode, closeFrame.ReasonPhrase);

        if (_state != ConnectionState.Active)
            return;

        // After the state check, so a repeated Close() does not report a second closure.
        QuicEventSource.Log.ConnectionClosed(RoleName, Convert.ToHexString(Scid.Span),
                                             "local", (long)closeFrame.ErrorCode, closeFrame.ReasonPhrase);

        LocalCloseErrorCode = closeFrame.ErrorCode;
        _closeFrame = closeFrame;
        _state = ConnectionState.Closing;
        _closePacketPending = true;
        _closeDeadlineTicks = NowTicks + CloseTimeout().Ticks;
    }

    /// <summary>
    /// Closing/draining duration per RFC 9000 §10.2: three times the current probe timeout.
    /// </summary>
    private TimeSpan CloseTimeout() => 3 * _recovery.Rtt.GetProbeTimeout(_recovery.MaxAckDelay);

    /// <summary>
    /// After the closing/draining period expires, transition to the final closed state.
    /// </summary>
    private void MaybeTransitionToClosed()
    {
        if (_state is ConnectionState.Closing or ConnectionState.Draining &&
            _closeDeadlineTicks >= 0 && NowTicks >= _closeDeadlineTicks)
            _state = ConnectionState.Closed;
    }

    /// <summary>
    /// Checks the idle timeout (RFC 9000 §10.1). On expiry the connection is closed silently
    /// (state discarded): after that no more datagrams are produced or processed. Call
    /// periodically. Returns <c>true</c> once the timeout has occurred.
    /// </summary>
    public bool CheckIdleTimeout()
    {
        MaybeTransitionToClosed(); // finish closing/draining after 3·PTO (RFC 9000 §10.2)

        // A path validation without an answer lapses (RFC 9000 §8.2): PathValidated then stays false.
        if (_pathChallengePending is not null && _pathValidationDeadlineTicks >= 0 &&
            NowTicks >= _pathValidationDeadlineTicks)
        {
            _pathChallengePending = null;
            _pathValidationDeadlineTicks = -1;
        }
        if (!_idleTimedOut &&
            _idle.IsExpired(NowTicks, _recovery.Rtt.GetProbeTimeout(_recovery.MaxAckDelay)))
            _idleTimedOut = true;
        return _idleTimedOut;
    }

    // ---- Role hooks (default: no behaviour) ------------------------------------------------

    /// <summary>
    /// Called for every received long-header packet before decryption.
    /// </summary>
    protected virtual void OnLongHeaderPacket(LongPacketType type, LongHeaderPrefix prefix) { }

    /// <summary>
    /// Inserts role-specific control frames before the remaining 1-RTT frames (server: HANDSHAKE_DONE).
    /// </summary>
    protected virtual void AddApplicationControlFrames(List<Frame> frames) { }

    /// <summary>
    /// A HANDSHAKE_DONE was received (only the client reacts to it).
    /// </summary>
    protected virtual void OnHandshakeDoneReceived() { }

    /// <summary>
    /// One of our 1-RTT packets was acknowledged. The client MAY thereupon regard the handshake as
    /// confirmed (RFC 9001 §4.1.2), even without HANDSHAKE_DONE. The server does not use this (it
    /// confirms at completion).
    /// </summary>
    protected virtual void OnOneRttPacketAcknowledged() { }

    /// <summary>
    /// A 1-RTT frame was processed (client: collect for inspection).
    /// </summary>
    protected virtual void OnApplicationFrameHandled(Frame frame) { }

    /// <summary>
    /// A NEW_TOKEN frame arrived (RFC 9000 §8.1.3) — only the client acts on this.
    /// </summary>
    protected virtual void OnNewTokenReceived(ReadOnlyMemory<byte> token) { }

    /// <summary>
    /// A stream was (possibly newly) supplied (server: track new request streams).
    /// </summary>
    protected virtual void OnStreamOpened(StreamId id, bool isNew) { }

    /// <summary>
    /// A version-negotiation packet was received (only the client reacts to it).
    /// </summary>
    protected virtual void HandleVersionNegotiationPacket(ReadOnlySpan<byte> datagram) { }

    /// <summary>
    /// A Retry packet was received (only the client reacts to it).
    /// </summary>
    protected virtual void HandleRetryPacket(ReadOnlySpan<byte> datagram) { }

    /// <summary>
    /// A long-header packet with an unsupported version arrived (only the server reacts – with VN).
    /// </summary>
    protected virtual void HandleUnsupportedVersion(ReadOnlySpan<byte> datagram) { }

    /// <summary>
    /// Enqueues an already-finished raw datagram (version negotiation / Retry) for sending.
    /// </summary>
    protected void EnqueueDatagram(byte[] datagram) => _pendingDatagrams.Enqueue(datagram);

    /// <summary>
    /// Marks the peer address as validated (RFC 9000 §8.1) – e.g. after a valid Retry token came
    /// back. Lifts the anti-amplification limit.
    /// </summary>
    protected void MarkAddressValidated() => _addressValidated = true;

    /// <summary>
    /// <c>true</c> once the peer address is validated (no more anti-amplification limit). Diagnostics.
    /// </summary>
    public bool AddressValidated => _addressValidated;

    /// <summary>
    /// <c>true</c> once at least one packet was processed successfully (for the VN rules, RFC 9000 §6.2).
    /// </summary>
    protected bool AnyPacketProcessed => _anyPacketProcessed;

    /// <summary>
    /// Reacts to a Retry on the client side: derive new Initial keys from <paramref name="newDcid"/>
    /// (RFC 9001 §5.2), remember the Retry token and reset the Initial CRYPTO offset so the
    /// ClientHello is sent again from offset 0.
    /// </summary>
    protected void ApplyRetry(ConnectionId newDcid, byte[] token)
    {
        Dcid = newDcid;
        InitialToken = token;
        InstallInitialKeys(newDcid);
        _sendCryptoOffset[(int)EncryptionLevel.Initial] = 0;
    }

    // ---- Opening streams -------------------------------------------------------------------

    protected QuicStream OpenLocalStream(bool bidirectional)
    {
        MaybeDecodePeerParameters();
        ulong index = bidirectional ? _nextLocalBidiIndex++ : _nextLocalUniIndex++;
        return GetOrCreateStream(StreamId.Create(clientInitiated: !IsServer, bidirectional, index));
    }

    private QuicStream GetOrCreateStream(StreamId id)
    {
        if (StreamMap.TryGetValue(id.Value, out QuicStream? existing))
            return existing;
        ulong receiveWindow = ReceiveWindowFor(id);
        var stream = new QuicStream(id, PeerSendLimitFor(id), receiveWindow);
        if (receiveWindow > 0)
            stream.Receive.WindowTuner = new ReceiveWindowTuner(receiveWindow, MaxStreamReceiveWindow); // auto-tuning (phase 9)
        StreamMap[id.Value] = stream;
        if (QuicMetrics.StreamsOpened.Enabled)
            QuicMetrics.StreamsOpened.Add(1, QuicMetrics.RoleTag(IsServer));
        return stream;
    }

    // "Locally initiated" from this role's point of view (client: client-initiated, server: server-initiated).
    private bool LocallyInitiated(StreamId id) => IsServer ? id.IsServerInitiated : id.IsClientInitiated;

    private ulong ReceiveWindowFor(StreamId id)
        => id.IsUnidirectional
            ? LocalParams.InitialMaxStreamDataUniValue
            : LocallyInitiated(id)
                ? LocalParams.InitialMaxStreamDataBidiLocalValue
                : LocalParams.InitialMaxStreamDataBidiRemoteValue;

    private ulong PeerSendLimitFor(StreamId id)
    {
        if (PeerParams is null)
            return 0;
        return id.IsUnidirectional
            ? PeerParams.InitialMaxStreamDataUniValue
            : LocallyInitiated(id)
                ? PeerParams.InitialMaxStreamDataBidiRemoteValue
                : PeerParams.InitialMaxStreamDataBidiLocalValue;
    }

    // ---- Sending ---------------------------------------------------------------------------

    /// <summary>
    /// Produces the datagrams currently to send (CRYPTO, ACKs, stream data, flow control).
    /// </summary>
    public IReadOnlyList<byte[]> GetDatagramsToSend()
    {
        IReadOnlyList<byte[]> datagrams = BuildDatagramsToSend();

        // Counted here rather than at any of the many return points below, so nothing can be added
        // later that quietly escapes the accounting.
        if (QuicMetrics.BytesSent.Enabled && datagrams.Count > 0)
        {
            long total = 0;
            for (int i = 0; i < datagrams.Count; i++)
                total += datagrams[i].Length;
            QuicMetrics.BytesSent.Add(total, QuicMetrics.RoleTag(IsServer));
        }
        return datagrams;
    }

    private IReadOnlyList<byte[]> BuildDatagramsToSend()
    {
        var datagrams = new List<byte[]>();
        MaybeTransitionToClosed();
        if (_idleTimedOut || _state is ConnectionState.Draining or ConnectionState.Closed)
            return datagrams; // send nothing while draining/closed (RFC 9000 §10.2.2)

        // In the closing state, send exclusively (repeatedly) the CONNECTION_CLOSE (RFC 9000 §10.2.1).
        if (_state == ConnectionState.Closing)
        {
            if (_closePacketPending && BuildClosePacket() is { } closePacket)
            {
                _closePacketPending = false;
                datagrams.Add(closePacket);
            }
            return datagrams;
        }

        // Send raw datagrams (version negotiation / Retry) up front – independent of the TLS state.
        while (_pendingDatagrams.Count > 0)
            datagrams.Add(_pendingDatagrams.Dequeue());
        if (TlsHandshake is null)
            return datagrams;

        // Keep-alive (RFC 9000 §10.1.2): after enough inactivity, schedule a PING that resets the
        // idle timeout on both sides (the PING is ack-eliciting and is acknowledged by the peer).
        if (_keepAliveInterval is { } keepAlive && _appKeysInstalled &&
            _idle.ShouldSendKeepAlive(NowTicks, keepAlive))
            _pingPending = true;

        // Refill the pacing budget for this call based on the elapsed time and the current rate.
        _pacer.Refill(NowTicks, _recovery.Congestion.CongestionWindow, _recovery.Rtt.SmoothedRtt);

        // Take outgoing CRYPTO into the persistent buffers (kept in case the
        // anti-amplification budget defers sending).
        while (TlsHandshake.TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data))
            _outgoingCrypto[(int)level].AddRange(data);

        // Anti-amplification budget (RFC 9000 §8.1): unlimited once the address is validated.
        long amplificationBudget = _addressValidated ? long.MaxValue : Math.Max(0, 3 * _amplificationReceived - _amplificationSent);

        MaybeInstallZeroRttKeys();      // client: the early secret is ready right after Start
        MaybeHandleZeroRttRejection();  // catch up rejected 0-RTT immediately over 1-RTT

        AppendLevelPackets(EncryptionLevel.Initial, datagrams, ref amplificationBudget);
        AppendLevelPackets(EncryptionLevel.Handshake, datagrams, ref amplificationBudget);
        BuildZeroRttPackets(datagrams);     // client: early-queued application data as 0-RTT (before 1-RTT keys)
        BuildApplicationPackets(datagrams); // 1-RTT only after the handshake ⇒ the address is validated by then
        MaybeSendPmtuProbe(datagrams);      // last: an oversized probe must not displace real data

        // After the just-built flight, discard the keys no longer needed (RFC 9001 §4.9):
        // Initial once the client sent its Handshake packet; Handshake once the handshake is confirmed.
        MaybeDiscardInitialKeys();
        MaybeDiscardHandshakeKeys();
        MaybeDiscardServerZeroRttKeys();

        return datagrams;
    }

    /// <summary>
    /// Payload budget per packet. Chosen conservatively so that header (long header + possible token),
    /// packet number and the AEAD tag together stay below the ~1200-byte MTU floor (RFC 9000 §14.1).
    /// Everything that does not fit — large ClientHellos (PQ hybrid), certificate chains, but equally
    /// a long run of control frames — is spread across several packets.
    /// </summary>
    /// <summary>
    /// Room reserved above the payload for the packet header, the packet number and the AEAD tag.
    /// The original constants encoded exactly this: 1200 − 1000. Keeping it explicit is what lets
    /// the payload budget follow a discovered MTU instead of being frozen at the floor.
    /// </summary>
    private const int PacketOverheadHeadroom = 200;

    private int MaxPayloadPerPacket => CurrentMaxDatagramSize - PacketOverheadHeadroom;

    /// <summary>
    /// Size no datagram we emit exceeds. RFC 9000 §14.1 guarantees exactly 1200 bytes on every path;
    /// anything above it may be dropped or fragmented without notice, which is why loss recovery and
    /// pacing also reckon with this value. <see cref="MaxPayloadPerPacket"/> is chosen so that
    /// payload + header + AEAD tag stay below it in every packet form.
    /// </summary>
    public const int MaxDatagramSize = PathMtuDiscovery.BasePlpmtu;

    /// <summary>
    /// Largest datagram this connection currently emits. Starts at the guaranteed floor and rises
    /// only once DPLPMTUD has proof the path carries more (RFC 9000 §14.3) — an acknowledged probe.
    /// </summary>
    public int CurrentMaxDatagramSize => _pmtu.MaxDatagramSize;

    /// <summary>
    /// Path MTU discovery for this connection (RFC 9000 §14.3).
    /// </summary>
    public PathMtuDiscovery PathMtu => _pmtu;

    private readonly PathMtuDiscovery _pmtu;

    /// <summary>
    /// CRYPTO payload per Initial/Handshake packet. Deliberately fixed at the floor rather than
    /// following the discovered MTU: RFC 9000 §14.3.1 lets DPLPMTUD start only once the handshake is
    /// complete, so during the handshake nothing larger than the guaranteed 1200 bytes is known to
    /// get through.
    /// </summary>
    private const int MaxCryptoDataPerPacket = PathMtuDiscovery.BasePlpmtu - PacketOverheadHeadroom;

    /// <summary>
    /// Emits the packets of an encryption level (RFC 9000 §12.2/§13). Control frames (retransmits,
    /// ACK) and the outgoing CRYPTO are distributed across as many packets as needed so that NO
    /// datagram exceeds the packet budget. Anti-amplification (RFC 9000 §8.1) is checked per packet;
    /// what is not sent stays buffered — CRYPTO keeps its offset, unsent control frames go back into
    /// the retransmit queue, a built but unsent ACK re-arms.
    /// </summary>
    private void AppendLevelPackets(EncryptionLevel level, List<byte[]> datagrams, ref long amplificationBudget)
    {
        int i = (int)level;
        if (WriteKeys[i] is not { } keys)
            return;

        // Control frames of this pass, collected once and then spread across packets.
        var control = new List<Frame>();
        DrainRetransmitQueue(i, control);
        // Initial and Handshake: §13.2.1 "An endpoint MUST acknowledge all ack-eliciting Initial and
        // Handshake packets immediately" — no delay policy applies here.
        if (Spaces[i].IsAckDue(NowTicks, LocalMaxAckDelay, immediateSpace: true) && BuildAckFor(i) is { } ack)
            control.Add(ack);
        int controlCursor = 0;

        var writer = new BufferWriter();
        try
        {
            while (true)
            {
                writer.Truncate(0);
                var frames = new List<Frame>();

                // 1) As many pending control frames as fit into the packet.
                int cursorBefore = controlCursor;
                controlCursor = FrameParser.WriteUpTo(ref writer, control, controlCursor, MaxPayloadPerPacket);
                for (int k = cursorBefore; k < controlCursor; k++)
                    frames.Add(control[k]);

                // 2) Fill the rest of the packet with CRYPTO.
                int cryptoBudget = MaxCryptoDataPerPacket - writer.Length;
                int cryptoChunk = cryptoBudget > 0 ? Math.Min(_outgoingCrypto[i].Count, cryptoBudget) : 0;
                if (cryptoChunk > 0)
                {
                    var crypto = new CryptoFrame(_sendCryptoOffset[i], _outgoingCrypto[i].GetRange(0, cryptoChunk).ToArray());
                    crypto.Write(ref writer);
                    frames.Add(crypto);
                }

                if (frames.Count == 0)
                    return;

                ulong pn = Spaces[i].NextPacketNumber();
                int pnLength = PacketNumber.EncodeLength(pn, Spaces[i].LargestAckedByPeer);

                byte[] packet = level == EncryptionLevel.Initial
                    ? InitialPacketFactory.BuildPadded(keys, Version, Dcid, Scid, InitialToken, pn, pnLength, writer.WrittenSpan)
                    : LongHeader.Build(keys, LongPacketType.Handshake, Version, Dcid, Scid, default, pn, pnLength, writer.WrittenSpan);

                // Anti-amplification (RFC 9000 §8.1): if the packet blows the budget, defer it – the CRYPTO
                // stays buffered, the offset unchanged (later receipt raises the limit).
                if (packet.Length > amplificationBudget)
                {
                    RequeueUnsentControlFrames(i, control, cursorBefore);
                    return;
                }

                amplificationBudget -= packet.Length;
                if (!_addressValidated)
                    _amplificationSent += packet.Length;
                if (cryptoChunk > 0)
                {
                    _sendCryptoOffset[i] += (ulong)cryptoChunk;
                    _outgoingCrypto[i].RemoveRange(0, cryptoChunk);
                }
                RecordSent(i, pn, packet.Length, frames);
                datagrams.Add(packet);
                if (level == EncryptionLevel.Handshake)
                    _handshakePacketSent = true; // client: later triggers discarding the Initial keys (RFC 9001 §4.9.1)

                if (controlCursor >= control.Count && _outgoingCrypto[i].Count == 0)
                    return; // neither control frames nor CRYPTO left to distribute
            }
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Puts control frames that were built but not sent back into circulation: reliable frames go
    /// back into the retransmit queue, an unsent ACK re-arms so the next send builds a fresh one.
    /// Without this, deferring a packet (anti-amplification) would silently drop them.
    /// </summary>
    private void RequeueUnsentControlFrames(int level, List<Frame> control, int from)
    {
        for (int k = from; k < control.Count; k++)
        {
            if (control[k] is AckFrame)
                Spaces[level].MarkAckPending();
            else
                _retransmitQueue[level].Add(control[k]);
        }
    }

    /// <summary>
    /// Payload budget for stream data per 1-RTT packet, so a datagram stays within the current MTU.
    /// Unlike the CRYPTO budget this one follows what DPLPMTUD has proven, which is where the extra
    /// throughput actually comes from.
    /// </summary>
    private int MaxStreamDataPerPacket => MaxPayloadPerPacket;

    /// <summary>
    /// Emits the 1-RTT datagrams: the control frames of this pass (retransmits, HANDSHAKE_DONE,
    /// CID management, PING, ACK, flow control, stream cancellations, post-handshake CRYPTO) are
    /// collected ONCE and then spread across as many packets as needed — an arbitrary number of them
    /// must never end up in a single over-MTU datagram (RFC 9000 §14). Each packet is then filled up
    /// with a DATAGRAM frame or a stream chunk, as long as cwnd (RFC 9002 §7) and pacing (§7.7)
    /// permit. Control frames and PTO probes themselves are exempt from that budget so feedback and
    /// loss probes are never blocked.
    /// </summary>
    private void BuildApplicationPackets(List<byte[]> datagrams)
    {
        int i = (int)EncryptionLevel.Application;
        if (WriteKeys[i] is not { } keys)
            return;

        MaybeDecodePeerParameters();

        // Shared budget for NEW stream data from congestion window and pacing.
        long sendBudget = Math.Min(_recovery.Congestion.Available, _pacer.AvailableBytes);

        var control = new List<Frame>();
        DrainRetransmitQueue(i, control);
        AddApplicationControlFrames(control); // server: HANDSHAKE_DONE
        if (_pendingControlFrames.Count > 0)
        {
            control.AddRange(_pendingControlFrames); // NEW_/RETIRE_CONNECTION_ID (RFC 9000 §5.1)
            _pendingControlFrames.Clear();
        }
        if (_pingPending)
        {
            control.Add(PingFrame.Instance); // keep-alive (RFC 9000 §10.1.2)
            _pingPending = false;
        }
        // Application space: acknowledge after two ack-eliciting packets, on reordering or an ECN-CE
        // mark, or once max_ack_delay has elapsed (§13.2.1/§13.2.2). An ACK also rides along free of
        // charge whenever this pass is sending something anyway — §13.2.1: "An endpoint SHOULD send
        // an ACK frame with other frames when there are new ack-eliciting packets to acknowledge."
        if ((Spaces[i].IsAckDue(NowTicks, LocalMaxAckDelay, immediateSpace: !DelayedAcknowledgments) ||
             (Spaces[i].AckPending && control.Count > 0)) &&
            BuildAckFor(i) is { } ack)
            control.Add(ack);
        CollectFlowControlFrames(control);
        CollectStreamCancellationFrames(control); // RESET_STREAM / STOP_SENDING (RFC 9000 §2.4)

        // An endpoint that only ever sends ACK frames never gets acknowledged itself — its packets
        // are not ack-eliciting — so its own ACK state can never be released (§13.2.4). §13.2.1
        // names the way out: "An endpoint that is only sending non-ack-eliciting packets might
        // choose to occasionally add an ack-eliciting frame to those packets to ensure that it
        // receives an acknowledgment … In that case, an endpoint MUST NOT send an ack-eliciting
        // frame in all packets", hence every fourth rather than every one.
        if (DelayedAcknowledgments && control.Count > 0 && control.TrueForAll(f => f is AckFrame) &&
            !StreamMap.Values.Any(s => s.Send.HasPending) && _outgoingDatagrams.Count == 0)
        {
            if (++_consecutiveAckOnlyPackets % AckElicitingAckInterval == 0)
                control.Add(PingFrame.Instance);
        }
        else
            _consecutiveAckOnlyPackets = 0;

        // Post-handshake CRYPTO at application level (e.g. NewSessionTicket, RFC 8446 §4.6.1).
        int appCryptoChunk = Math.Min(_outgoingCrypto[i].Count, MaxCryptoDataPerPacket);
        if (appCryptoChunk > 0)
        {
            control.Add(new CryptoFrame(_sendCryptoOffset[i], _outgoingCrypto[i].GetRange(0, appCryptoChunk).ToArray()));
            _sendCryptoOffset[i] += (ulong)appCryptoChunk;
            _outgoingCrypto[i].RemoveRange(0, appCryptoChunk);
        }
        int controlCursor = 0;

        var writer = new BufferWriter();
        try
        {
        while (true)
        {
            writer.Truncate(0);
            var frames = new List<Frame>();

            // As many pending control frames as fit into this packet – the rest follows in the next one.
            int cursorBefore = controlCursor;
            controlCursor = FrameParser.WriteUpTo(ref writer, control, controlCursor, MaxPayloadPerPacket);
            for (int k = cursorBefore; k < controlCursor; k++)
                frames.Add(control[k]);
            int payloadLeft = MaxPayloadPerPacket - writer.Length;

            // DATAGRAM frames (RFC 9221 §5): as early as possible, one frame per packet (unfragmentable,
            // MTU!), congestion-controlled — if the budget does not suffice, they stay in the queue (§5.4).
            bool sentDatagram = false;
            if (sendBudget > 0 && _outgoingDatagrams.Count > 0 &&
                _outgoingDatagrams.Peek().Length + DatagramFrameOverhead <= payloadLeft)
            {
                byte[] datagramPayload = _outgoingDatagrams.Dequeue();
                frames.Add(new DatagramFrame(datagramPayload));
                sendBudget -= datagramPayload.Length + DatagramFrameOverhead;
                sentDatagram = true;
            }

            // At most one stream chunk per packet, limited by flow control, send budget and what is
            // left of the packet after the control frames.
            if (!sentDatagram && sendBudget > 0 && payloadLeft > StreamFrameOverhead)
                AppendOneStreamChunk(frames, ref sendBudget, payloadLeft - StreamFrameOverhead);

            if (frames.Count == 0)
                break;

            // Everything after the control frames still has to go into the writer.
            for (int k = controlCursor - cursorBefore; k < frames.Count; k++)
                frames[k].Write(ref writer);

            ulong pn = Spaces[i].NextPacketNumber();
            // First GENUINE 1-RTT packet – its acknowledgment may confirm the handshake (RFC 9001 §4.1.2).
            // IMPORTANT: set only here, NEVER in BuildZeroRttPackets. 0-RTT shares the application PN
            // space with 1-RTT, but §4.1.2 expressly requires the acknowledgment of a "1-RTT packet".
            // If a 0-RTT packet occupied this PN, its ACK would confirm the handshake too early (with
            // accepted 0-RTT) or a stray 0-RTT ACK would confirm it falsely (with 0-RTT rejection).
            // Since 0-RTT PNs are always smaller than that of the first 1-RTT packet, LargestAck ≥ this
            // PN provably implies acknowledgment of a genuine 1-RTT packet – the Handshake key discard
            // after 0-RTT rejection thus stays correct.
            _firstOneRttPacketNumber ??= pn;
            int pnLength = PacketNumber.EncodeLength(pn, Spaces[i].LargestAckedByPeer);
            byte[] packet = ShortHeader.Build(keys, Dcid, pn, pnLength, writer.WrittenSpan, keyPhase: _sendKeyPhase);
            RecordSent(i, pn, packet.Length, frames);
            datagrams.Add(packet);

            // Control frames still pending always keep the loop going — they must get out.
            if (controlCursor < control.Count)
                continue;

            // Otherwise continue only while budget remains AND stream data or datagrams are pending.
            if (sendBudget <= 0 ||
                (!StreamMap.Values.Any(s => s.Send.HasPending) && _outgoingDatagrams.Count == 0))
                break;
        }
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Emits one PMTU probe when the discovery wants one (RFC 9000 §14.3/§14.4). The probe is an
    /// ordinary ack-eliciting 1-RTT packet carrying nothing but PING and PADDING — §14.4 recommends
    /// exactly that, because a packet larger than the current maximum is the one most likely to be
    /// dropped and there is no point risking real data on it. An acknowledgment proves the size; a
    /// loss is attributed to the path, not to congestion.
    /// </summary>
    private void MaybeSendPmtuProbe(List<byte[]> datagrams)
    {
        int i = (int)EncryptionLevel.Application;
        if (WriteKeys[i] is not { } keys)
            return;

        // §14.3.1: the search may start once the handshake is complete — before that the path is not
        // even validated. The peer's max_udp_payload_size is only known from its transport
        // parameters, and §14 makes it an additional limit on what is worth discovering.
        if (HandshakeIsConfirmed && PeerParams is { } peer)
            _pmtu.Start(peer.MaxUdpPayloadSizeValue);

        int probeSize = _pmtu.NextProbeSize();
        if (probeSize <= 0)
            return;

        ulong pn = Spaces[i].NextPacketNumber();
        int pnLength = PacketNumber.EncodeLength(pn, Spaces[i].LargestAckedByPeer);

        // Short header: type byte + DCID + packet number, plus the AEAD tag the protection appends.
        int overhead = 1 + Dcid.Length + pnLength + AeadTagLength;
        int payloadLength = probeSize - overhead;
        if (payloadLength < 1)
            return; // cannot build a probe this small — nothing to discover

        // PING makes it ack-eliciting; the rest is PADDING, which is a run of zero bytes (§19.1).
        byte[] payload = new byte[payloadLength];
        payload[0] = (byte)FrameType.Ping;

        byte[] packet = ShortHeader.Build(keys, Dcid, pn, pnLength, payload, keyPhase: _sendKeyPhase);
        RecordSent(i, pn, packet.Length, [PingFrame.Instance], isPmtuProbe: true);
        _pmtu.OnProbeSent();
        datagrams.Add(packet);
    }

    /// <summary>
    /// Length of the AEAD authentication tag every protected packet carries (RFC 9001 §5.3).
    /// </summary>
    private const int AeadTagLength = 16;

    /// <summary>
    /// Worst-case frame overhead beyond the pure payload: type + stream/offset/length varints
    /// (RFC 9000 §19.8) resp. type + length (§19.7 DATAGRAM). Used when sizing a packet so the frame
    /// header cannot push the datagram beyond the budget.
    /// </summary>
    private const int StreamFrameOverhead = 25;
    private const int DatagramFrameOverhead = 9;

    private readonly Queue<byte[]> _outgoingDatagrams = new(); // waiting DATAGRAM payloads (RFC 9221)
    private List<byte[]> _receivedDatagrams = [];

    /// <summary>
    /// The max_datagram_frame_size announced by the peer (RFC 9221 §3); 0 = the peer accepts no
    /// DATAGRAM frames.
    /// </summary>
    public ulong PeerMaxDatagramFrameSize => PeerParams?.MaxDatagramFrameSizeValue ?? 0;

    /// <summary>
    /// The connection's TLS keying-material exporter (RFC 8446 §7.5): both endpoints obtain identical
    /// keying material for the same label and context — e.g. for channel binding or the WebTransport
    /// exporter (draft-webtrans-http3 §4.7). Available once the 1-RTT secrets are in place; before
    /// that, <see cref="InvalidOperationException"/>.
    /// </summary>
    public byte[] ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length)
        => TlsHandshake is { } tls
            ? tls.ExportKeyingMaterial(label, context, length)
            : throw new InvalidOperationException("No TLS handshake present.");

    /// <summary>
    /// Enqueues a QUIC DATAGRAM (RFC 9221) for sending — unreliable: NO retransmission on loss;
    /// under congestion pressure it is delayed (§5.4). <c>false</c> when the peer announced no
    /// DATAGRAM frames (§3 MUST NOT) or the frame exceeds its limit/the MTU.
    /// </summary>
    public bool TrySendDatagram(ReadOnlySpan<byte> payload)
    {
        MaybeDecodePeerParameters();
        ulong frameSize = 1UL + (ulong)VarInt.GetLength((ulong)payload.Length) + (ulong)payload.Length;
        if (PeerMaxDatagramFrameSize == 0 || frameSize > PeerMaxDatagramFrameSize)
            return false; // §3: without (or above) the peer announcement, MUST NOT send
        if (payload.Length > MaxStreamDataPerPacket)
            return false; // §5: DATAGRAM frames are unfragmentable — MTU cap like stream chunks
        _outgoingDatagrams.Enqueue(payload.ToArray());
        return true;
    }

    /// <summary>
    /// Fetches all QUIC DATAGRAM payloads received so far (RFC 9221 §5: immediate delivery).
    /// </summary>
    public IReadOnlyList<byte[]> TakeReceivedDatagrams()
    {
        if (_receivedDatagrams.Count == 0)
            return [];
        List<byte[]> taken = _receivedDatagrams;
        _receivedDatagrams = [];
        return taken;
    }

    private ulong _incrementalCursor = ulong.MaxValue; // last-served incremental stream (round-robin)

    /// <summary>
    /// Appends a single stream data chunk (one frame), provided flow control/budget permit.
    /// The stream choice follows RFC 9218 §10: ascending urgency; at equal urgency,
    /// NON-incremental streams are served one after another in ascending stream ID (request order),
    /// incremental ones share the bandwidth via round-robin.
    /// </summary>
    /// <param name="payloadLeft">
    /// Bytes still free in the current packet after the control frames — the chunk must fit in
    /// there too, not just under <see cref="MaxStreamDataPerPacket"/>.
    /// </param>
    private void AppendOneStreamChunk(List<Frame> frames, ref long sendBudget, int payloadLeft)
    {
        // Choose the prioritised candidate — possibly retry when a stream, despite HasPending,
        // currently yields no frame (e.g. stream flow control exhausted).
        var skip = new HashSet<ulong>();
        while (true)
        {
            QuicStream? stream = PickSendStream(skip);
            if (stream is null)
                return;
            ulong connWindow = _connSendLimit > _connSendUsed ? _connSendLimit - _connSendUsed : 0;
            if (connWindow == 0)
                return; // connection window exhausted
            int maxChunk = Math.Min(MaxStreamDataPerPacket, payloadLeft);
            int chunk = (int)Math.Min(Math.Min((ulong)maxChunk, connWindow), (ulong)sendBudget);
            if (chunk <= 0)
                return;
            StreamFrame? sf = stream.Send.NextFrame(chunk);
            if (sf is null)
            {
                skip.Add(stream.Id.Value);
                continue;
            }
            frames.Add(sf);
            _connSendUsed += (ulong)sf.Data.Length;
            sendBudget -= sf.Data.Length;
            if (stream.SendIncremental)
                _incrementalCursor = stream.Id.Value; // advance the round-robin cursor
            return; // one chunk per packet
        }
    }

    /// <summary>
    /// Chooses the next stream to serve per RFC 9218 §10 (see
    /// <see cref="AppendOneStreamChunk"/>). <paramref name="skip"/> contains streams already tried
    /// unsuccessfully in this call.
    /// </summary>
    private QuicStream? PickSendStream(HashSet<ulong> skip)
    {
        // 1) Determine the highest pending urgency (smallest urgency value).
        int bestUrgency = int.MaxValue;
        foreach (QuicStream s in StreamMap.Values)
            if (s.Send.HasPending && !skip.Contains(s.Id.Value) && s.SendUrgency < bestUrgency)
                bestUrgency = s.SendUrgency;
        if (bestUrgency == int.MaxValue)
            return null;

        // 2) Non-incremental first: serve the smallest stream ID exclusively (request order, §10).
        QuicStream? nonIncremental = null;
        foreach (QuicStream s in StreamMap.Values)
            if (s.Send.HasPending && !skip.Contains(s.Id.Value) && s.SendUrgency == bestUrgency &&
                !s.SendIncremental && (nonIncremental is null || s.Id.Value < nonIncremental.Id.Value))
                nonIncremental = s;
        if (nonIncremental is not null)
            return nonIncremental;

        // 3) Incremental only: round-robin — smallest ID above the cursor, otherwise from the start.
        QuicStream? next = null, first = null;
        foreach (QuicStream s in StreamMap.Values)
        {
            if (!s.Send.HasPending || skip.Contains(s.Id.Value) || s.SendUrgency != bestUrgency)
                continue;
            if (first is null || s.Id.Value < first.Id.Value)
                first = s;
            if (s.Id.Value > _incrementalCursor && (next is null || s.Id.Value < next.Id.Value))
                next = s;
        }
        return next ?? first;
    }

    /// <summary>
    /// Emits 0-RTT packets (RFC 9001 §4) with early-queued application data (typically the HTTP/3
    /// request) while the 1-RTT keys are still missing. Only the client sends 0-RTT; the packets carry
    /// stream frames in the application packet-number space and are protected with the 0-RTT keys
    /// (long header 0x01). After the handshake the rest runs over 1-RTT (shared stream/PN state).
    /// </summary>
    private void BuildZeroRttPackets(List<byte[]> datagrams)
    {
        if (IsServer || _appKeysInstalled || _zeroRttKeys is not { } keys)
            return;
        int i = (int)EncryptionLevel.Application;

        // Rough cap at the conservative initial congestion window; a small GET fits in easily.
        for (int packetCount = 0; packetCount < 10; packetCount++)
        {
            var frames = new List<Frame>();
            foreach (QuicStream stream in StreamMap.Values)
            {
                if (!stream.Send.HasPending)
                    continue;
                ulong connWindow = _connSendLimit > _connSendUsed ? _connSendLimit - _connSendUsed : 0;
                if (connWindow == 0)
                    break;
                int chunk = (int)Math.Min((ulong)MaxStreamDataPerPacket, connWindow);
                StreamFrame? sf = stream.Send.NextFrame(chunk);
                if (sf is null)
                    continue;
                frames.Add(sf);
                _connSendUsed += (ulong)sf.Data.Length;
                break; // one stream chunk per packet
            }
            if (frames.Count == 0)
                return;

            ulong pn = Spaces[i].NextPacketNumber();
            int pnLength = PacketNumber.EncodeLength(pn, Spaces[i].LargestAckedByPeer);
            byte[] packet = LongHeader.Build(keys, LongPacketType.ZeroRtt, Version, Dcid, Scid, default,
                pn, pnLength, FrameParser.Serialize(frames));
            RecordSent(i, pn, packet.Length, frames);
            datagrams.Add(packet);
            _zeroRttDataSent = true;
            _zeroRttMaxPacketNumber = pn; // PNs are monotonic ⇒ this is the boundary to later 1-RTT packets
        }
    }

    /// <summary>
    /// Handles a 0-RTT rejection (RFC 9001 §4.6.2): when the client sent early data but the server did
    /// not accept it (no early_data in EncryptedExtensions), the frames sent as 0-RTT are repeated
    /// immediately over 1-RTT – without waiting for time threshold/PTO.
    /// </summary>
    private void MaybeHandleZeroRttRejection()
    {
        if (_zeroRttRejectionHandled || IsServer || !_zeroRttDataSent || !_appKeysInstalled)
            return;
        _zeroRttRejectionHandled = true;
        if (TlsHandshake?.EarlyDataAccepted == true)
            return; // accepted – the data continues normally

        int i = (int)EncryptionLevel.Application;
        _retransmitQueue[i].AddRange(_recovery.OnZeroRttRejected(i, _zeroRttMaxPacketNumber));
    }

    /// <summary>
    /// Fetches pending RESET_STREAM/STOP_SENDING frames of the streams (RFC 9000 §19.4/§19.5).
    /// Reliability arises via loss recovery: both frame types are tracked as retransmittable and
    /// re-queued on loss.
    /// </summary>
    private void CollectStreamCancellationFrames(List<Frame> frames)
    {
        bool peerResetAt = PeerParams?.PeerSupportsResetStreamAt ?? false;
        foreach ((ulong id, QuicStream stream) in StreamMap)
        {
            if (stream.Send.TakeResetFrame(peerResetAt) is { } reset)
                frames.Add(reset);
            if (stream.Receive.TakeStopSendingErrorCode() is { } errorCode)
                frames.Add(new StopSendingFrame(id, errorCode));
        }
    }

    private void DrainRetransmitQueue(int level, List<Frame> frames)
    {
        if (_retransmitQueue[level].Count == 0)
            return;
        int before = frames.Count;
        foreach (Frame frame in _retransmitQueue[level])
        {
            // RFC 9000 §19.4: after a RESET_STREAM, no longer (re)transmit STREAM frames of that
            // stream; §3.5: a STOP_SENDING is superfluous once the peer has already reset. Exception:
            // after RESET_STREAM_AT, data up to the reliable size must keep being retransmitted
            // (draft-ietf-quic-reliable-stream-reset §5).
            if (frame is StreamFrame sf && StreamMap.TryGetValue(sf.StreamId, out QuicStream? s) && s.Send.IsReset &&
                !(s.Send.IsResetAt && sf.Offset < s.Send.ReliableSize))
                continue;
            if (frame is StopSendingFrame ss && StreamMap.TryGetValue(ss.StreamId, out QuicStream? r) && r.Receive.ResetReceived)
                continue;
            frames.Add(frame);
        }
        _retransmitQueue[level].Clear();

        // Counted where the frames actually leave, not where they were queued: the filters above
        // drop frames of streams that have since been reset, and those never went out again.
        if (QuicMetrics.FramesRetransmitted.Enabled && frames.Count > before)
            QuicMetrics.FramesRetransmitted.Add(frames.Count - before, QuicMetrics.RoleTag(IsServer));
    }

    private void RecordSent(int level, ulong packetNumber, int size, List<Frame> frames, bool isPmtuProbe = false)
    {
        Qlog?.PacketSent(QlogTimeMs, QlogPacketType(level), packetNumber, size, frames);
        if (QuicMetrics.PacketsSent.Enabled)
            QuicMetrics.PacketsSent.Add(1, QuicMetrics.RoleTag(IsServer));

        // RFC 9000 §13.2.4: remember which Largest Acknowledged we reported in which packet — its
        // acknowledgment later releases the ACK state. Must happen for pure ACK packets too (the peer
        // reports those in its ranges as well), i.e. before the ack-eliciting early exit below.
        foreach (Frame frame in frames)
            if (frame is AckFrame sentAck)
            {
                Spaces[level].OnAckFrameSent(packetNumber, sentAck.LargestAcknowledged);
                break;
            }

        bool ackEliciting = frames.Any(f => f is not AckFrame and not PaddingFrame);
        if (!ackEliciting)
            return; // pure ACK packets count neither towards bytes_in_flight nor the pacing budget
        _pacer.OnBytesSent(size);
        _idle.OnAckElicitingPacketSent(NowTicks); // RFC 9000 §10.1
        // RESET_STREAM/STOP_SENDING must arrive reliably (RFC 9000 §19.4/§3.5) ⇒ track them.
        // HANDSHAKE_DONE likewise: RFC 9000 §13.3 requires retransmission until acknowledged — if it
        // is lost, the client never learns that the handshake is confirmed and, once the server has
        // discarded its Handshake keys, cannot be reached by a Handshake-level probe either ⇒ deadlock.
        List<Frame> retransmittable = frames.Where(f => f is CryptoFrame or StreamFrame or ResetStreamFrame
                                                          or ResetStreamAtFrame or StopSendingFrame or HandshakeDoneFrame).ToList();
        if (retransmittable.Any(f => f is HandshakeDoneFrame))
            HandshakeDoneSentCountForTest++;
        _recovery.OnPacketSent(level, new SentPacket
        {
            PacketNumber = packetNumber,
            TimeSentTicks = NowTicks,
            AckEliciting = true,
            Size = size,
            RetransmittableFrames = retransmittable,
            IsPmtuProbe = isPmtuProbe,
        });
    }

    /// <summary>
    /// Checks the PTO (RFC 9002 §6.2) and enqueues retransmissions on expiry. Call periodically.
    /// </summary>
    public void CheckLossDetectionTimeout()
    {
        MaybeDiscardServerZeroRttKeys(); // purely time-driven (§4.9.3) – even without further traffic

        long deadline = _recovery.GetProbeTimeoutDeadline();
        if (deadline < 0 || NowTicks < deadline)
            return;
        _recovery.OnProbeTimeoutFired();

        bool anyProbe = false;
        for (int level = 0; level < LevelCount; level++)
        {
            if (WriteKeys[level] is null)
                continue;
            List<Frame> probe = _recovery.GetProbeFrames(level);
            if (probe.Count == 0)
                continue;
            _retransmitQueue[level].AddRange(probe);
            anyProbe = true;
        }

        // Nothing outstanding to repeat ⇒ send an ack-eliciting PING anyway (RFC 9002 §6.2.4).
        // Without it, a PTO at a client whose peer has not yet validated the address (§6.2.2.1)
        // would produce no packet at all and the handshake would stay stuck.
        if (!anyProbe)
            for (int level = LevelCount - 1; level >= 0; level--)
                if (WriteKeys[level] is not null)
                {
                    _retransmitQueue[level].Add(PingFrame.Instance);
                    break;
                }
    }

    private void CollectFlowControlFrames(List<Frame> frames)
    {
        long now = NowTicks;
        long rttTicks = _recovery.Rtt.SmoothedRtt.Ticks;

        foreach ((ulong id, QuicStream stream) in StreamMap)
        {
            if (stream.Receive.WindowTuner is not { } tuner || stream.Receive.HighestReceivedOffset == 0)
                continue;
            StreamReceiveBuffer recv = stream.Receive;
            if (recv.MaxData - recv.BytesConsumed < tuner.Size / 2)
            {
                tuner.NoteWindowUpdate(now, rttTicks); // auto-tuning: double the window on fast drainage
                recv.MaxData = recv.BytesConsumed + tuner.Size;
                frames.Add(new MaxStreamDataFrame(id, recv.MaxData));
            }
        }

        if (_connWindowTuner is not { } connTuner)
            return;
        ulong totalConsumed = 0;
        foreach (QuicStream s in StreamMap.Values)
            totalConsumed += s.Receive.BytesConsumed;
        if (_localConnMaxData - totalConsumed < connTuner.Size / 2)
        {
            connTuner.NoteWindowUpdate(now, rttTicks);
            _localConnMaxData = totalConsumed + connTuner.Size;
            frames.Add(new MaxDataFrame(_localConnMaxData));
        }
    }

    /// <summary>
    /// Current (possibly auto-tuned) size of the connection receive window — for diagnostics/tests.
    /// </summary>
    internal ulong ConnectionReceiveWindowSize => _connWindowTuner?.Size ?? _localConnMaxData;

    // ---- Receiving -------------------------------------------------------------------------

    /// <summary>
    /// Processes a received UDP datagram (multiple coalesced packets possible).
    /// </summary>
    /// <param name="ecn">
    /// The ECN codepoint of the received IP datagram (RFC 9000 §13.4). If the transport layer cannot
    /// read it from the IP header (the default with BCL UDP sockets), it stays Not-ECT; the protocol
    /// logic (counting, reporting in the ACK, CE reaction) works regardless and is thus fully testable.
    /// </param>
    public void ProcessDatagram(ReadOnlySpan<byte> datagram, EcnCodepoint ecn = EcnCodepoint.NotEct)
    {
        MaybeTransitionToClosed();
        if (_idleTimedOut || datagram.IsEmpty || _state is ConnectionState.Draining or ConnectionState.Closed)
            return; // silently closed, draining or empty

        if (QuicMetrics.BytesReceived.Enabled)
            QuicMetrics.BytesReceived.Add(datagram.Length, QuicMetrics.RoleTag(IsServer));

        // Anti-amplification (RFC 9000 §8.1): received bytes raise the send budget (until validation).
        if (!_addressValidated)
            _amplificationReceived += datagram.Length;

        // In the closing state, process no more frames; every incoming packet triggers a renewed
        // CONNECTION_CLOSE (RFC 9000 §10.2.1, roughly 1:1 rate-limited).
        if (_state == ConnectionState.Closing)
        {
            _closePacketPending = true;
            return;
        }

        // Detect the special cases up front on the first (non-coalescable) packet: version negotiation
        // (version 0), Retry (its own type, no length) and unsupported versions (RFC 9000 §6, §17.2.1, §17.2.5).
        byte firstByte = datagram[0];
        if (PacketFormat.IsLongHeader(firstByte) && datagram.Length >= 5)
        {
            uint firstVersion = (uint)((datagram[1] << 24) | (datagram[2] << 16) | (datagram[3] << 8) | datagram[4]);
            if (firstVersion == 0)
            {
                HandleVersionNegotiationPacket(datagram);
                return;
            }
            if (PacketFormat.GetLongPacketType(firstByte) == LongPacketType.Retry)
            {
                HandleRetryPacket(datagram);
                return;
            }
            if (firstVersion != Version)
            {
                HandleUnsupportedVersion(datagram);
                return;
            }
        }

        int offset = 0;
        while (offset < datagram.Length)
        {
            byte first = datagram[offset];
            if (PacketFormat.IsShortHeader(first))
            {
                ProcessShortHeaderPacket(datagram[offset..], ecn);
                break;
            }

            if (offset + 5 > datagram.Length)
                break;
            uint version = (uint)((datagram[offset + 1] << 24) | (datagram[offset + 2] << 16) |
                                  (datagram[offset + 3] << 8) | datagram[offset + 4]);
            if (version == 0)
                break; // version negotiation only as a standalone datagram (handled above)

            LongPacketType type = PacketFormat.GetLongPacketType(first);
            if (!LongHeader.TryParse(datagram[offset..], out LongHeaderPrefix? prefix) || prefix is null)
                break;

            OnLongHeaderPacket(type, prefix);

            // 0-RTT (RFC 9001 §4): the server decrypts early data with the 0-RTT read keys in the
            // application packet-number space (frames land in the application layer like 1-RTT data).
            if (type == LongPacketType.ZeroRtt && IsServer && _zeroRttKeys is { } earlyKeys)
            {
                byte[] earlyPacket = datagram.Slice(offset, prefix.PacketEndOffset).ToArray();
                DecryptAndHandle(earlyKeys, EncryptionLevel.Application, earlyPacket, prefix.PacketNumberOffset, longHeader: true, ecn);
            }

            EncryptionLevel? level = type switch
            {
                LongPacketType.Initial => EncryptionLevel.Initial,
                LongPacketType.Handshake => EncryptionLevel.Handshake,
                _ => null,
            };
            if (level is { } lvl && ReadKeys[(int)lvl] is { } keys)
            {
                byte[] packet = datagram.Slice(offset, prefix.PacketEndOffset).ToArray();
                DecryptAndHandle(keys, lvl, packet, prefix.PacketNumberOffset, longHeader: true, ecn);
            }

            offset += prefix.PacketEndOffset;
        }

        // After processing the datagram, discard keys no longer needed (RFC 9001 §4.9):
        // Initial once the server processed a Handshake packet; Handshake once the handshake is confirmed.
        MaybeDiscardInitialKeys();
        MaybeDiscardHandshakeKeys();
        MaybeDiscardServerZeroRttKeys(); // 0-RTT read keys after the 3×PTO deadline expires (§4.9.3)
    }

    private void ProcessShortHeaderPacket(ReadOnlySpan<byte> packetSpan, EcnCodepoint ecn)
    {
        int i = (int)EncryptionLevel.Application;
        if (ReadKeys[i] is not { } current)
            return;
        if (!ShortHeader.TryLocatePacketNumber(packetSpan, Scid.Length, out ConnectionId dcid, out int pnOffset))
            return;
        // Accept only packets for one of our active (issued) connection IDs (RFC 9000 §5.1);
        // an unknown DCID could be a stateless reset (§10.3).
        if (!_cids.IsLocalConnectionId(dcid))
        {
            TryHandleStatelessReset(packetSpan);
            return;
        }

        byte[] packet = packetSpan.ToArray();
        // Remove the header protection (the HP key is constant across key updates) → then read the key-phase bit.
        if (!current.RemoveHeaderProtection(packet, pnOffset, Spaces[i].LargestReceived, longHeader: false, out ulong pn, out int headerLength))
        {
            Qlog?.PacketDropped(QlogTimeMs, "1RTT", packet.Length, "header_protection_failure");
            return;
        }

        bool packetKeyPhase = (packet[0] & 0x04) != 0;
        ReadOnlySpan<byte> header = packet.AsSpan(0, headerLength);
        ReadOnlySpan<byte> body = packet.AsSpan(headerLength);
        byte[] plaintext = new byte[packet.Length];
        int len;

        if (packetKeyPhase == _recvKeyPhase)
        {
            // Current phase; on failure possibly a reordered packet of the previous phase (after an update).
            if (current.Decrypt(pn, header, body, plaintext, out len)) { }
            else if (_prevAppReadKeys is { } prev && prev.Decrypt(pn, header, body, plaintext, out len)) { }
            else { TryHandleStatelessReset(packetSpan); return; }
        }
        else
        {
            // Flipped key-phase bit ⇒ peer key update (RFC 9001 §6). Check with the next read keys.
            if (_nextAppReadKeys is not { } next || !next.Decrypt(pn, header, body, plaintext, out len))
            {
                TryHandleStatelessReset(packetSpan);
                return;
            }
            CommitPeerKeyUpdate(packetKeyPhase);
        }

        DeliverApplicationFrames(pn, plaintext, len, ecn, packet.Length);
    }

    /// <summary>
    /// Checks whether a non-processable (short-header) datagram is a stateless reset (RFC 9000
    /// §10.3.1): if the last 16 bytes end in a peer stateless-reset token known to us, the connection
    /// is immediately put into the draining state.
    /// </summary>
    private bool TryHandleStatelessReset(ReadOnlySpan<byte> datagram)
    {
        if (datagram.Length < StatelessReset.MinLength ||
            !_cids.MatchesRemoteStatelessResetToken(datagram[^StatelessReset.TokenLength..]))
            return false;
        StatelessResetReceived = true;
        EnterDraining();
        return true;
    }

    private void DeliverApplicationFrames(ulong packetNumber, byte[] plaintext, int length, EcnCodepoint ecn,
                                          int packetLength = 0)
    {
        _idle.OnPacketReceived(NowTicks); // RFC 9000 §10.1
        _anyPacketProcessed = true;
        Spaces[(int)EncryptionLevel.Application].RecordReceived(packetNumber, ecn, NowTicks);
        // Only when the qlog is on: parse the frames a second time purely for the log.
        if (Qlog is { } qlog && FrameParser.TryParseAll(plaintext.AsSpan(0, length), out List<Frame> loggedFrames) == FrameParseResult.Ok)
            qlog.PacketReceived(QlogTimeMs, "1RTT", packetNumber, packetLength, loggedFrames);

        // Only genuine 1-RTT packets (short header) land here – 0-RTT (long header 0x01) runs through
        // DecryptAndHandle. With the first 1-RTT packet the server knows the upper bound of the 0-RTT
        // PNs and starts the short retention period of its 0-RTT read keys (RFC 9001 §4.9.3): after it,
        // it MUST discard them "within a short time", RECOMMENDED 3×PTO. If ALL 0-RTT packets are
        // already present (gap-free), it discards immediately.
        if (IsServer)
        {
            _serverOneRttPacketReceived = true;
            if (_zeroRttKeys is not null && _serverZeroRttDiscardDeadlineTicks < 0)
                _serverZeroRttDiscardDeadlineTicks = NowTicks + ServerZeroRttDiscardDelay().Ticks;
            MaybeDiscardServerZeroRttKeysIfComplete();
        }

        DeliverFrames(EncryptionLevel.Application, plaintext.AsSpan(0, length), packetNumber);
    }

    private void DecryptAndHandle(PacketProtection keys, EncryptionLevel level, byte[] packet, int pnOffset, bool longHeader, EcnCodepoint ecn)
    {
        int i = (int)level;
        byte[] plaintext = new byte[packet.Length];
        if (!keys.UnprotectPacket(packet, pnOffset, Spaces[i].LargestReceived, longHeader, plaintext, out ulong pn, out int len))
        {
            Qlog?.PacketDropped(QlogTimeMs, QlogPacketType(i), packet.Length, "decryption_failure");
            return;
        }

        _idle.OnPacketReceived(NowTicks); // RFC 9000 §10.1: restart the timer on successful receipt
        _anyPacketProcessed = true;
        // A decrypted Handshake packet proves the peer obtained our handshake keys ⇒
        // the address is validated (RFC 9000 §8.1), the anti-amplification limit is lifted.
        if (level == EncryptionLevel.Handshake)
        {
            _addressValidated = true;
            _handshakePacketReceived = true; // server: later triggers discarding the Initial keys (RFC 9001 §4.9.1)
        }
        Spaces[i].RecordReceived(pn, ecn, NowTicks);
        // Only when the qlog is on: parse the frames a second time purely for the log — that keeps
        // the hot path (DeliverFrames below) completely untouched.
        if (Qlog is { } qlog && FrameParser.TryParseAll(plaintext.AsSpan(0, len), out List<Frame> loggedFrames) == FrameParseResult.Ok)
            qlog.PacketReceived(QlogTimeMs, QlogPacketType(i), pn, packet.Length, loggedFrames);
        // Server 0-RTT receipt (application level, long header): if a reordered 0-RTT packet arrives
        // only AFTER the first 1-RTT packet and closes the last PN gap, all 0-RTT packets are present ⇒ keys gone (§4.9.3).
        if (level == EncryptionLevel.Application)
            MaybeDiscardServerZeroRttKeysIfComplete();
        DeliverFrames(level, plaintext.AsSpan(0, len), pn);
    }

    /// <summary>
    /// Parses a packet's frames; an encoding/unknown error is FRAME_ENCODING_ERROR (RFC 9000 §12.4).
    /// </summary>
    private void DeliverFrames(EncryptionLevel level, ReadOnlySpan<byte> plaintext, ulong packetNumber = 0)
    {
        if (FrameParser.TryParseAll(plaintext, out List<Frame> frames) != FrameParseResult.Ok)
        {
            CloseWithTransportError(TransportError.FrameEncodingError, "malformed or unknown frame");
            return;
        }
        HandleFrames(level, frames, packetNumber);
    }

    /// <summary>
    /// Closes the connection due to a peer protocol violation (RFC 9000 §11).
    /// </summary>
    private void CloseWithTransportError(TransportError error, string reason) => Close(error, reason);

    /// <summary>
    /// Closes with CRYPTO_ERROR carrying a TLS alert (RFC 9001 §4.8: the code is
    /// <c>0x0100 + AlertDescription</c>). QUIC has no alert records, so this is how the peer learns
    /// <em>which</em> handshake failure occurred.
    /// </summary>
    private void CloseWithCryptoError(TlsAlert alert, string reason)
        => EnterClosing(new ConnectionCloseFrame((ulong)TransportError.CryptoErrorBase + (ulong)alert,
                                                 IsApplicationError: false, 0, reason));

    /// <summary>
    /// The error code of the CONNECTION_CLOSE we sent, if any — diagnostics and tests.
    /// </summary>
    public ulong? LocalCloseErrorCode { get; private set; }

    private void HandleFrames(EncryptionLevel level, List<Frame> frames, ulong packetNumber = 0)
    {
        // RFC 9000 §13.2.1: only an ack-eliciting packet may start the acknowledgment clock — "An
        // endpoint MUST NOT send a non-ack-eliciting packet in response to a non-ack-eliciting
        // packet … This avoids an infinite feedback loop of acknowledgments."
        if (frames.Exists(f => f is not AckFrame and not PaddingFrame and not ConnectionCloseFrame))
            Spaces[(int)level].OnAckElicitingReceived(packetNumber, NowTicks);

        foreach (Frame frame in frames)
        {
            if (_state != ConnectionState.Active)
                return; // a violation (close) or a received CONNECTION_CLOSE ended the connection

            switch (frame)
            {
                case CryptoFrame crypto:
                    _recvCrypto[(int)level].Add(crypto.Offset, crypto.Data.Span);
                    DeliverCryptoToTls(level);
                    break;
                case AckFrame ack:
                    Spaces[(int)level].OnAckReceived(ack); // incl. ACK-state pruning, RFC 9000 §13.2.4
                    // A Handshake ACK proves the server has validated our address (RFC 9002 §A.6)
                    // ⇒ the §6.2.2.1 special case for the PTO no longer applies.
                    if (level == EncryptionLevel.Handshake && !IsServer)
                        _recovery.PeerCompletedAddressValidation = true;
                    // §19.3: the field is "decoded by multiplying the value in the field by 2 to
                    // the power of the ack_delay_exponent transport parameter sent by the SENDER of
                    // the ACK frame". Before the peer's parameters arrive the default of 3 applies,
                    // which is what PeerAckDelayExponent falls back to.
                    var ackDelay = TimeSpan.FromMicroseconds(ack.AckDelay * (1UL << (int)PeerAckDelayExponent));
                    _retransmitQueue[(int)level].AddRange(
                        _recovery.OnAckReceived((int)level, ack, ackDelay, NowTicks));
                    QlogRecoveryMetrics();
                    // When one of our 1-RTT packets is acknowledged, the client MAY regard the handshake
                    // as confirmed (RFC 9001 §4.1.2) – even without a (possibly lost) HANDSHAKE_DONE.
                    if (level == EncryptionLevel.Application &&
                        _firstOneRttPacketNumber is { } firstPn && ack.LargestAcknowledged >= firstPn)
                        OnOneRttPacketAcknowledged();
                    break;
                case StreamFrame sf:
                    HandleStreamFrame(sf);
                    break;
                case ResetStreamFrame rs:
                    HandleResetStream(rs);
                    break;
                case ResetStreamAtFrame rsa:
                    HandleResetStreamAt(rsa);
                    break;
                case StopSendingFrame ss:
                    HandleStopSending(ss);
                    break;
                case DatagramFrame datagram:
                    // RFC 9221 §3/§5: only permissible under 0-RTT/1-RTT protection; without our own
                    // announcement or above our announced limit ⇒ PROTOCOL_VIOLATION.
                    if (level != EncryptionLevel.Application ||
                        LocalParams.MaxDatagramFrameSizeValue == 0)
                    {
                        CloseWithTransportError(TransportError.ProtocolViolation, "unexpected DATAGRAM frame");
                        return;
                    }
                    if (1UL + (ulong)VarInt.GetLength((ulong)datagram.Data.Length) + (ulong)datagram.Data.Length >
                        LocalParams.MaxDatagramFrameSizeValue)
                    {
                        CloseWithTransportError(TransportError.ProtocolViolation, "DATAGRAM larger than announced limit");
                        return;
                    }
                    _receivedDatagrams.Add(datagram.Data.ToArray()); // §5: deliver to the application immediately
                    break;
                case MaxStreamDataFrame m when StreamMap.TryGetValue(m.StreamId, out QuicStream? s):
                    s.Send.MaxData = Math.Max(s.Send.MaxData, m.MaximumStreamData);
                    break;
                case MaxDataFrame md:
                    _connSendLimit = Math.Max(_connSendLimit, md.MaximumData);
                    break;
                case HandshakeDoneFrame:
                    OnHandshakeDoneReceived();
                    break;
                case NewTokenFrame newToken:
                    // §19.7: "Clients MUST NOT send NEW_TOKEN frames. A server MUST treat receipt of
                    // a NEW_TOKEN frame as a connection error of type PROTOCOL_VIOLATION."
                    if (IsServer)
                    {
                        CloseWithTransportError(TransportError.ProtocolViolation, "client sent NEW_TOKEN");
                        return;
                    }
                    // §19.7: "The token MUST NOT be empty. A client MUST treat receipt of a NEW_TOKEN
                    // frame with an empty Token field as a connection error of type
                    // FRAME_ENCODING_ERROR."
                    if (newToken.Token.Length == 0)
                    {
                        CloseWithTransportError(TransportError.FrameEncodingError, "empty NEW_TOKEN");
                        return;
                    }
                    OnNewTokenReceived(newToken.Token);
                    break;
                case NewConnectionIdFrame ncid:
                    HandleNewConnectionId(ncid);
                    break;
                case RetireConnectionIdFrame rcid:
                    _cids.RetireLocal(rcid.SequenceNumber); // the peer retires one of our local CIDs
                    break;
                case PathChallengeFrame pc:
                    _pendingControlFrames.Add(new PathResponseFrame(pc.Data)); // RFC 9000 §8.2: mirror it back
                    break;
                case PathResponseFrame pr:
                    if (_pathChallengePending == pr.Data) // matching answer → path validated
                    {
                        PathValidated = true;
                        _pathChallengePending = null;
                        _pathValidationDeadlineTicks = -1;
                    }
                    break;
                case ConnectionCloseFrame close:
                    PeerCloseFrame = close;
                    QuicEventSource.Log.ConnectionClosed(RoleName, Convert.ToHexString(Scid.Span),
                                                         "remote", (long)close.ErrorCode, close.ReasonPhrase);
                    EnterDraining(); // RFC 9000 §10.2.2: drain on receipt of a CONNECTION_CLOSE
                    return;          // process no further frames of this packet
            }

            if (level == EncryptionLevel.Application)
                OnApplicationFrameHandled(frame);
        }
    }

    private void EnterDraining()
    {
        if (_state is ConnectionState.Draining or ConnectionState.Closed)
            return;
        _state = ConnectionState.Draining;
        _closeDeadlineTicks = NowTicks + CloseTimeout().Ticks;
    }

    private void HandleNewConnectionId(NewConnectionIdFrame frame)
    {
        List<RetireConnectionIdFrame> retires = _cids.OnNewConnectionId(frame, out bool dcidChanged, out ConnectionId newDcid);
        if (dcidChanged)
            Dcid = newDcid; // the previous DCID was retired via "Retire Prior To"
        foreach (RetireConnectionIdFrame retire in retires)
            _pendingControlFrames.Add(retire);
    }

    /// <summary>
    /// Builds the CONNECTION_CLOSE datagram. With 1-RTT keys a short-header packet suffices; DURING
    /// the handshake, however, the close is sent coalesced on ALL available long-header levels
    /// (RFC 9000 §10.2.3): the peer may only have the Initial keys (e.g. when we reject its first
    /// flight before it ever saw ours) and could never read a pure Handshake close.
    /// </summary>
    private byte[]? BuildClosePacket()
    {
        if (_closeFrame is null)
            return null;
        byte[] payload = FrameParser.Serialize([_closeFrame]);

        // §10.2.3: AFTER the confirmed handshake the close MUST go as a 1-RTT packet. BEFORE that, a
        // pure 1-RTT close would be risky: the peer may only have Initial/Handshake keys (e.g. when
        // we reject its very first flight) and could never decrypt it.
        int app = (int)EncryptionLevel.Application;
        if (WriteKeys[app] is { } appKeys && HandshakeIsConfirmed)
        {
            ulong pn = Spaces[app].NextPacketNumber();
            return ShortHeader.Build(appKeys, Dcid, pn, PacketNumber.EncodeLength(pn, Spaces[app].LargestAckedByPeer), payload, keyPhase: _sendKeyPhase);
        }

        var datagram = new List<byte>();
        int init = (int)EncryptionLevel.Initial;
        if (WriteKeys[init] is { } initKeys)
        {
            ulong pn = Spaces[init].NextPacketNumber();
            datagram.AddRange(InitialPacketFactory.BuildPadded(initKeys, Version, Dcid, Scid, InitialToken, pn,
                PacketNumber.EncodeLength(pn, Spaces[init].LargestAckedByPeer), payload));
        }
        int hs = (int)EncryptionLevel.Handshake;
        if (WriteKeys[hs] is { } hsKeys)
        {
            ulong pn = Spaces[hs].NextPacketNumber();
            datagram.AddRange(LongHeader.Build(hsKeys, LongPacketType.Handshake, Version, Dcid, Scid, default, pn,
                PacketNumber.EncodeLength(pn, Spaces[hs].LargestAckedByPeer), payload));
        }
        if (datagram.Count == 0 && WriteKeys[app] is { } onlyAppKeys)
        {
            // Fallback: only 1-RTT keys remain (Initial/Handshake already discarded).
            ulong pn = Spaces[app].NextPacketNumber();
            return ShortHeader.Build(onlyAppKeys, Dcid, pn, PacketNumber.EncodeLength(pn, Spaces[app].LargestAckedByPeer), payload, keyPhase: _sendKeyPhase);
        }
        return datagram.Count > 0 ? [.. datagram] : null;
    }

    private void HandleStreamFrame(StreamFrame sf)
    {
        var id = new StreamId(sf.StreamId);

        // STREAM_LIMIT_ERROR (RFC 9000 §4.6): a peer-initiated stream beyond the limit we granted.
        if (!LocallyInitiated(id) && id.Index >= StreamLimitFor(id))
        {
            CloseWithTransportError(TransportError.StreamLimitError, "stream limit exceeded");
            return;
        }

        bool isNew = !StreamMap.ContainsKey(sf.StreamId);
        StreamReceiveResult result = GetOrCreateStream(id).Receive.Receive(sf.Offset, sf.Data.Span, sf.Fin);
        switch (result)
        {
            case StreamReceiveResult.FlowControlError:   // RFC 9000 §4.1
                CloseWithTransportError(TransportError.FlowControlError, "stream flow control exceeded");
                return;
            case StreamReceiveResult.FinalSizeError:     // RFC 9000 §4.5
                CloseWithTransportError(TransportError.FinalSizeError, "inconsistent final size");
                return;
        }
        if (ConnectionFlowControlViolated())             // §4.1: sum over all streams > MAX_DATA
            return;

        OnStreamOpened(id, isNew);
    }

    /// <summary>
    /// Checks the CONNECTION flow-control limit on the receive side (RFC 9000 §4.1): the sum of the
    /// highest received offsets of all streams (with RESET the final size counts, §4.5) must not
    /// exceed the window we granted via initial_max_data/MAX_DATA — otherwise FLOW_CONTROL_ERROR.
    /// Returns <c>true</c> when the connection was closed because of it.
    /// </summary>
    private bool ConnectionFlowControlViolated()
    {
        ulong totalReceived = 0;
        foreach (QuicStream s in StreamMap.Values)
            totalReceived += s.Receive.HighestReceivedOffset;
        if (totalReceived <= _localConnMaxData)
            return false;
        CloseWithTransportError(TransportError.FlowControlError, "connection flow control exceeded");
        return true;
    }

    /// <summary>
    /// Test seam: raises our own connection send limit above the one granted by the peer, to provoke
    /// peer-side FLOW_CONTROL_ERROR paths (transport-error-matrix tests).
    /// </summary>
    internal void OverrideConnSendLimitForTest(ulong limit) => _connSendLimit = limit;

    /// <summary>
    /// Test seam: the current DCID (for the §7.3 validator tests, which must construct
    /// matching/mismatching initial_source_connection_id values).
    /// </summary>
    internal ConnectionId DcidForTest => Dcid;

    /// <summary>
    /// The stream limit (count) we granted for the category of <paramref name="id"/>.
    /// </summary>
    private ulong StreamLimitFor(StreamId id)
        => id.IsUnidirectional ? LocalParams.InitialMaxStreamsUniValue : LocalParams.InitialMaxStreamsBidiValue;

    /// <summary>
    /// Processes a peer RESET_STREAM (RFC 9000 §19.4): validates stream state/limit, adopts the final
    /// size (§4.5) and marks the receive side as aborted.
    /// </summary>
    private void HandleResetStream(ResetStreamFrame rs)
    {
        var id = new StreamId(rs.StreamId);

        // §19.4: a RESET_STREAM for a stream on which the peer does not send at all (our uni send side)
        // or for a locally initiated, never-opened stream ⇒ STREAM_STATE_ERROR.
        if ((id.IsUnidirectional && LocallyInitiated(id)) ||
            (LocallyInitiated(id) && !StreamMap.ContainsKey(rs.StreamId)))
        {
            CloseWithTransportError(TransportError.StreamStateError, "RESET_STREAM on send-only or uncreated stream");
            return;
        }
        // §4.6: RESET_STREAM must not create a stream beyond the granted limit either.
        if (!LocallyInitiated(id) && id.Index >= StreamLimitFor(id))
        {
            CloseWithTransportError(TransportError.StreamLimitError, "stream limit exceeded");
            return;
        }

        switch (GetOrCreateStream(id).Receive.Reset(rs.ApplicationErrorCode, rs.FinalSize))
        {
            case StreamReceiveResult.FlowControlError:   // §4.1: final size above the granted window
                CloseWithTransportError(TransportError.FlowControlError, "reset final size exceeds flow control");
                return;
            case StreamReceiveResult.FinalSizeError:     // §4.5: contradictory final size
                CloseWithTransportError(TransportError.FinalSizeError, "inconsistent final size in RESET_STREAM");
                return;
        }
        ConnectionFlowControlViolated(); // §4.5: the final size counts fully against the connection window
    }

    /// <summary>
    /// Processes a peer RESET_STREAM_AT (draft-ietf-quic-reliable-stream-reset §4/§5): like
    /// <see cref="HandleResetStream"/>, but still delivers the first reliable-size bytes to the application.
    /// </summary>
    private void HandleResetStreamAt(ResetStreamAtFrame rsa)
    {
        var id = new StreamId(rsa.StreamId);

        // §19.4 (analogous): RESET_STREAM_AT on a pure send side or on a never-opened, locally
        // initiated stream ⇒ STREAM_STATE_ERROR.
        if ((id.IsUnidirectional && LocallyInitiated(id)) ||
            (LocallyInitiated(id) && !StreamMap.ContainsKey(rsa.StreamId)))
        {
            CloseWithTransportError(TransportError.StreamStateError, "RESET_STREAM_AT on send-only or uncreated stream");
            return;
        }
        if (!LocallyInitiated(id) && id.Index >= StreamLimitFor(id))
        {
            CloseWithTransportError(TransportError.StreamLimitError, "stream limit exceeded");
            return;
        }

        switch (GetOrCreateStream(id).Receive.ResetAt(rsa.ApplicationErrorCode, rsa.FinalSize, rsa.ReliableSize))
        {
            case StreamReceiveResult.FrameEncodingError: // draft §4: reliable size > final size
                CloseWithTransportError(TransportError.FrameEncodingError, "RESET_STREAM_AT reliable size exceeds final size");
                return;
            case StreamReceiveResult.FlowControlError:   // §4.1: final size above the granted window
                CloseWithTransportError(TransportError.FlowControlError, "reset final size exceeds flow control");
                return;
            case StreamReceiveResult.FinalSizeError:     // §4.5: contradictory final size
                CloseWithTransportError(TransportError.FinalSizeError, "inconsistent final size in RESET_STREAM_AT");
                return;
            case StreamReceiveResult.StreamStateError:   // draft §5.2: error code changed
                CloseWithTransportError(TransportError.StreamStateError, "RESET_STREAM_AT changed error code");
                return;
        }
        ConnectionFlowControlViolated(); // §4.5: the final size counts fully against the connection window
    }

    /// <summary>
    /// Processes a peer STOP_SENDING (RFC 9000 §19.5, §3.5): validates the stream state and resets our
    /// send side with the copied error code (MUST in "Ready"/"Send"; we always answer immediately —
    /// that also conservatively covers the MAY deferral in "Data Sent").
    /// </summary>
    private void HandleStopSending(StopSendingFrame ss)
    {
        var id = new StreamId(ss.StreamId);

        // §19.5: STOP_SENDING for a pure receive stream (peer-initiated uni ⇒ we never send)
        // or for a locally initiated, never-opened stream ⇒ STREAM_STATE_ERROR.
        if ((id.IsUnidirectional && !LocallyInitiated(id)) ||
            (LocallyInitiated(id) && !StreamMap.ContainsKey(ss.StreamId)))
        {
            CloseWithTransportError(TransportError.StreamStateError, "STOP_SENDING on receive-only or uncreated stream");
            return;
        }
        if (!LocallyInitiated(id) && id.Index >= StreamLimitFor(id))
        {
            CloseWithTransportError(TransportError.StreamLimitError, "stream limit exceeded");
            return;
        }

        QuicStream stream = GetOrCreateStream(id);
        stream.PeerStopSendingErrorCode = ss.ApplicationErrorCode;
        stream.Send.Reset(ss.ApplicationErrorCode); // §3.5: the error code SHOULD be copied
    }

    private void DeliverCryptoToTls(EncryptionLevel level)
    {
        if (TlsHandshake is null)
            return;
        int i = (int)level;
        byte[] contiguous = _recvCrypto[i].Contiguous();
        if (contiguous.Length <= _deliveredCrypto[i])
            return;

        try
        {
            TlsHandshake.ProvideCrypto(level, contiguous.AsSpan((int)_deliveredCrypto[i]));
        }
        catch (PostHandshakeAuthenticationException e)
        {
            // RFC 9001 §4.4: a post-handshake CertificateRequest is a PROTOCOL_VIOLATION, not a
            // crypto failure — it is a well-formed message the peer was simply not allowed to send.
            CloseWithTransportError(TransportError.ProtocolViolation, e.Message);
            return;
        }
        catch (Exception e) when (e is TlsHandshakeException or CertificateValidationException or InvalidOperationException)
        {
            // A failed handshake must CLOSE the connection, not escape into the I/O loop above —
            // one client with a bad certificate would otherwise take down every other connection
            // sharing that loop. RFC 9001 §4.8 carries the TLS alert as CRYPTO_ERROR + alert code.
            TlsAlert alert = e is TlsHandshakeException typed ? typed.Alert : TlsAlert.HandshakeFailure;
            CloseWithCryptoError(alert, e.Message);
            return;
        }
        _deliveredCrypto[i] = contiguous.Length;

        MaybeInstallHandshakeKeys();
        MaybeInstallApplicationKeys();
        MaybeInstallZeroRttKeys();
        MaybeDecodePeerParameters();
    }

    /// <summary>
    /// Installs the 0-RTT keys from the <c>client_early_traffic_secret</c> (RFC 9001 §4) as soon as it
    /// is available: on the client the write key, on the server the read key. The 0-RTT suite (unlike
    /// the negotiated one) is fixed even before the ServerHello because it is bound to the ticket.
    /// </summary>
    private void MaybeInstallZeroRttKeys()
    {
        if (_zeroRttInstalled ||
            TlsHandshake?.EarlyTrafficSecret is not { } secret ||
            TlsHandshake.EarlyDataCipherSuite is not { } suite)
            return;
        var ks = new KeySchedule(suite);
        _zeroRttKeys = new PacketProtection(TrafficKeys.FromSecret(ks.Hash, secret, ks.AeadKeyLength), AeadFor(suite));
        _zeroRttInstalled = true;
    }

    // ---- Key installation ------------------------------------------------------------------

    /// <summary>
    /// Installs the Initial keys from <paramref name="dcid"/> in the correct direction.
    /// </summary>
    protected void InstallInitialKeys(ConnectionId dcid)
    {
        InitialSecrets s = InitialSecrets.DeriveV1(dcid.Span);
        WriteKeys[(int)EncryptionLevel.Initial] = new PacketProtection(IsServer ? s.Server : s.Client);
        ReadKeys[(int)EncryptionLevel.Initial] = new PacketProtection(IsServer ? s.Client : s.Server);
    }

    /// <summary>
    /// <c>true</c> once the TLS handshake is confirmed (client: HANDSHAKE_DONE received; server: completed).
    /// </summary>
    protected virtual bool HandshakeIsConfirmed => false;

    /// <summary>
    /// Discards the protection keys of an encryption level (RFC 9001 §4.9): keys gone, pending CRYPTO
    /// and retransmits discarded, the loss-recovery space cleaned up (RFC 9002 §6.4). Afterwards
    /// packets are neither sent (WriteKeys null) nor processed (ReadKeys null) on this level.
    /// </summary>
    private void DiscardKeys(EncryptionLevel level)
    {
        Qlog?.KeyDiscarded(QlogTimeMs, level == EncryptionLevel.Initial
            ? (IsServer ? "server_initial_secret" : "client_initial_secret")
            : level == EncryptionLevel.Handshake
                ? (IsServer ? "server_handshake_secret" : "client_handshake_secret")
                : (IsServer ? "server_0rtt_secret" : "client_0rtt_secret"));

        int i = (int)level;
        WriteKeys[i]?.Dispose();
        WriteKeys[i] = null;
        ReadKeys[i]?.Dispose();
        ReadKeys[i] = null;
        _outgoingCrypto[i].Clear();
        _retransmitQueue[i].Clear();
        _recovery.DiscardSpace(i);
    }

    /// <summary>
    /// Discards the Initial keys (RFC 9001 §4.9.1): the client as soon as it has <b>sent</b> a
    /// Handshake packet; the server as soon as it has <b>processed</b> one. Exactly these points are
    /// what the RFC prescribes – <b>not earlier</b>: §4.9 forbids discarding while the peer has not
    /// "done the same", and the Initial keys are still needed to ack the peer's Initial or to
    /// retransmit CRYPTO at the Initial level. The call at the end of the flight/datagram ensures the
    /// due Initial ACKs go out first. (An "earlier" discard would violate the RFC and gain nothing –
    /// ACK and Finished are in the same flight anyway.)
    /// </summary>
    private void MaybeDiscardInitialKeys()
    {
        if (_initialKeysDiscarded || WriteKeys[(int)EncryptionLevel.Initial] is null)
            return;
        if (!(IsServer ? _handshakePacketReceived : _handshakePacketSent))
            return;
        _initialKeysDiscarded = true;
        DiscardKeys(EncryptionLevel.Initial);
    }

    /// <summary>
    /// Discards the Handshake keys as soon as the handshake is confirmed (RFC 9001 §4.9.2) – an
    /// unconditional MUST, <b>without</b> a retention window. Checked and deliberately so: unlike
    /// §4.9.3 for 0-RTT (there, MAY "Servers … temporarily retain 0-RTT keys … three times the PTO"
    /// against reordering), §4.9.2 grants <b>no</b> reordering window for Handshake keys. That is no
    /// omission but intent: confirmation (§4.1.2) means a mutually finished handshake; per §4.9,
    /// "new data … at the highest currently available encryption level" is sent from then on, with
    /// only ACKs and CRYPTO retransmits on the lower levels. A late reordered Handshake packet would
    /// thus at most carry already-known CRYPTO data or a duplicate ACK – nothing whose loss costs
    /// content (with 0-RTT, by contrast, the sender still produces real app data, hence the window
    /// only there). Keeping keys longer would only extend the attack window – hence the immediate
    /// discard. (The "keep briefly for reordering" of previous read keys applies exclusively to the
    /// 1-RTT key update per §6, not to the Handshake keys.)
    /// </summary>
    private void MaybeDiscardHandshakeKeys()
    {
        if (HandshakeIsConfirmed)
            _recovery.PeerCompletedAddressValidation = true; // RFC 9002 §A.6

        if (_handshakeKeysDiscarded || !HandshakeIsConfirmed || WriteKeys[(int)EncryptionLevel.Handshake] is null)
            return;
        _handshakeKeysDiscarded = true;
        DiscardKeys(EncryptionLevel.Handshake);
    }

    /// <summary>
    /// Test helper: <c>true</c> while the Handshake protection keys are installed (RFC 9001 §4.9.2).
    /// </summary>
    internal bool HasHandshakeKeysForTest =>
        ReadKeys[(int)EncryptionLevel.Handshake] is not null || WriteKeys[(int)EncryptionLevel.Handshake] is not null;

    /// <summary>
    /// Test helper: <c>true</c> while the 0-RTT keys are installed (RFC 9001 §4.9.3).
    /// </summary>
    internal bool HasZeroRttKeysForTest => _zeroRttKeys is not null;

    /// <summary>
    /// Test helper: overrides the server's 0-RTT discard deadline (instead of 3×PTO) to check it deterministically.
    /// </summary>
    internal TimeSpan? ServerZeroRttDiscardDelayForTest { get; set; }

    /// <summary>
    /// Retention period of the server's 0-RTT read keys after the first 1-RTT packet (RFC 9001 §4.9.3: 3×PTO recommended).
    /// </summary>
    private TimeSpan ServerZeroRttDiscardDelay() =>
        ServerZeroRttDiscardDelayForTest ?? 3 * _recovery.Rtt.GetProbeTimeout(_recovery.MaxAckDelay);

    /// <summary>
    /// Discards the server's 0-RTT read keys once the short retention period after the first 1-RTT
    /// packet has expired (RFC 9001 §4.9.3). Until then they stay installed to still be able to
    /// decrypt reordered 0-RTT packets without forcing a retransmission over 1-RTT.
    /// </summary>
    private void MaybeDiscardServerZeroRttKeys()
    {
        if (_zeroRttKeys is null || _serverZeroRttDiscardDeadlineTicks < 0 ||
            NowTicks < _serverZeroRttDiscardDeadlineTicks)
            return;
        _zeroRttKeys.Dispose();
        _zeroRttKeys = null;
    }

    /// <summary>
    /// Discards the server's 0-RTT read keys <b>earlier</b> than after 3×PTO, once all 0-RTT packets
    /// have certainly been received (RFC 9001 §4.9.3, last sentence: "A server MAY discard 0-RTT keys
    /// earlier if it determines that it has received all 0-RTT packets, … by keeping track of missing
    /// packet numbers"). 0-RTT PNs start at 0 and all lie below the first 1-RTT PN; so if a 1-RTT
    /// packet has already arrived (upper bound known) AND the application space is gap-free from 0,
    /// no reordered 0-RTT packet can still be outstanding.
    /// </summary>
    private void MaybeDiscardServerZeroRttKeysIfComplete()
    {
        if (!IsServer || _zeroRttKeys is null || !_serverOneRttPacketReceived ||
            !Spaces[(int)EncryptionLevel.Application].IsContiguousFromZero)
            return;
        _zeroRttKeys.Dispose();
        _zeroRttKeys = null;
    }

    private void MaybeInstallHandshakeKeys()
    {
        if (_handshakeKeysInstalled || TlsHandshake?.HandshakeSecrets is not { } hs || TlsHandshake.NegotiatedCipherSuite is not { } suite)
            return;
        var ks = new KeySchedule(suite);
        AeadAlgorithm aead = AeadFor(suite);
        byte[] write = IsServer ? hs.ServerHandshakeTrafficSecret : hs.ClientHandshakeTrafficSecret;
        byte[] read = IsServer ? hs.ClientHandshakeTrafficSecret : hs.ServerHandshakeTrafficSecret;
        WriteKeys[(int)EncryptionLevel.Handshake] = new PacketProtection(TrafficKeys.FromSecret(ks.Hash, write, ks.AeadKeyLength), aead);
        ReadKeys[(int)EncryptionLevel.Handshake] = new PacketProtection(TrafficKeys.FromSecret(ks.Hash, read, ks.AeadKeyLength), aead);
        _handshakeKeysInstalled = true;
    }

    /// <summary>
    /// AEAD algorithm for the cipher suite (Initial always uses AES-128-GCM, RFC 9001 §5.2).
    /// </summary>
    private static AeadAlgorithm AeadFor(CipherSuite suite)
        => suite == CipherSuite.ChaCha20Poly1305Sha256 ? AeadAlgorithm.ChaCha20Poly1305 : AeadAlgorithm.AesGcm;

    private void MaybeInstallApplicationKeys()
    {
        if (_appKeysInstalled || TlsHandshake?.ApplicationSecrets is not { } app || TlsHandshake.NegotiatedCipherSuite is not { } suite)
            return;
        var ks = new KeySchedule(suite);
        byte[] write = IsServer ? app.ServerApplicationTrafficSecret : app.ClientApplicationTrafficSecret;
        byte[] read = IsServer ? app.ClientApplicationTrafficSecret : app.ServerApplicationTrafficSecret;

        _appHash = ks.Hash;
        _appHashLength = ks.HashLength;
        _appAead = AeadFor(suite);
        _appWriteTk = TrafficKeys.FromSecret(ks.Hash, write, ks.AeadKeyLength);
        _appReadTk = TrafficKeys.FromSecret(ks.Hash, read, ks.AeadKeyLength);
        WriteKeys[(int)EncryptionLevel.Application] = new PacketProtection(_appWriteTk, _appAead);
        ReadKeys[(int)EncryptionLevel.Application] = new PacketProtection(_appReadTk, _appAead);

        // Prepare generation 1 of the read keys, so a peer key update can be decoded immediately.
        _nextAppReadTk = _appReadTk.Next(_appHash, _appHashLength);
        _nextAppReadKeys = new PacketProtection(_nextAppReadTk, _appAead);

        // The DCID learned in the handshake is the remote connection ID with sequence 0.
        _cids.InitializeRemote(Dcid);
        ApplyPeerStatelessResetToken();
        _appKeysInstalled = true;

        // The 1-RTT keys are the honest "handshake done" mark for both roles: from here application
        // data flows. Reported once — this method installs the keys exactly once per connection.
        double handshakeMs = NowTicks / (double)TimeSpan.TicksPerMillisecond;
        QuicMetrics.Handshakes.Add(1, QuicMetrics.RoleTag(IsServer));
        if (QuicMetrics.HandshakeDuration.Enabled)
            QuicMetrics.HandshakeDuration.Record(handshakeMs, QuicMetrics.RoleTag(IsServer));
        QuicEventSource.Log.HandshakeCompleted(RoleName, Convert.ToHexString(Scid.Span), handshakeMs);

        // RFC 9001 §4.9.3: the client SHOULD discard its 0-RTT keys once the 1-RTT keys are in place –
        // "as they have no use after that moment": it sends no more 0-RTT packets after the first
        // 1-RTT packet (§5.6) and NEVER receives any itself (0-RTT is client→server), so it has no
        // 0-RTT read path at all. Hence – unlike on the server – there is deliberately NO reordering
        // window here: reordered/late packets at the client are unprotected with Initial/Handshake/
        // 1-RTT read keys, never with 0-RTT keys; and lost 0-RTT data is retransmitted over 1-RTT
        // (application retransmit queue ⇒ BuildApplicationPackets), not re-encrypted as 0-RTT.
        // Immediate discarding minimises the attack window. (Only the SERVER keeps 0-RTT READ keys
        // briefly for reordered 0-RTT packets – a different trigger, §4.9.3, see below.)
        if (!IsServer)
        {
            _zeroRttKeys?.Dispose();
            _zeroRttKeys = null;
        }
    }

    /// <summary>
    /// Adopts the peer's <c>stateless_reset_token</c> TP as the token of the remote handshake CID.
    /// </summary>
    private void ApplyPeerStatelessResetToken()
    {
        if (PeerParams?.StatelessResetTokenValue is { } token)
            _cids.SetInitialRemoteToken(token);
    }

    // ---- Key update (RFC 9001 §6) ----------------------------------------------------------

    /// <summary>
    /// Current number of completed 1-RTT key updates (diagnostics/test).
    /// </summary>
    public uint KeyUpdateCount => _keyUpdateCount;

    /// <summary>
    /// The current key-phase bit carried by outgoing 1-RTT packets.
    /// </summary>
    public bool CurrentKeyPhase => _sendKeyPhase;

    /// <summary>
    /// Initiates a 1-RTT key update locally (RFC 9001 §6.1): rotates our own send keys to the next
    /// generation and flips the key-phase bit. The read keys follow once the peer answers with the
    /// new phase. Requires installed application keys.
    /// </summary>
    public bool InitiateKeyUpdate()
    {
        if (!_appKeysInstalled || _appWriteTk is null)
            return false;
        _appWriteTk = _appWriteTk.Next(_appHash, _appHashLength);
        WriteKeys[(int)EncryptionLevel.Application]?.Dispose();
        WriteKeys[(int)EncryptionLevel.Application] = new PacketProtection(_appWriteTk, _appAead);
        _sendKeyPhase = !_sendKeyPhase;
        _keyUpdateCount++;
        Qlog?.KeyUpdated(QlogTimeMs, IsServer ? "server_1rtt_secret" : "client_1rtt_secret",
                         "local_update", (ulong)_keyUpdateCount);
        return true;
    }

    /// <summary>
    /// Commits the key update detected via a flipped key-phase bit on the receive side: the prepared
    /// next read keys become active (the previous ones kept briefly for reordering), the generation
    /// after next is prepared, and – if not already done – the send keys rotate along as well
    /// (answering the peer's update).
    /// </summary>
    private void CommitPeerKeyUpdate(bool newPhase)
    {
        int i = (int)EncryptionLevel.Application;
        _prevAppReadKeys?.Dispose();
        _prevAppReadKeys = ReadKeys[i];
        ReadKeys[i] = _nextAppReadKeys!;
        _appReadTk = _nextAppReadTk!;
        _recvKeyPhase = newPhase;

        _nextAppReadTk = _appReadTk.Next(_appHash, _appHashLength);
        _nextAppReadKeys = new PacketProtection(_nextAppReadTk, _appAead);

        if (_sendKeyPhase != newPhase)
        {
            _appWriteTk = _appWriteTk!.Next(_appHash, _appHashLength);
            WriteKeys[i]?.Dispose();
            WriteKeys[i] = new PacketProtection(_appWriteTk, _appAead);
            _sendKeyPhase = newPhase;
        }
        _keyUpdateCount++;
    }

    private void MaybeDecodePeerParameters()
    {
        if (PeerParams is not null || TlsHandshake?.PeerQuicTransportParameters is not { } bytes)
            return;
        if (!TransportParameters.TryDecode(bytes, out TransportParameters? p) || p is null)
        {
            // RFC 9000 §7.4: faulty/invalid transport parameters ⇒ TRANSPORT_PARAMETER_ERROR.
            CloseWithTransportError(TransportError.TransportParameterError, "invalid transport parameters");
            return;
        }
        if (ValidatePeerTransportParameters(p) is { } problem)
        {
            CloseWithTransportError(TransportError.TransportParameterError, problem);
            return;
        }

        PeerParams = p;
        _connSendLimit = p.InitialMaxDataValue;
        // RFC 9002 §6.2: the PTO includes the PEER's max_ack_delay — it is the delay the peer told
        // us it may add, so budgeting our own value here would make us probe too early.
        _recovery.MaxAckDelay = TimeSpan.FromMilliseconds(p.MaxAckDelayMs);
        _idle.Negotiate(LocalParams.MaxIdleTimeoutMs, p.MaxIdleTimeoutMs); // effective idle timeout (RFC 9000 §10.1)
        ApplyPeerStatelessResetToken();

        // The connection ID inside preferred_address has sequence number 1 (§18.2) and exists so the
        // client is guaranteed an unused active CID when it migrates. Feeding it through the same
        // path as a NEW_CONNECTION_ID frame keeps the active-CID limit and the retire logic in one
        // place instead of growing a second way to learn a remote CID.
        if (!IsServer && p.PreferredAddressValue is { } preferred)
            _cids.OnNewConnectionId(new NewConnectionIdFrame(1, 0, preferred.ConnectionId.ToArray(),
                                                             preferred.StatelessResetToken),
                                    out _, out _);

        foreach (QuicStream s in StreamMap.Values)
            if (s.Send.MaxData == 0)
                s.Send.MaxData = PeerSendLimitFor(s.Id);
    }

    /// <summary>
    /// Authenticates the peer transport parameters (RFC 9000 §7.3): initial_source_connection_id MUST
    /// be present and match the peer's source connection ID from its Initial packet (which at this
    /// point is our <see cref="Dcid"/>) — this cryptographically binds the parameters negotiated in
    /// the handshake to the connection IDs transmitted in the clear. Role-specific checks
    /// (ODCID/Retry on the client, server-only parameters on the server) are added by the subclasses.
    /// Return: an error description or <c>null</c> when everything matches.
    /// </summary>
    internal virtual string? ValidatePeerTransportParameters(TransportParameters p)
    {
        if (!p.SawInitialSourceConnectionId)
            return "missing initial_source_connection_id"; // §7.3: absence is a connection error
        if (!p.InitialSourceConnectionIdValue.Span.SequenceEqual(Dcid.Span))
            return "initial_source_connection_id mismatch";
        return null;
    }

    public virtual void Dispose()
    {
        if (_connectionCounted)
        {
            _connectionCounted = false;
            QuicMetrics.ActiveConnections.Add(-1, QuicMetrics.RoleTag(IsServer));
        }

        TlsHandshake?.Dispose();
        foreach (PacketProtection? k in WriteKeys)
            k?.Dispose();
        foreach (PacketProtection? k in ReadKeys)
            k?.Dispose();
        _nextAppReadKeys?.Dispose();
        _prevAppReadKeys?.Dispose();
        // Release the 0-RTT keys too: if the connection ends before they were discarded regularly
        // (server before the 3×PTO deadline or before gap-free receipt, RFC 9001 §4.9.3), they would
        // otherwise sit undisposed until the GC. Nulling keeps the state consistent with the other
        // discard paths (idempotent).
        _zeroRttKeys?.Dispose();
        _zeroRttKeys = null;
        GC.SuppressFinalize(this);
    }
}
