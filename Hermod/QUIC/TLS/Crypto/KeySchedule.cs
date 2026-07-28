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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Crypto;

/// <summary>
/// The traffic secrets derived from the handshake secret (RFC 8446 §7.1).
/// </summary>
public sealed record HandshakeTrafficSecrets(
    byte[] HandshakeSecret,
    byte[] ClientHandshakeTrafficSecret,
    byte[] ServerHandshakeTrafficSecret);

/// <summary>
/// The application traffic secrets derived from the master secret (1-RTT).
/// </summary>
public sealed record ApplicationTrafficSecrets(
    byte[] MasterSecret,
    byte[] ClientApplicationTrafficSecret,
    byte[] ServerApplicationTrafficSecret);

/// <summary>
/// The TLS 1.3 key schedule (RFC 8446 §7.1). Chain:
/// <code>
///   Early Secret ─(Derive "derived")→ ─Extract(ECDHE)→ Handshake Secret ─(Derive "* hs traffic")
///   Handshake Secret ─(Derive "derived")→ ─Extract(0)→ Master Secret ─(Derive "* ap traffic")
/// </code>
/// All derivations go through <c>HKDF-Expand-Label</c> (see <see cref="TlsHkdf"/>).
/// QUIC uses the same traffic secrets but replaces the key/IV labels with "quic key"/"quic iv"
/// (handled by <c>TrafficKeys.FromSecret</c> in the QUIC layer).
/// </summary>
public sealed class KeySchedule
{
    private readonly HashAlgorithmName _hash;
    private readonly byte[] _emptyTranscriptHash;

    /// <summary>
    /// Length of the hash output (32 for SHA-256, 48 for SHA-384) – also the secret length.
    /// </summary>
    public int HashLength { get; }

    /// <summary>
    /// AEAD key length of the suite (16 for AES-128/ChaCha20, 32 for AES-256).
    /// </summary>
    public int AeadKeyLength { get; }

    public HashAlgorithmName Hash => _hash;

    public KeySchedule(CipherSuite suite)
    {
        (_hash, int hashLen, int aeadKeyLen) = suite switch
        {
            CipherSuite.Aes128GcmSha256 => (HashAlgorithmName.SHA256, 32, 16),
            CipherSuite.ChaCha20Poly1305Sha256 => (HashAlgorithmName.SHA256, 32, 32),
            CipherSuite.Aes256GcmSha384 => (HashAlgorithmName.SHA384, 48, 32),
            _ => throw new NotSupportedException($"Cipher suite {suite} is not supported."),
        };
        HashLength = hashLen;
        AeadKeyLength = aeadKeyLen;
        _emptyTranscriptHash = HashBytes(ReadOnlySpan<byte>.Empty);
    }

    /// <summary>
    /// Transcript hash over the (concatenated) handshake messages.
    /// </summary>
    public byte[] TranscriptHash(ReadOnlySpan<byte> handshakeMessages) => HashBytes(handshakeMessages);

    /// <summary>
    /// <c>Derive-Secret(Secret, Label, Messages)</c>
    /// = <c>HKDF-Expand-Label(Secret, Label, Transcript-Hash(Messages), Hash.length)</c>.
    /// </summary>
    public byte[] DeriveSecret(ReadOnlySpan<byte> secret, string label, ReadOnlySpan<byte> transcriptHash)
        => TlsHkdf.ExpandLabel(_hash, secret, label, transcriptHash, HashLength);

    /// <summary>
    /// Early secret = HKDF-Extract(salt = 0, IKM = PSK). Without a PSK the IKM is a run of zeros.
    /// </summary>
    public byte[] EarlySecret(ReadOnlySpan<byte> psk = default)
    {
        byte[] ikm = psk.IsEmpty ? new byte[HashLength] : psk.ToArray();
        byte[] salt = new byte[HashLength];
        return TlsHkdf.Extract(_hash, salt, ikm);
    }

    /// <summary>
    /// Handshake secret = HKDF-Extract(salt = Derive-Secret(Early,"derived",""), IKM = (EC)DHE).
    /// </summary>
    public byte[] HandshakeSecret(ReadOnlySpan<byte> earlySecret, ReadOnlySpan<byte> sharedSecret)
    {
        byte[] salt = DeriveSecret(earlySecret, "derived", _emptyTranscriptHash);
        return TlsHkdf.Extract(_hash, salt, sharedSecret);
    }

    /// <summary>
    /// Master secret = HKDF-Extract(salt = Derive-Secret(Handshake,"derived",""), IKM = 0).
    /// </summary>
    public byte[] MasterSecret(ReadOnlySpan<byte> handshakeSecret)
    {
        byte[] salt = DeriveSecret(handshakeSecret, "derived", _emptyTranscriptHash);
        return TlsHkdf.Extract(_hash, salt, new byte[HashLength]);
    }

    /// <summary>
    /// Convenient entry point: derives the handshake traffic secrets from the ECDHE secret and the
    /// transcript hash over ClientHello‖ServerHello. With resumption, the <paramref name="psk"/>
    /// flows into the early secret (RFC 8446 §7.1); without a PSK (default) it is 0.
    /// </summary>
    public HandshakeTrafficSecrets DeriveHandshakeSecrets(
        ReadOnlySpan<byte> sharedSecret,
        ReadOnlySpan<byte> transcriptHashClientHelloToServerHello,
        ReadOnlySpan<byte> psk = default)
    {
        byte[] early = EarlySecret(psk);
        byte[] handshake = HandshakeSecret(early, sharedSecret);
        byte[] client = DeriveSecret(handshake, "c hs traffic", transcriptHashClientHelloToServerHello);
        byte[] server = DeriveSecret(handshake, "s hs traffic", transcriptHashClientHelloToServerHello);
        return new HandshakeTrafficSecrets(handshake, client, server);
    }

    /// <summary>
    /// The <c>binder_key</c> for a resumption PSK (RFC 8446 §7.1): <c>Derive-Secret(Early Secret,
    /// "res binder", "")</c>. The PSK binder is then the <see cref="FinishedVerifyData"/> over the
    /// transcript hash of the ClientHello truncated just before the binders.
    /// </summary>
    public byte[] ResumptionBinderKey(ReadOnlySpan<byte> psk)
        => DeriveSecret(EarlySecret(psk), "res binder", _emptyTranscriptHash);

    /// <summary>
    /// The <c>resumption_master_secret</c> (RFC 8446 §7.1): <c>Derive-Secret(Master Secret, "res master",
    /// ClientHello…client Finished)</c>. The basis of the resumption PSKs issued later.
    /// </summary>
    public byte[] ResumptionMasterSecret(
        ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> transcriptHashThroughClientFinished)
        => DeriveSecret(masterSecret, "res master", transcriptHashThroughClientFinished);

    /// <summary>
    /// The resumption PSK resulting from a NewSessionTicket (RFC 8446 §4.6.1):
    /// <c>HKDF-Expand-Label(resumption_master_secret, "resumption", ticket_nonce, Hash.length)</c>.
    /// </summary>
    public byte[] ResumptionPsk(ReadOnlySpan<byte> resumptionMasterSecret, ReadOnlySpan<byte> ticketNonce)
        => TlsHkdf.ExpandLabel(_hash, resumptionMasterSecret, "resumption", ticketNonce, HashLength);

    /// <summary>
    /// The <c>client_early_traffic_secret</c> (RFC 8446 §7.1) for 0-RTT: <c>Derive-Secret(Early Secret(psk),
    /// "c e traffic", ClientHello)</c>. From it the QUIC layer derives the 0-RTT keys (client→server only).
    /// </summary>
    public byte[] ClientEarlyTrafficSecret(ReadOnlySpan<byte> psk, ReadOnlySpan<byte> transcriptHashClientHello)
        => DeriveSecret(EarlySecret(psk), "c e traffic", transcriptHashClientHello);

    /// <summary>
    /// Derives the application (1-RTT) traffic secrets from the handshake secret and the
    /// transcript hash up to and including the server Finished.
    /// </summary>
    public ApplicationTrafficSecrets DeriveApplicationSecrets(
        ReadOnlySpan<byte> handshakeSecret,
        ReadOnlySpan<byte> transcriptHashThroughServerFinished)
    {
        byte[] master = MasterSecret(handshakeSecret);
        byte[] client = DeriveSecret(master, "c ap traffic", transcriptHashThroughServerFinished);
        byte[] server = DeriveSecret(master, "s ap traffic", transcriptHashThroughServerFinished);
        return new ApplicationTrafficSecrets(master, client, server);
    }

    /// <summary>
    /// The <c>exporter_master_secret</c> (RFC 8446 §7.1): <c>Derive-Secret(Master Secret, "exp master",
    /// ClientHello…server Finished)</c>. The basis of all keying-material exports (§7.5).
    /// </summary>
    public byte[] ExporterMasterSecret(
        ReadOnlySpan<byte> masterSecret, ReadOnlySpan<byte> transcriptHashThroughServerFinished)
        => DeriveSecret(masterSecret, "exp master", transcriptHashThroughServerFinished);

    /// <summary>
    /// The TLS exporter (RFC 8446 §7.5): <c>HKDF-Expand-Label(Derive-Secret(exporter_master_secret,
    /// label, ""), "exporter", Hash(context), length)</c>. Both sides of the connection obtain
    /// identical keying material for the same label/context (e.g. for channel binding).
    /// </summary>
    public byte[] ExportKeyingMaterial(
        ReadOnlySpan<byte> exporterMasterSecret, string label, ReadOnlySpan<byte> context, int length)
    {
        byte[] derived = DeriveSecret(exporterMasterSecret, label, _emptyTranscriptHash);
        return TlsHkdf.ExpandLabel(_hash, derived, "exporter", HashBytes(context), length);
    }

    /// <summary>
    /// The <c>finished_key</c> of a traffic secret: <c>HKDF-Expand-Label(secret, "finished", "", Hash.length)</c>
    /// (RFC 8446 §4.4.4). The context is empty.
    /// </summary>
    public byte[] FinishedKey(ReadOnlySpan<byte> baseTrafficSecret)
        => TlsHkdf.ExpandLabel(_hash, baseTrafficSecret, "finished", HashLength);

    /// <summary>
    /// The <c>verify_data</c> of a Finished message: <c>HMAC(finished_key, transcript hash)</c>
    /// (RFC 8446 §4.4.4). For the server Finished the transcript is CH..CertificateVerify, for the
    /// client Finished CH..server Finished.
    /// </summary>
    public byte[] FinishedVerifyData(ReadOnlySpan<byte> baseTrafficSecret, ReadOnlySpan<byte> transcriptHash)
    {
        byte[] key = FinishedKey(baseTrafficSecret);
        return _hash == HashAlgorithmName.SHA384
            ? HMACSHA384.HashData(key, transcriptHash)
            : HMACSHA256.HashData(key, transcriptHash);
    }

    private byte[] HashBytes(ReadOnlySpan<byte> data)
    {
        byte[] output = new byte[HashLength];
        if (_hash == HashAlgorithmName.SHA256)
            SHA256.HashData(data, output);
        else
            SHA384.HashData(data, output);
        return output;
    }
}
