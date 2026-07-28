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

using System.Net;
using System.Security.Cryptography;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

/// <summary>
/// Issues and validates address-validation tokens for Retry packets (RFC 9000 §8.1.2/§8.1.4). The
/// token carries everything the server would otherwise have to remember, so a Retry can be answered
/// <b>without any connection state</b> — which is the entire point: a spoofed-source flood then
/// costs the server one AEAD seal per packet instead of a connection object.
/// <para>
/// Token layout (server-private, never parsed by anyone else):
/// <c>nonce(12) ‖ AES-256-GCM(plaintext) ‖ tag(16)</c> with
/// <c>plaintext = kind(1) ‖ issued(8, ms) ‖ odcid ‖ retryScid</c> (each length-prefixed) and the
/// client address as associated data. The key is HKDF-derived from a server secret; only the server
/// needs it (§8.1.4 "Only the server requires access to the integrity protection key").
/// </para>
/// <para>
/// What the RFC demands and where it is met:
/// <list type="bullet">
/// <item>§8.1.1 — Retry and NEW_TOKEN tokens MUST be distinguishable: <see cref="TokenKind"/>.</item>
/// <item>§8.1.4 — MUST be hard to guess and integrity protected: AEAD with a 256-bit key.</item>
/// <item>§8.1.4 — Retry tokens SHOULD prove address <i>and port</i> are unchanged: both are AAD.</item>
/// <item>§8.1.4 — replay MUST be prevented or limited: short <see cref="Lifetime"/> plus a bounded
///       single-use set (<see cref="TryConsume"/>).</item>
/// <item>§8.1.2 — the server must show it saw the original Initial: the ODCID rides in the token and
///       comes back out for the <c>original_destination_connection_id</c> transport parameter.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RetryTokenGenerator
{
    /// <summary>
    /// First plaintext byte: which mechanism issued this token (RFC 9000 §8.1.1). Both kinds travel
    /// in the same Initial header field but are validated differently — a Retry token is bound to
    /// one connection attempt, a NEW_TOKEN token to a future one.
    /// </summary>
    public enum TokenKind : byte
    {
        /// <summary>Issued in a Retry packet; usable immediately and only for this attempt.</summary>
        Retry = 0x01,

        /// <summary>Issued in a NEW_TOKEN frame (§8.1.3). Reserved — not issued yet.</summary>
        NewToken = 0x02,
    }

    private const int NonceLength = 12;
    private const int TagLength = 16;

    /// <summary>
    /// Upper bound on remembered spent tokens. Beyond it the oldest are dropped: replay stays
    /// "limited" via <see cref="Lifetime"/> even in the worst case, and the memory cannot grow.
    /// </summary>
    public const int MaxTrackedTokens = 8192;

    /// <summary>
    /// Default validity of a Retry token. §8.1.4 asks for "a short time", since a client returns the
    /// token immediately — this only has to cover one round trip plus a retransmission or two.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(10);

    private readonly byte[] _key;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();
    private readonly Dictionary<ulong, long> _spent = []; // token fingerprint -> expiry (ms)

    /// <summary>
    /// <paramref name="secret"/> = the server secret (a random one is generated when omitted; it
    /// then only survives for this process, which is fine for tokens that live seconds).
    /// </summary>
    public RetryTokenGenerator(byte[]? secret = null, TimeSpan? lifetime = null, TimeProvider? timeProvider = null)
    {
        byte[] material = secret is { Length: > 0 } ? secret : RandomNumberGenerator.GetBytes(32);
        // Derive rather than use the secret directly, so one secret can key several purposes
        // (stateless reset, Retry, NEW_TOKEN) without ever sharing a key between them.
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, info: "quic retry token"u8.ToArray());
        Lifetime = lifetime ?? DefaultLifetime;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// How long an issued token stays valid.
    /// </summary>
    public TimeSpan Lifetime { get; }

    /// <summary>
    /// Number of spent tokens currently remembered (diagnostics/test).
    /// </summary>
    public int TrackedTokenCount { get { lock (_lock) return _spent.Count; } }

    /// <summary>
    /// Issues a token for <paramref name="client"/> that binds the DCID of the triggering Initial
    /// (<paramref name="originalDestinationConnectionId"/>) and the source connection ID the server
    /// puts into the Retry packet (<paramref name="retrySourceConnectionId"/> — which becomes the
    /// client's new DCID, RFC 9000 §7.2).
    /// </summary>
    public byte[] Issue(IPEndPoint client,
                        ConnectionId originalDestinationConnectionId,
                        ConnectionId retrySourceConnectionId)
    {
        using var plain = new BufferWriter(64);
        plain.WriteByte((byte)TokenKind.Retry);
        plain.WriteUInt64((ulong)_timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        plain.WriteByte((byte)originalDestinationConnectionId.Length);
        plain.WriteBytes(originalDestinationConnectionId.Span);
        plain.WriteByte((byte)retrySourceConnectionId.Length);
        plain.WriteBytes(retrySourceConnectionId.Span);

        ReadOnlySpan<byte> plaintext = plain.WrittenSpan;
        byte[] token = new byte[NonceLength + plaintext.Length + TagLength];
        RandomNumberGenerator.Fill(token.AsSpan(0, NonceLength));

        using var aead = new AesGcm(_key, TagLength);
        aead.Encrypt(token.AsSpan(0, NonceLength),
                     plaintext,
                     token.AsSpan(NonceLength, plaintext.Length),
                     token.AsSpan(NonceLength + plaintext.Length, TagLength),
                     AssociatedData(client));
        return token;
    }

    /// <summary>
    /// Validates a token returned by a client: authentic, issued for exactly this address and port,
    /// not expired, and of kind <see cref="TokenKind.Retry"/>. Does <em>not</em> consume it — call
    /// <see cref="TryConsume"/> for that once the packet has proven otherwise usable.
    /// </summary>
    public bool TryValidate(ReadOnlySpan<byte> token, IPEndPoint client,
                            out ConnectionId originalDestinationConnectionId,
                            out ConnectionId retrySourceConnectionId)
    {
        originalDestinationConnectionId = ConnectionId.Empty;
        retrySourceConnectionId = ConnectionId.Empty;

        if (token.Length < NonceLength + TagLength + 1)
            return false;

        int plaintextLength = token.Length - NonceLength - TagLength;
        byte[] plaintext = new byte[plaintextLength];
        try
        {
            using var aead = new AesGcm(_key, TagLength);
            // Fails for a forged, modified or foreign-address token — this is the integrity
            // protection §8.1.4 requires, and the address binding in one step.
            aead.Decrypt(token[..NonceLength],
                         token.Slice(NonceLength, plaintextLength),
                         token[^TagLength..],
                         plaintext,
                         AssociatedData(client));
        }
        catch (CryptographicException)
        {
            return false;
        }

        var reader = new BufferReader(plaintext);
        if (!reader.TryReadByte(out byte kind) || kind != (byte)TokenKind.Retry)
            return false;
        if (!reader.TryReadUInt64(out ulong issuedMs))
            return false;
        if (!TryReadCid(ref reader, out originalDestinationConnectionId) ||
            !TryReadCid(ref reader, out retrySourceConnectionId))
            return false;

        long nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        long ageMs = nowMs - (long)issuedMs;
        // A negative age means the token predates our clock — treat it as invalid rather than
        // trusting it; the same holds for anything older than the lifetime (§8.1.4 "short time").
        return ageMs >= 0 && ageMs <= (long)Lifetime.TotalMilliseconds;
    }

    /// <summary>
    /// Marks a validated token as spent (RFC 9000 §8.1.4: "Servers are encouraged to allow tokens to
    /// be used only once"). Returns <c>false</c> if it was already used — the packet is then a
    /// replay and must not create a second connection.
    /// <para>
    /// A retransmitted Initial does not land here: it carries the same connection ID/address as the
    /// first one and is therefore routed to the connection that already exists.
    /// </para>
    /// </summary>
    public bool TryConsume(ReadOnlySpan<byte> token)
    {
        ulong fingerprint = Fingerprint(token);
        long nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        lock (_lock)
        {
            if (_spent.TryGetValue(fingerprint, out long expiry) && expiry > nowMs)
                return false;

            if (_spent.Count >= MaxTrackedTokens)
                Prune(nowMs);

            _spent[fingerprint] = nowMs + (long)Lifetime.TotalMilliseconds;
            return true;
        }
    }

    /// <summary>
    /// Drops expired entries; if that is not enough, the oldest go too. Caller holds the lock.
    /// </summary>
    private void Prune(long nowMs)
    {
        foreach (ulong expired in _spent.Where(e => e.Value <= nowMs).Select(e => e.Key).ToList())
            _spent.Remove(expired);

        if (_spent.Count < MaxTrackedTokens)
            return;

        // Everything is still live: give up the oldest quarter. Those tokens become replayable
        // until they expire — bounded memory beats an unbounded set under attack.
        foreach (ulong oldest in _spent.OrderBy(e => e.Value).Take(MaxTrackedTokens / 4).Select(e => e.Key).ToList())
            _spent.Remove(oldest);
    }

    /// <summary>
    /// 64-bit fingerprint of a token. The token is already authenticated when we get here, so this
    /// only has to be collision-resistant enough to identify a replay, not to resist forgery.
    /// </summary>
    private static ulong Fingerprint(ReadOnlySpan<byte> token)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(token, hash);
        return BitConverter.ToUInt64(hash[..8]);
    }

    /// <summary>
    /// The associated data: IP address ‖ port. A token stolen from another client's packet therefore
    /// fails to decrypt at a different address (§8.1.4).
    /// </summary>
    private static byte[] AssociatedData(IPEndPoint client)
    {
        Span<byte> address = stackalloc byte[16];
        if (!client.Address.TryWriteBytes(address, out int written))
            written = 0;
        byte[] aad = new byte[written + 2];
        address[..written].CopyTo(aad);
        aad[written] = (byte)(client.Port >> 8);
        aad[written + 1] = (byte)client.Port;
        return aad;
    }

    private static bool TryReadCid(ref BufferReader reader, out ConnectionId cid)
    {
        cid = ConnectionId.Empty;
        if (!reader.TryReadByte(out byte length) || length > ConnectionId.MaxLength ||
            !reader.TryReadBytes(length, out ReadOnlySpan<byte> bytes))
            return false;
        cid = new ConnectionId(bytes);
        return true;
    }
}
