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
using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Messages;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

/// <summary>
/// Drives the client side of the TLS 1.3 handshake for QUIC (RFC 8446 + RFC 9001). Offers key shares
/// for multiple groups (default: X25519 + P-256) and handles HelloRetryRequest (RFC 8446 §4.1.4).
/// Interface to the QUIC layer as established (CRYPTO in/out, keys appear as properties).
/// <para>Validates the server certificate: the CertificateVerify signature always, chain/hostname per
/// <see cref="CertificateValidationOptions"/>; likewise the server Finished MAC.</para>
/// </summary>
public sealed class TlsClientHandshake : ITlsHandshake
{
    private enum State { New, WaitServerHello, WaitServerFinished, Complete }

    private readonly string _serverName;
    private readonly byte[] _quicTransportParameters;
    private readonly IReadOnlyList<NamedGroup> _keyShareGroups;
    private readonly IReadOnlyList<NamedGroup> _supportedGroups;
    private readonly IReadOnlyList<CipherSuite> _cipherSuites;
    private readonly CertificateValidationOptions _validation;
    private readonly Dictionary<NamedGroup, IKeyExchange> _keyExchanges = [];
    private readonly Queue<(EncryptionLevel Level, byte[] Data)> _outgoing = new();
    private readonly Dictionary<EncryptionLevel, List<byte>> _recvBuffers = new();

    private State _state = State.New;
    private byte[] _clientHello1 = [];
    private bool _hrrHandled;
    private KeySchedule? _ks;
    private Transcript? _transcript;
    private List<byte[]>? _serverCertChain;

    // Session resumption (RFC 8446 §2.2): the offered ticket, the binder key derived from it,
    // this connection's resumption_master_secret and the new tickets received from the server.
    private readonly ResumptionTicket? _resumptionTicket;
    private byte[]? _binderKey;
    private bool _pskAccepted;
    private byte[]? _resumptionMasterSecret;
    private byte[]? _exporterMasterSecret; // exporter_master_secret (RFC 8446 §7.1) for §7.5 exports
    private readonly List<ResumptionTicket> _newSessionTickets = [];

    // 0-RTT (RFC 8446 §2.3): offered/derived early traffic secret + whether the server accepted it.
    private bool _earlyDataOffered;
    private byte[]? _earlyTrafficSecret;

    // Wall clock for the obfuscated ticket age (RFC 8446 §4.2.11); injectable for tests.
    private readonly TimeProvider _timeProvider;

    // Optional key log (NSS format) for Wireshark; null = off. See KeyLog for the security note.
    private readonly KeyLog? _keyLog;

    // Client authentication (RFC 8446 §4.3.2): our own credential, used only if the server asks.
    // Null means we answer any CertificateRequest with an empty Certificate.
    private readonly ServerCertificate? _clientCertificate;
    private bool _certificateRequested;
    private byte[] _certificateRequestContext = [];

    /// <summary>
    /// <c>true</c> when the server asked us to authenticate (RFC 8446 §4.3.2).
    /// </summary>
    public bool ClientCertificateRequested => _certificateRequested;

    /// <summary>
    /// <c>true</c> when we actually sent a certificate — false if none was configured, in which case
    /// an empty Certificate went out instead and the server decides what to do about it.
    /// </summary>
    public bool ClientCertificateSent { get; private set; }

    /// <summary>
    /// The random of the ClientHello most recently built — the connection identifier of the key log.
    /// After a HelloRetryRequest that is the random of ClientHello2, i.e. the one on the wire.
    /// </summary>
    private ReadOnlySpan<byte> ClientRandom => ClientHelloParser.ClientRandom(_currentClientHello);

    private byte[] _currentClientHello = [];

    private void LogHandshakeSecrets()
    {
        if (_keyLog is null || HandshakeSecrets is null)
            return;
        _keyLog.Write(KeyLog.ClientHandshakeTrafficSecret, ClientRandom, HandshakeSecrets.ClientHandshakeTrafficSecret);
        _keyLog.Write(KeyLog.ServerHandshakeTrafficSecret, ClientRandom, HandshakeSecrets.ServerHandshakeTrafficSecret);
    }

    private void LogApplicationSecrets()
    {
        if (_keyLog is null || ApplicationSecrets is null)
            return;
        _keyLog.Write(KeyLog.ClientTrafficSecret0, ClientRandom, ApplicationSecrets.ClientApplicationTrafficSecret);
        _keyLog.Write(KeyLog.ServerTrafficSecret0, ClientRandom, ApplicationSecrets.ServerApplicationTrafficSecret);
        if (_exporterMasterSecret is { } exporter)
            _keyLog.Write(KeyLog.ExporterSecret, ClientRandom, exporter);
    }

    public TlsClientHandshake(
        string serverName,
        byte[] quicTransportParameters,
        IReadOnlyList<NamedGroup>? keyShareGroups = null,
        IReadOnlyList<NamedGroup>? supportedGroups = null,
        CertificateValidationOptions? certificateValidation = null,
        IReadOnlyList<CipherSuite>? cipherSuites = null,
        ResumptionTicket? resumptionTicket = null,
        TimeProvider? timeProvider = null,
        KeyLog? keyLog = null,
        ServerCertificate? clientCertificate = null)
    {
        _clientCertificate = clientCertificate;
        _keyLog = keyLog;
        _serverName = serverName;
        _quicTransportParameters = quicTransportParameters;
        _keyShareGroups = keyShareGroups ?? KeyExchange.DefaultGroups;
        _supportedGroups = supportedGroups ?? [NamedGroup.X25519, NamedGroup.Secp256r1, NamedGroup.Secp384r1];
        _cipherSuites = cipherSuites ?? [CipherSuite.Aes128GcmSha256, CipherSuite.Aes256GcmSha384];
        _validation = certificateValidation ?? CertificateValidationOptions.Default;
        _resumptionTicket = resumptionTicket;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// <c>true</c> when the server accepted our PSK offer (handshake via resumption instead of certificate).
    /// </summary>
    public bool ResumptionAccepted => _pskAccepted;

    /// <summary>
    /// The session tickets issued by the server after the handshake (RFC 8446 §4.6.1) for later resumption.
    /// </summary>
    public IReadOnlyList<ResumptionTicket> NewSessionTickets => _newSessionTickets;

    /// <summary>
    /// Diagnostics: number of received NewSessionTicket messages (including those unusable as tickets).
    /// </summary>
    public int NewSessionTicketMessagesSeen { get; private set; }

    public byte[]? EarlyTrafficSecret => _earlyTrafficSecret;
    public CipherSuite? EarlyDataCipherSuite => _earlyDataOffered ? _resumptionTicket?.CipherSuite : null;
    public bool EarlyDataAccepted { get; private set; }

    public CipherSuite? NegotiatedCipherSuite { get; private set; }
    public HandshakeTrafficSecrets? HandshakeSecrets { get; private set; }
    public ApplicationTrafficSecrets? ApplicationSecrets { get; private set; }

    /// <summary>
    /// TLS keying-material exporter (RFC 8446 §7.5) based on the <c>exporter_master_secret</c>;
    /// available once the application secrets are derived (after the server Finished).
    /// </summary>
    public byte[] ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length)
        => _exporterMasterSecret is { } secret && _ks is { } ks
            ? ks.ExportKeyingMaterial(secret, label, context, length)
            : throw new InvalidOperationException("Keying-material export only possible after the server Finished (RFC 8446 §7.5).");
    public bool ServerFinishedValid { get; private set; }
    public bool IsComplete => _state == State.Complete;
    public byte[]? PeerQuicTransportParameters { get; private set; }

    /// <summary>
    /// The server's validated leaf certificate (only available after CertificateVerify).
    /// </summary>
    public X509Certificate2? ServerCertificate { get; private set; }

    /// <summary>
    /// <c>true</c> once the server certificate incl. the CertificateVerify signature has been validated.
    /// </summary>
    public bool ServerCertificateValid { get; private set; }

    /// <summary>
    /// The group with which the handshake was ultimately completed (after a possible HRR).
    /// </summary>
    public NamedGroup? NegotiatedGroup { get; private set; }

    /// <summary>
    /// Starts the handshake: generates key shares and builds the (first) ClientHello.
    /// </summary>
    public void Start()
    {
        foreach (NamedGroup group in _keyShareGroups)
            _keyExchanges[group] = KeyExchange.Create(group);

        // Resumption: prepare the key schedule (bound to the ticket suite) and the binder key so the
        // ClientHello can carry the PSK binder.
        if (_resumptionTicket is { } ticket)
        {
            _ks = new KeySchedule(ticket.CipherSuite);
            _binderKey = _ks.ResumptionBinderKey(ticket.Psk);
            _earlyDataOffered = ticket.AllowsEarlyData; // if the ticket allows 0-RTT, we offer it
        }

        _clientHello1 = BuildClientHello(_keyShareGroups);
        _outgoing.Enqueue((EncryptionLevel.Initial, _clientHello1));

        // 0-RTT: derive the early traffic secret over the hash of the (complete) ClientHello – from it
        // the QUIC layer installs the 0-RTT write keys in order to send application data immediately.
        if (_earlyDataOffered && _resumptionTicket is { } t && _ks is not null)
        {
            _earlyTrafficSecret = _ks.ClientEarlyTrafficSecret(t.Psk, _ks.TranscriptHash(_clientHello1));
            _keyLog?.Write(KeyLog.ClientEarlyTrafficSecret, ClientRandom, _earlyTrafficSecret);
        }

        _state = State.WaitServerHello;
    }

    /// <summary>
    /// After a QUIC Retry, sends the same ClientHello again (unchanged content, RFC 9000 §17.2.5).
    /// The transcript hash still only starts at the ServerHello, so no rebuild is needed.
    /// </summary>
    public void ResendClientHello()
    {
        if (_clientHello1.Length > 0)
            _outgoing.Enqueue((EncryptionLevel.Initial, _clientHello1));
    }

    public bool TryGetOutgoingCrypto(out EncryptionLevel level, out byte[] data)
    {
        if (_outgoing.Count > 0)
        {
            (level, data) = _outgoing.Dequeue();
            return true;
        }
        level = default;
        data = [];
        return false;
    }

    public void ProvideCrypto(EncryptionLevel level, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;
        if (!_recvBuffers.TryGetValue(level, out List<byte>? buffer))
            _recvBuffers[level] = buffer = [];
        buffer.AddRange(data);

        if (!HandshakeMessages.TryReadAll(buffer.ToArray(), out List<HandshakeMessage> messages, out int consumed))
            return;
        foreach (HandshakeMessage message in messages)
            ProcessMessage(message);
        buffer.RemoveRange(0, consumed);
    }

    private void ProcessMessage(HandshakeMessage message)
    {
        switch (message.Type)
        {
            case HandshakeType.ServerHello when _state == State.WaitServerHello:
                ProcessServerHello(message);
                break;
            case HandshakeType.Finished when _state == State.WaitServerFinished:
                VerifyServerFinished(message);
                _transcript!.Append(message.Full.Span);
                GenerateClientFinishedAndAppKeys();
                break;
            case HandshakeType.EncryptedExtensions:
                ExtractTransportParameters(message.Body.Span);
                _transcript?.Append(message.Full.Span);
                break;
            case HandshakeType.Certificate:
                ProcessCertificate(message);
                break;
            case HandshakeType.CertificateVerify:
                ProcessCertificateVerify(message);
                break;
            case HandshakeType.CertificateRequest:
                ProcessCertificateRequest(message);
                break;
            case HandshakeType.NewSessionTicket:
                // Post-handshake message: do NOT append to the handshake transcript.
                NewSessionTicketMessagesSeen++;
                ProcessNewSessionTicket(message);
                break;
            default:
                _transcript?.Append(message.Full.Span);
                break;
        }
    }

    /// <summary>
    /// The server asks us to authenticate (RFC 8446 §4.3.2). We only record it here — the answer
    /// (Certificate + CertificateVerify) belongs at the END of the client flight, after the server's
    /// Finished, per the §4.4 handshake-context table.
    /// </summary>
    private void ProcessCertificateRequest(HandshakeMessage message)
    {
        // RFC 9001 §4.4: "servers MUST NOT send post-handshake TLS CertificateRequest messages, and
        // clients MUST treat receipt of such messages as a connection error of type
        // PROTOCOL_VIOLATION." QUIC's multiplexing means the client could not correlate the request
        // with whatever triggered it, so the mechanism is banned outright rather than merely unused.
        if (_state == State.Complete)
            throw new PostHandshakeAuthenticationException(
                "Server sent a post-handshake CertificateRequest, which RFC 9001 §4.4 forbids.");

        if (!CertificateRequestMessage.TryParse(message.Body.Span, out byte[] context, out _))
            throw new InvalidOperationException("Invalid CertificateRequest message.");

        _certificateRequested = true;
        _certificateRequestContext = context; // echoed back in our Certificate (§4.4.2)
        _transcript?.Append(message.Full.Span);
    }

    private void ProcessCertificate(HandshakeMessage message)
    {
        if (!CertificateMessage.TryParse(message.Body.Span, out List<byte[]> chain))
            throw new InvalidOperationException("Invalid Certificate message.");
        _serverCertChain = chain;
        _transcript?.Append(message.Full.Span);
    }

    private void ProcessCertificateVerify(HandshakeMessage message)
    {
        if (_serverCertChain is null)
            throw new InvalidOperationException("CertificateVerify without a preceding Certificate.");
        if (!CertificateVerify.TryParse(message.Body.Span, out SignatureScheme scheme, out byte[] signature))
            throw new InvalidOperationException("Invalid CertificateVerify message.");

        // The signed transcript hash extends up to and including Certificate – i.e. BEFORE appending this message.
        byte[] transcriptHash = _transcript!.CurrentHash();
        ServerCertificate = PeerCertificateValidator.Validate(
            _serverCertChain, scheme, signature, transcriptHash, _serverName, _validation);
        ServerCertificateValid = true;

        _transcript.Append(message.Full.Span);
    }

    private void ProcessServerHello(HandshakeMessage message)
    {
        if (!ServerHello.TryParse(message.Full.Span, out ServerHelloInfo? sh) || sh is null)
            throw new InvalidOperationException("Invalid ServerHello.");

        NegotiatedCipherSuite = sh.CipherSuite;

        if (sh.IsHelloRetryRequest)
        {
            HandleHelloRetryRequest(message, sh);
            return;
        }

        if (sh.KeyShareGroup is not { } group || sh.KeySharePublicKey is null)
            throw new InvalidOperationException("ServerHello without a key share.");
        if (!_keyExchanges.TryGetValue(group, out IKeyExchange? kex))
            throw new InvalidOperationException($"Server chose unoffered group {group}.");

        // Did the server accept our PSK offer? Then the handshake runs via resumption (no certificate).
        _pskAccepted = _resumptionTicket is not null && sh.SelectedPskIdentity == 0;

        // Create the transcript at the first (non-HRR-triggered) ServerHello. With resumption, _ks is
        // already created with the ticket suite (for the binder) and is kept.
        if (_transcript is null)
        {
            _ks ??= new KeySchedule(sh.CipherSuite);
            _transcript = new Transcript(_ks.Hash);
            _transcript.Append(_clientHello1);
        }
        _transcript.Append(message.Full.Span);

        NegotiatedGroup = group;
        byte[] shared = kex.DeriveSharedSecret(sh.KeySharePublicKey);
        // With accepted resumption, the PSK flows into the early secret (RFC 8446 §7.1).
        ReadOnlySpan<byte> psk = _pskAccepted ? _resumptionTicket!.Psk : default;
        HandshakeSecrets = _ks!.DeriveHandshakeSecrets(shared, _transcript.CurrentHash(), psk);
        LogHandshakeSecrets();
        _state = State.WaitServerFinished;
    }

    private void HandleHelloRetryRequest(HandshakeMessage hrr, ServerHelloInfo sh)
    {
        if (_hrrHandled)
            throw new InvalidOperationException("A second HelloRetryRequest is not permitted.");
        _hrrHandled = true;

        if (sh.KeyShareGroup is not { } group)
            throw new InvalidOperationException("HRR without a requested group.");
        if (!_supportedGroups.Contains(group) || !KeyExchange.IsSupported(group))
            throw new InvalidOperationException($"HRR requests unsupported group {group}.");

        _ks = new KeySchedule(sh.CipherSuite);
        _transcript = new Transcript(_ks.Hash);

        // RFC 8446 §4.4.1: ClientHello1 is replaced by the synthetic message_hash message.
        _transcript.Append(SyntheticMessageHash(_ks.TranscriptHash(_clientHello1), _ks.HashLength));
        _transcript.Append(hrr.Full.Span);

        if (!_keyExchanges.ContainsKey(group))
            _keyExchanges[group] = KeyExchange.Create(group);

        byte[] clientHello2 = BuildClientHello([group]);
        _transcript.Append(clientHello2);
        _outgoing.Enqueue((EncryptionLevel.Initial, clientHello2));
        _state = State.WaitServerHello;
    }

    private byte[] BuildClientHello(IReadOnlyList<NamedGroup> keyShareGroups)
    {
        var shares = new List<KeyShareEntry>(keyShareGroups.Count);
        foreach (NamedGroup group in keyShareGroups)
            shares.Add(new KeyShareEntry(group, _keyExchanges[group].PublicKey));

        PskIdentity? pskIdentity = null;
        int binderLength = 0;
        Func<ReadOnlyMemory<byte>, byte[]>? computeBinder = null;
        if (_resumptionTicket is { } ticket && _ks is { } ks && _binderKey is { } binderKey)
        {
            pskIdentity = new PskIdentity(ticket.Identity, ticket.ObfuscatedTicketAge(_timeProvider.GetUtcNow()));
            binderLength = ks.HashLength;
            // Binder = HMAC(finished_key(binder_key), transcript hash(truncated ClientHello)).
            computeBinder = truncated => ks.FinishedVerifyData(binderKey, ks.TranscriptHash(truncated.Span));
        }

        _currentClientHello = ClientHello.Build(new ClientHelloOptions
        {
            ServerName = _serverName,
            CipherSuites = _cipherSuites,
            SupportedGroups = _supportedGroups,
            KeyShares = shares,
            QuicTransportParameters = _quicTransportParameters,
            PskIdentity = pskIdentity,
            PskBinderLength = binderLength,
            ComputeBinder = computeBinder,
            OfferEarlyData = _earlyDataOffered,
        });
        return _currentClientHello;
    }

    /// <summary>
    /// The synthetic message_hash message (RFC 8446 §4.4.1): type 0xFE ‖ 3-byte length ‖ hash.
    /// </summary>
    private static byte[] SyntheticMessageHash(byte[] hash, int hashLength)
    {
        byte[] message = new byte[4 + hashLength];
        message[0] = 0xFE; // message_hash
        message[3] = (byte)hashLength;
        hash.CopyTo(message, 4);
        return message;
    }

    private void ExtractTransportParameters(ReadOnlySpan<byte> encryptedExtensionsBody)
    {
        var reader = new BufferReader(encryptedExtensionsBody);
        if (!reader.TryReadUInt16(out ushort extensionsLength) || extensionsLength > reader.Remaining)
            return;
        while (reader.Remaining >= 4)
        {
            if (!reader.TryReadUInt16(out ushort type) ||
                !reader.TryReadUInt16(out ushort length) ||
                !reader.TryReadBytes(length, out ReadOnlySpan<byte> data))
                return;
            if (type == (ushort)ExtensionType.QuicTransportParameters)
                PeerQuicTransportParameters = data.ToArray();
            else if (type == (ushort)ExtensionType.EarlyData)
                EarlyDataAccepted = true; // server confirms 0-RTT (RFC 8446 §4.2.10)
        }
    }

    private void VerifyServerFinished(HandshakeMessage finished)
    {
        byte[] expected = _ks!.FinishedVerifyData(HandshakeSecrets!.ServerHandshakeTrafficSecret, _transcript!.CurrentHash());
        ServerFinishedValid = CryptographicOperations.FixedTimeEquals(expected, finished.Body.Span);
    }

    private void GenerateClientFinishedAndAppKeys()
    {
        byte[] transcriptThroughServerFinished = _transcript!.CurrentHash();
        // The application secrets are anchored at the server Finished (RFC 8446 §7.1) and do NOT move
        // when client authentication adds messages after it.
        ApplicationSecrets = _ks!.DeriveApplicationSecrets(HandshakeSecrets!.HandshakeSecret, transcriptThroughServerFinished);
        // exporter_master_secret (RFC 8446 §7.1) over CH…server Finished — for §7.5 keying-material exports.
        _exporterMasterSecret = _ks.ExporterMasterSecret(ApplicationSecrets.MasterSecret, transcriptThroughServerFinished);
        LogApplicationSecrets();

        if (_certificateRequested)
            SendClientAuthentication();

        // §4.4: Finished is a MAC over Transcript-Hash(Handshake Context, Certificate,
        // CertificateVerify) — the current hash, which the two messages above have just extended.
        byte[] verifyData = _ks.FinishedVerifyData(HandshakeSecrets.ClientHandshakeTrafficSecret, _transcript.CurrentHash());
        byte[] clientFinished = Finished.BuildMessage(verifyData);
        _outgoing.Enqueue((EncryptionLevel.Handshake, clientFinished));
        _transcript.Append(clientFinished);

        // resumption_master_secret (RFC 8446 §7.1) over CH…client Finished – the basis of the
        // resumption PSKs issued later via NewSessionTicket.
        _resumptionMasterSecret = _ks.ResumptionMasterSecret(
            ApplicationSecrets.MasterSecret, _transcript.CurrentHash());
        _state = State.Complete;
    }

    /// <summary>
    /// Answers a CertificateRequest (RFC 8446 §4.4.2/§4.4.3). Without a configured credential the
    /// answer is an EMPTY Certificate — the legal way to decline (§4.4.2.4) — and no CertificateVerify
    /// follows it, since there is no key to prove possession of. The server then decides whether to
    /// continue.
    /// </summary>
    private void SendClientAuthentication()
    {
        byte[] certificate = CertificateMessage.Build(
            _clientCertificate?.Der ?? ReadOnlySpan<byte>.Empty, _certificateRequestContext);
        _outgoing.Enqueue((EncryptionLevel.Handshake, certificate));
        _transcript!.Append(certificate);

        if (_clientCertificate is not { } credential)
            return;

        // Signed over the transcript INCLUDING our Certificate, with the client context string —
        // "TLS 1.3, client CertificateVerify" is what keeps this signature from being replayable as
        // a server's (§4.4.3).
        byte[] content = CertificateVerify.BuildSignatureContent(
            CertificateVerify.ClientContext, _transcript.CurrentHash());
        byte[] signature = credential.SignCertificateVerify(content);

        var w = new BufferWriter(signature.Length + 16);
        try
        {
            w.WriteByte((byte)HandshakeType.CertificateVerify);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);
            w.WriteUInt16((ushort)credential.SignatureScheme);
            int sigLen = TlsWriter.BeginVector(ref w, 2);
            w.WriteBytes(signature);
            TlsWriter.EndVector(ref w, sigLen, 2);
            TlsWriter.EndVector(ref w, bodyLen, 3);

            byte[] certificateVerify = w.WrittenSpan.ToArray();
            _outgoing.Enqueue((EncryptionLevel.Handshake, certificateVerify));
            _transcript.Append(certificateVerify);
        }
        finally { w.Dispose(); }

        ClientCertificateSent = true;
    }

    private void ProcessNewSessionTicket(HandshakeMessage message)
    {
        if (_resumptionMasterSecret is null || _ks is null || NegotiatedCipherSuite is not { } suite)
            return; // invalid before handshake completion
        if (!Messages.NewSessionTicket.TryParse(message.Body.Span, out NewSessionTicketInfo? info) || info is null)
            return;

        byte[] psk = _ks.ResumptionPsk(_resumptionMasterSecret, info.Nonce);
        _newSessionTickets.Add(new ResumptionTicket(
            psk, info.Ticket, info.AgeAdd, suite, _serverName,
            info.LifetimeSeconds, info.MaxEarlyDataSize, PeerQuicTransportParameters ?? [],
            receivedAt: _timeProvider.GetUtcNow()));
    }

    public void Dispose()
    {
        foreach (IKeyExchange kex in _keyExchanges.Values)
            kex.Dispose();
        _transcript?.Dispose();
        ServerCertificate?.Dispose();
    }
}
