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
/// The server side of the TLS 1.3 handshake for QUIC (RFC 8446 + RFC 9001). Chooses a group from the
/// client's key shares (default preference X25519, then P-256) and sends a HelloRetryRequest when the
/// client offers no matching key share but a matching supported_group. Produces
/// ServerHello → EncryptedExtensions → Certificate → CertificateVerify (signed) → Finished and
/// verifies the client Finished.
/// </summary>
public sealed class TlsServerHandshake : ITlsHandshake
{
    private enum State { New, WaitClientHello2, WaitClientFinished, Complete }

    private readonly ServerCertificate _certificate;
    private readonly byte[] _quicTransportParameters;
    private readonly IReadOnlyList<NamedGroup> _preferredGroups;
    private readonly Queue<(EncryptionLevel Level, byte[] Data)> _outgoing = new();
    private readonly Dictionary<EncryptionLevel, List<byte>> _recvBuffers = new();

    private State _state = State.New;
    private KeySchedule? _ks;
    private Transcript? _transcript;
    private byte[] _clientHello1 = [];
    private NamedGroup _requestedGroup;
    private byte[] _transcriptThroughServerFinished = [];
    private byte[]? _exporterMasterSecret; // exporter_master_secret (RFC 8446 §7.1) for §7.5 exports

    private readonly IReadOnlyList<CipherSuite> _preferredCipherSuites;
    private CipherSuite _cipherSuite = CipherSuite.Aes128GcmSha256;

    // Session resumption (RFC 8446 §2.2): optional ticket store. When set, the server accepts
    // valid PSK offers and issues new tickets after the handshake.
    private readonly ServerResumptionCache? _resumptionCache;
    private readonly uint _ticketLifetimeSeconds;
    private readonly uint _maxEarlyDataSize;
    private bool _pskAccepted;
    private byte[] _selectedPsk = [];
    private bool _newTicketIssued;

    // 0-RTT (RFC 8446 §2.3): accepted early_data + the early secret needed to read the 0-RTT packets.
    private byte[]? _earlyTrafficSecret;

    // Optional key log (NSS format) for Wireshark; null = off. See KeyLog for the security note.
    private readonly KeyLog? _keyLog;

    // Client authentication (RFC 8446 §4.3.2). Null = never ask, which stays the default.
    private readonly ClientCertificateOptions? _clientCertificate;
    private bool _certificateRequestSent;
    private List<byte[]>? _clientCertChain;
    private string? _clientCertFailure;

    /// <summary>
    /// The validated client certificate (mutual TLS), or <c>null</c> if none was asked for, none was
    /// presented, or validation failed under <see cref="ClientCertificateMode.Request"/>.
    /// </summary>
    public X509Certificate2? ClientCertificate { get; private set; }

    /// <summary>
    /// The outcome of client authentication, for the application above.
    /// </summary>
    public ClientAuthenticationResult ClientAuthentication => _certificateRequestSent
        ? new ClientAuthenticationResult(true, ClientCertificate, _clientCertFailure)
        : ClientAuthenticationResult.NotRequested;

    /// <summary>
    /// The random of the ClientHello the handshake is running on — the connection identifier of the
    /// key log. After a HelloRetryRequest that is the random of ClientHello2.
    /// </summary>
    private ReadOnlySpan<byte> ClientRandom => ClientHelloParser.ClientRandom(_clientHello1);

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

    public TlsServerHandshake(
        ServerCertificate certificate,
        byte[] quicTransportParameters,
        IReadOnlyList<NamedGroup>? preferredGroups = null,
        IReadOnlyList<CipherSuite>? preferredCipherSuites = null,
        ServerResumptionCache? resumptionCache = null,
        uint ticketLifetimeSeconds = 7200,
        uint maxEarlyDataSize = 0,
        KeyLog? keyLog = null,
        ClientCertificateOptions? clientCertificate = null)
    {
        _keyLog = keyLog;
        _certificate = certificate;
        _clientCertificate = clientCertificate is { Mode: not ClientCertificateMode.None } ? clientCertificate : null;
        _quicTransportParameters = quicTransportParameters;
        _preferredGroups = preferredGroups ?? [NamedGroup.X25519, NamedGroup.Secp256r1];
        // Preference: AES-128, then ChaCha20, then AES-256; the server accepts all three.
        _preferredCipherSuites = preferredCipherSuites ??
            [CipherSuite.Aes128GcmSha256, CipherSuite.ChaCha20Poly1305Sha256, CipherSuite.Aes256GcmSha384];
        _resumptionCache = resumptionCache;
        _ticketLifetimeSeconds = ticketLifetimeSeconds;
        _maxEarlyDataSize = maxEarlyDataSize;
    }

    /// <summary>
    /// <c>true</c> when a client PSK offer was accepted and the handshake ran via resumption.
    /// </summary>
    public bool ResumptionAccepted => _pskAccepted;

    public byte[]? EarlyTrafficSecret => _earlyTrafficSecret;
    public CipherSuite? EarlyDataCipherSuite => _earlyTrafficSecret is not null ? _cipherSuite : null;
    public bool EarlyDataAccepted { get; private set; }

    public CipherSuite? NegotiatedCipherSuite { get; private set; }

    /// <summary>
    /// The key-exchange group the server picked from the client's key shares. Set with the server
    /// flight, i.e. before <see cref="NegotiatedCipherSuite"/> — and after a HelloRetryRequest it is
    /// the group of the SECOND ClientHello, which is the one the secret actually comes from.
    /// </summary>
    public NamedGroup? NegotiatedGroup { get; private set; }

    public HandshakeTrafficSecrets? HandshakeSecrets { get; private set; }
    public ApplicationTrafficSecrets? ApplicationSecrets { get; private set; }
    public byte[]? PeerQuicTransportParameters { get; private set; }
    public bool ClientFinishedValid { get; private set; }
    public bool IsComplete => _state == State.Complete;

    /// <summary>
    /// TLS keying-material exporter (RFC 8446 §7.5) based on the <c>exporter_master_secret</c>;
    /// available once the application secrets are derived (with the server Finished).
    /// </summary>
    public byte[] ExportKeyingMaterial(string label, ReadOnlySpan<byte> context, int length)
        => _exporterMasterSecret is { } secret && _ks is { } ks
            ? ks.ExportKeyingMaterial(secret, label, context, length)
            : throw new InvalidOperationException("Keying-material export only possible after the server Finished (RFC 8446 §7.5).");

    /// <summary>
    /// Whether a HelloRetryRequest was sent (diagnostics/test).
    /// </summary>
    public bool SentHelloRetryRequest { get; private set; }

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
            case HandshakeType.ClientHello when _state == State.New:
                ProcessClientHello(message, isSecond: false);
                break;
            case HandshakeType.ClientHello when _state == State.WaitClientHello2:
                ProcessClientHello(message, isSecond: true);
                break;
            // Client authentication (RFC 8446 §4.4): Certificate and CertificateVerify arrive in the
            // client's flight, before its Finished — and both go into the transcript that Finished
            // is computed over.
            case HandshakeType.Certificate when _state == State.WaitClientFinished:
                ProcessClientCertificate(message);
                break;
            case HandshakeType.CertificateVerify when _state == State.WaitClientFinished:
                ProcessClientCertificateVerify(message);
                break;
            case HandshakeType.Finished when _state == State.WaitClientFinished:
                RequireClientCertificateIfDemanded();
                VerifyClientFinished(message);
                _transcript!.Append(message.Full.Span); // through client Finished – the basis for res_master
                _state = State.Complete;
                if (ClientFinishedValid)
                    IssueNewSessionTicket();
                break;
        }
    }

    /// <summary>
    /// The client's Certificate. An empty one is legal — it is how a client declines (§4.4.2.4) —
    /// so the decision what to do about it is deferred to <see cref="RequireClientCertificateIfDemanded"/>,
    /// after the flight is complete.
    /// </summary>
    private void ProcessClientCertificate(HandshakeMessage message)
    {
        if (!_certificateRequestSent)
            throw new TlsHandshakeException(TlsAlert.UnexpectedMessage, "Client sent a Certificate without being asked.");
        if (!CertificateMessage.TryParseWithContext(message.Body.Span, out List<byte[]> chain, out byte[] context))
            throw new TlsHandshakeException(TlsAlert.DecodeError, "Invalid client Certificate message.");
        // §4.4.2: the context is echoed from our CertificateRequest, which is always empty here.
        if (context.Length != 0)
            throw new TlsHandshakeException(TlsAlert.DecodeError, "Client echoed an unknown certificate_request_context.");

        _clientCertChain = chain;
        // The hash BEFORE this message is not what CertificateVerify signs — that one covers the
        // Certificate too, so append first and take the hash when the CertificateVerify arrives.
        _transcript!.Append(message.Full.Span);
    }

    private void ProcessClientCertificateVerify(HandshakeMessage message)
    {
        if (_clientCertChain is null)
            throw new TlsHandshakeException(TlsAlert.UnexpectedMessage, "CertificateVerify without a preceding Certificate.");
        if (_clientCertChain.Count == 0)
            // §4.4.2.4: a client that sends an empty Certificate has nothing to prove possession of.
            throw new TlsHandshakeException(TlsAlert.UnexpectedMessage, "CertificateVerify after an empty Certificate.");
        if (!CertificateVerify.TryParse(message.Body.Span, out SignatureScheme scheme, out byte[] signature))
            throw new TlsHandshakeException(TlsAlert.DecodeError, "Invalid client CertificateVerify message.");

        try
        {
            // Signed over the transcript up to and including the client's Certificate, with the
            // CLIENT context string — that is what keeps a server signature from being replayed here.
            ClientCertificate = PeerCertificateValidator.ValidateClient(
                _clientCertChain, scheme, signature, _transcript!.CurrentHash(),
                _clientCertificate!.Validation);
        }
        catch (CertificateValidationException e)
        {
            // Under Request the application decides what an untrusted client is worth; under Require
            // the handshake ends here (see RequireClientCertificateIfDemanded).
            _clientCertFailure = e.Message;
        }

        _transcript!.Append(message.Full.Span);
    }

    /// <summary>
    /// Applies the policy once the client's authentication messages are in: under
    /// <see cref="ClientCertificateMode.Require"/> a missing or rejected certificate ends the
    /// handshake (§4.4.2.4 "abort the handshake with a certificate_required alert").
    /// </summary>
    private void RequireClientCertificateIfDemanded()
    {
        if (_clientCertificate is not { Mode: ClientCertificateMode.Require })
            return;
        if (ClientCertificate is not null)
            return;
        throw _clientCertFailure is null
            ? new TlsHandshakeException(TlsAlert.CertificateRequired,
                                        "Client presented no certificate although one is required.")
            : new TlsHandshakeException(TlsAlert.BadCertificate,
                                        $"Client certificate rejected: {_clientCertFailure}");
    }

    /// <summary>
    /// Issues a NewSessionTicket after the handshake (RFC 8446 §4.6.1), provided a ticket store is set:
    /// derives the resumption_master_secret, chooses nonce + PSK, stores them under a new identity in
    /// the store and enqueues the message at application level (post-handshake, 1-RTT).
    /// </summary>
    private void IssueNewSessionTicket()
    {
        if (_newTicketIssued || _resumptionCache is null || ApplicationSecrets is null || _ks is null)
            return;
        _newTicketIssued = true;

        byte[] resMaster = _ks.ResumptionMasterSecret(ApplicationSecrets.MasterSecret, _transcript!.CurrentHash());
        byte[] nonce = RandomNumberGenerator.GetBytes(8);
        byte[] psk = _ks.ResumptionPsk(resMaster, nonce);
        uint ageAdd = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
        // ageAdd and lifetime are stored with the ticket — the server needs both later for the
        // §8.3 freshness check and for expiry.
        byte[] identity = _resumptionCache.Issue(psk, _cipherSuite, _maxEarlyDataSize, _quicTransportParameters,
                                                 ageAdd, _ticketLifetimeSeconds);

        byte[] ticket = Messages.NewSessionTicket.Build(
            _ticketLifetimeSeconds, ageAdd, nonce, identity, _maxEarlyDataSize);
        _outgoing.Enqueue((EncryptionLevel.Application, ticket));
    }

    private void ProcessClientHello(HandshakeMessage message, bool isSecond)
    {
        if (!ClientHelloParser.TryParse(message.Full.Span, out ClientHelloInfo? ch) || ch is null)
            throw new InvalidOperationException("Invalid ClientHello.");

        // Negotiate the cipher suite: first preferred one the client offers (only at the first ClientHello).
        if (!isSecond)
        {
            if (!TrySelectCipherSuite(ch, out _cipherSuite))
                throw new InvalidOperationException("No common cipher suite (handshake_failure).");
            NegotiatedCipherSuite = _cipherSuite;
        }

        PeerQuicTransportParameters = ch.QuicTransportParameters;

        if (isSecond)
        {
            // Second ClientHello after HRR: the transcript already holds synthetic+HRR; now append CH2.
            if (!ch.KeyShares.TryGetValue((ushort)_requestedGroup, out byte[]? clientKeyShare2))
                throw new InvalidOperationException("ClientHello2 does not contain the requested group.");
            _transcript!.Append(message.Full.Span);
            EmitServerFlight(_requestedGroup, clientKeyShare2);
            return;
        }

        _clientHello1 = message.Full.ToArray();

        // 1) Is there a preferred group for which the client already sends a key share?
        if (TrySelectGroupWithKeyShare(ch, out NamedGroup group, out byte[]? clientKeyShare))
        {
            _ks = new KeySchedule(_cipherSuite);
            _transcript = new Transcript(_ks.Hash);
            _transcript.Append(_clientHello1);
            TryAcceptResumption(ch);
            EmitServerFlight(group, clientKeyShare!);
            return;
        }

        // 2) Otherwise: a preferred group the client lists in supported_groups → HelloRetryRequest.
        if (TrySelectGroupForHrr(ch, out NamedGroup hrrGroup))
        {
            SendHelloRetryRequest(hrrGroup);
            return;
        }

        throw new InvalidOperationException("No common named group (handshake_failure).");
    }

    private void SendHelloRetryRequest(NamedGroup group)
    {
        _requestedGroup = group;
        _ks = new KeySchedule(_cipherSuite);
        _transcript = new Transcript(_ks.Hash);

        // RFC 8446 §4.4.1: ClientHello1 → synthetic message_hash message.
        _transcript.Append(SyntheticMessageHash(_ks.TranscriptHash(_clientHello1), _ks.HashLength));

        byte[] hrr = ServerHello.BuildHelloRetryRequest(_cipherSuite, group);
        _transcript.Append(hrr);
        _outgoing.Enqueue((EncryptionLevel.Initial, hrr));
        SentHelloRetryRequest = true;
        _state = State.WaitClientHello2;
    }

    /// <summary>
    /// Checks a client PSK offer (RFC 8446 §4.2.11): resolves the ticket identity in the store and
    /// verifies the binder over the truncated ClientHello. On success, resumption is accepted.
    /// Precondition: _ks and _transcript are created and the ClientHello has been appended.
    /// </summary>
    private void TryAcceptResumption(ClientHelloInfo ch)
    {
        if (_resumptionCache is null || !ch.OffersPskDheKe || ch.OfferedPsks.Count == 0 || ch.PskBinderListOffset < 0)
            return;

        OfferedPsk offer = ch.OfferedPsks[0];
        // Look up WITHOUT consuming: the binder is unverified at this point, so consuming here would
        // let anyone who merely saw a ticket identity destroy the legitimate client's ticket.
        if (!_resumptionCache.TryResolve(offer.Identity, offer.ObfuscatedAge, out ResumptionLookup? found) ||
            found is null)
            return;

        // Binder = HMAC(finished_key(binder_key), transcript hash(ClientHello up to just before the binder list)).
        // A hash mismatch (wrong suite) automatically fails here on the length difference.
        byte[] psk = found.Psk;
        byte[] binderKey = _ks!.ResumptionBinderKey(psk);
        byte[] truncatedHash = _ks.TranscriptHash(_clientHello1.AsSpan(0, ch.PskBinderListOffset));
        byte[] expected = _ks.FinishedVerifyData(binderKey, truncatedHash);
        if (!CryptographicOperations.FixedTimeEquals(expected, offer.Binder))
            return;

        // Single-use ticket (RFC 8446 §8.1, MUST via RFC 9001 §9.2): only the FIRST user gets the
        // ticket. A replay — and a concurrent ClientHello carrying the same ticket — loses the race
        // here and falls back to a full handshake.
        if (!_resumptionCache.TryConsume(offer.Identity))
            return;

        _pskAccepted = true;
        _selectedPsk = psk;

        // Accept 0-RTT when the client offers early_data, we permit it (max_early_data_size > 0) AND
        // the ticket age claimed by the client is plausible (freshness check, RFC 8446 §8.3).
        // The QUIC layer derives the early secret (client_early_traffic_secret) to READ the 0-RTT packets.
        if (ch.OffersEarlyData && _maxEarlyDataSize > 0 && found.EarlyDataFresh)
        {
            EarlyDataAccepted = true;
            _earlyTrafficSecret = _ks.ClientEarlyTrafficSecret(psk, _ks.TranscriptHash(_clientHello1));
            _keyLog?.Write(KeyLog.ClientEarlyTrafficSecret, ClientRandom, _earlyTrafficSecret);
        }
    }

    /// <summary>
    /// Produces the server flight (ServerHello + handshake messages). The transcript extends up to before the ServerHello.
    /// </summary>
    private void EmitServerFlight(NamedGroup group, byte[] clientKeyShare)
    {
        NegotiatedGroup = group;
        using IKeyExchange kex = KeyExchange.Create(group);
        // Server side: produce response + secret from the client share. With (EC)DHE the response is our
        // own public key; with the KEM hybrid (X25519MLKEM768) the ciphertext — so it depends on the
        // client share and must be computed BEFORE the ServerHello.
        (byte[] responseShare, byte[] shared) = kex.Encapsulate(clientKeyShare);

        // With accepted resumption: selected_identity in the ServerHello, PSK into the early secret, NO certificate.
        ushort? selectedIdentity = _pskAccepted ? (ushort)0 : null;
        byte[] serverHello = ServerHello.Build(_cipherSuite, group, responseShare, selectedIdentity);
        _outgoing.Enqueue((EncryptionLevel.Initial, serverHello));
        _transcript!.Append(serverHello);

        ReadOnlySpan<byte> psk = _pskAccepted ? _selectedPsk : default;
        HandshakeSecrets = _ks!.DeriveHandshakeSecrets(shared, _transcript.CurrentHash(), psk);
        LogHandshakeSecrets();

        byte[] encryptedExtensions = BuildEncryptedExtensions();
        _transcript.Append(encryptedExtensions);
        _outgoing.Enqueue((EncryptionLevel.Handshake, encryptedExtensions));

        if (!_pskAccepted)
        {
            // Client authentication, if configured. §4.3.2: "This message, if sent, MUST follow
            // EncryptedExtensions" — and servers authenticating with a PSK MUST NOT send it in the
            // main handshake at all, which is why it lives inside this branch.
            if (_clientCertificate is { } clientAuth)
            {
                byte[] certificateRequest = CertificateRequestMessage.Build(clientAuth.SignatureSchemes);
                _transcript.Append(certificateRequest);
                _outgoing.Enqueue((EncryptionLevel.Handshake, certificateRequest));
                _certificateRequestSent = true;
            }

            // Full authentication only without resumption – with a PSK the binder vouches for the identity.
            byte[] certificate = BuildCertificate(_certificate.Der);
            _transcript.Append(certificate);
            _outgoing.Enqueue((EncryptionLevel.Handshake, certificate));

            byte[] certificateVerify = BuildCertificateVerify(_transcript.CurrentHash());
            _transcript.Append(certificateVerify);
            _outgoing.Enqueue((EncryptionLevel.Handshake, certificateVerify));
        }

        byte[] verifyData = _ks.FinishedVerifyData(HandshakeSecrets.ServerHandshakeTrafficSecret, _transcript.CurrentHash());
        byte[] finished = Finished.BuildMessage(verifyData);
        _transcript.Append(finished);
        _outgoing.Enqueue((EncryptionLevel.Handshake, finished));

        _transcriptThroughServerFinished = _transcript.CurrentHash();
        ApplicationSecrets = _ks.DeriveApplicationSecrets(HandshakeSecrets.HandshakeSecret, _transcriptThroughServerFinished);
        // exporter_master_secret (RFC 8446 §7.1) over CH…server Finished — for §7.5 keying-material exports.
        _exporterMasterSecret = _ks.ExporterMasterSecret(ApplicationSecrets.MasterSecret, _transcriptThroughServerFinished);
        LogApplicationSecrets();
        _state = State.WaitClientFinished;
    }

    /// <summary>
    /// Chooses the first preferred cipher suite the client offers.
    /// </summary>
    private bool TrySelectCipherSuite(ClientHelloInfo ch, out CipherSuite suite)
    {
        foreach (CipherSuite preferred in _preferredCipherSuites)
            if (ch.CipherSuites.Contains((ushort)preferred))
            {
                suite = preferred;
                return true;
            }
        suite = CipherSuite.Aes128GcmSha256;
        return false;
    }

    private bool TrySelectGroupWithKeyShare(ClientHelloInfo ch, out NamedGroup group, out byte[]? keyShare)
    {
        foreach (NamedGroup g in _preferredGroups)
        {
            if (KeyExchange.IsSupported(g) && ch.KeyShares.TryGetValue((ushort)g, out keyShare))
            {
                group = g;
                return true;
            }
        }
        group = default;
        keyShare = null;
        return false;
    }

    private bool TrySelectGroupForHrr(ClientHelloInfo ch, out NamedGroup group)
    {
        foreach (NamedGroup g in _preferredGroups)
        {
            if (KeyExchange.IsSupported(g) && ch.SupportedGroups.Contains((ushort)g))
            {
                group = g;
                return true;
            }
        }
        group = default;
        return false;
    }

    private void VerifyClientFinished(HandshakeMessage finished)
    {
        // RFC 8446 §4.4: the client's Finished is a MAC over Transcript-Hash(Handshake Context,
        // Certificate, CertificateVerify) — so the current transcript, NOT the snapshot taken at our
        // own Finished. Without client authentication nothing was appended since, and the two are
        // the same value; with it, only this one is right.
        byte[] expected = _ks!.FinishedVerifyData(HandshakeSecrets!.ClientHandshakeTrafficSecret, _transcript!.CurrentHash());
        ClientFinishedValid = CryptographicOperations.FixedTimeEquals(expected, finished.Body.Span);
    }

    private static byte[] SyntheticMessageHash(byte[] hash, int hashLength)
    {
        byte[] message = new byte[4 + hashLength];
        message[0] = 0xFE; // message_hash
        message[3] = (byte)hashLength;
        hash.CopyTo(message, 4);
        return message;
    }

    // ---- Message builders --------------------------------------------------------------------

    private byte[] BuildEncryptedExtensions()
    {
        var w = new BufferWriter(64);
        try
        {
            w.WriteByte((byte)HandshakeType.EncryptedExtensions);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);
            int extLen = TlsWriter.BeginVector(ref w, 2);

            w.WriteUInt16((ushort)ExtensionType.Alpn);
            int alpnExt = TlsWriter.BeginVector(ref w, 2);
            int protoList = TlsWriter.BeginVector(ref w, 2);
            w.WriteByte(2);
            w.WriteBytes("h3"u8);
            TlsWriter.EndVector(ref w, protoList, 2);
            TlsWriter.EndVector(ref w, alpnExt, 2);

            TlsWriter.WriteExtension(ref w, ExtensionType.QuicTransportParameters, _quicTransportParameters);

            // With accepted 0-RTT, confirm the (empty) early_data extension (RFC 8446 §4.2.10).
            if (EarlyDataAccepted)
                TlsWriter.WriteExtension(ref w, ExtensionType.EarlyData, ReadOnlySpan<byte>.Empty);

            TlsWriter.EndVector(ref w, extLen, 2);
            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    private static byte[] BuildCertificate(byte[] der)
    {
        var w = new BufferWriter(der.Length + 32);
        try
        {
            w.WriteByte((byte)HandshakeType.Certificate);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);
            w.WriteByte(0); // certificate_request_context: empty
            int listLen = TlsWriter.BeginVector(ref w, 3);
            {
                int certLen = TlsWriter.BeginVector(ref w, 3);
                w.WriteBytes(der);
                TlsWriter.EndVector(ref w, certLen, 3);
                w.WriteUInt16(0); // this entry's extensions: empty
            }
            TlsWriter.EndVector(ref w, listLen, 3);
            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    private byte[] BuildCertificateVerify(byte[] transcriptHash)
    {
        byte[] content = CertificateVerify.BuildSignatureContent(CertificateVerify.ServerContext, transcriptHash);
        byte[] signature = _certificate.SignCertificateVerify(content);

        var w = new BufferWriter(signature.Length + 16);
        try
        {
            w.WriteByte((byte)HandshakeType.CertificateVerify);
            int bodyLen = TlsWriter.BeginVector(ref w, 3);
            w.WriteUInt16((ushort)_certificate.SignatureScheme);
            w.WriteUInt16((ushort)signature.Length);
            w.WriteBytes(signature);
            TlsWriter.EndVector(ref w, bodyLen, 3);
            return w.WrittenSpan.ToArray();
        }
        finally { w.Dispose(); }
    }

    public void Dispose() => _transcript?.Dispose();
}
