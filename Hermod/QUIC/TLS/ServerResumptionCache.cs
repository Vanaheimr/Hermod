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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;

/// <summary>
/// The result of a successful ticket lookup.
/// </summary>
/// <param name="Psk">The resumption PSK belonging to the ticket.</param>
/// <param name="CipherSuite">The cipher suite of the original connection.</param>
/// <param name="EarlyDataFresh">
/// Whether the ticket age claimed by the client is plausible (RFC 8446 §8.3 freshness check).
/// <c>false</c> ⇒ resumption may still proceed, but 0-RTT MUST be rejected.
/// </param>
public sealed record ResumptionLookup(byte[] Psk, CipherSuite CipherSuite, bool EarlyDataFresh);

/// <summary>
/// Server-side ticket store for session resumption (RFC 8446 §4.6.1) — and with it the anti-replay
/// defence for 0-RTT.
/// <para>
/// <b>RFC 9001 §9.2 makes this a MUST for QUIC:</b> "Endpoints MUST implement and use the replay
/// protections described in [TLS13]". Implemented here is the <b>single-use ticket</b> variant of
/// RFC 8446 §8.1 — the tickets are database keys and the PSK is deleted on use, which additionally
/// gives resumed connections forward secrecy. Complemented by the <b>freshness check</b> of §8.3:
/// the ticket age claimed by the client is compared against the age measured by the server, and a
/// gross mismatch rejects 0-RTT (the 1-RTT handshake still completes).
/// </para>
/// <para>
/// Deliberate limitation: the store is process-local. RFC 8446 §8 requires "at most once per server
/// instance", which is exactly what this achieves — in a multi-instance deployment the number of
/// possible replays equals the number of instances, which is what the RFC calls the minimum
/// guarantee. A distributed deployment needs a shared store.
/// </para>
/// </summary>
public sealed class ServerResumptionCache
{
    /// <summary>
    /// Default upper bound on stored tickets. Beyond it the oldest are evicted — without a cap, an
    /// attacker could exhaust memory by opening connections, since every handshake issues a ticket.
    /// </summary>
    public const int DefaultMaxEntries = 4096;

    /// <summary>
    /// Default tolerance of the §8.3 freshness check. RFC 8446 §8.3 suggests "windows on the order
    /// of ten seconds" for Internet clients, to absorb clock skew and propagation delay.
    /// </summary>
    public static readonly TimeSpan DefaultFreshnessWindow = TimeSpan.FromSeconds(10);

    private sealed record Entry(
        byte[] Psk,
        CipherSuite CipherSuite,
        uint MaxEarlyDataSize,
        byte[] TransportParameters,
        uint AgeAdd,
        DateTimeOffset IssuedAt,
        uint LifetimeSeconds);

    // Insertion-ordered ⇒ the oldest entry is the first one, which makes eviction O(1) amortised.
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly Queue<string> _insertionOrder = new();
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _maxEntries;
    private readonly TimeSpan _freshnessWindow;

    /// <param name="timeProvider">Clock for expiry and freshness check; default the system clock.</param>
    /// <param name="maxEntries">Upper bound on stored tickets (see <see cref="DefaultMaxEntries"/>).</param>
    /// <param name="freshnessWindow">Tolerance of the §8.3 check (see <see cref="DefaultFreshnessWindow"/>).</param>
    public ServerResumptionCache(TimeProvider? timeProvider = null,
                                 int maxEntries = DefaultMaxEntries,
                                 TimeSpan? freshnessWindow = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxEntries = maxEntries;
        _freshnessWindow = freshnessWindow ?? DefaultFreshnessWindow;
    }

    /// <summary>
    /// Number of currently stored tickets (diagnostics/test).
    /// </summary>
    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    /// <summary>
    /// Creates a new ticket identity for a PSK and returns the opaque ticket bytes.
    /// <paramref name="ageAdd"/> and <paramref name="lifetimeSeconds"/> are the values that go into
    /// the NewSessionTicket — they are needed later for the freshness check and expiry.
    /// </summary>
    public byte[] Issue(byte[] psk, CipherSuite cipherSuite, uint maxEarlyDataSize, byte[] transportParameters,
                        uint ageAdd, uint lifetimeSeconds)
    {
        byte[] identity = RandomNumberGenerator.GetBytes(32);
        var entry = new Entry(psk, cipherSuite, maxEarlyDataSize, transportParameters,
                              ageAdd, _timeProvider.GetUtcNow(), lifetimeSeconds);

        lock (_lock)
        {
            string key = Convert.ToHexString(identity);
            _entries[key] = entry;
            _insertionOrder.Enqueue(key);
            EvictWhileOverCapacity();
        }
        return identity;
    }

    /// <summary>
    /// Looks a ticket identity up <b>without consuming it</b> — the caller has not verified the PSK
    /// binder yet. Consuming here would let anyone who merely observed a ticket identity destroy the
    /// legitimate client's ticket with a bogus binder. Expired tickets are removed and not returned.
    /// </summary>
    /// <param name="obfuscatedTicketAge">
    /// The <c>obfuscated_ticket_age</c> from the client's pre_shared_key extension — the basis of the
    /// §8.3 freshness check.
    /// </param>
    public bool TryResolve(byte[] identity, uint obfuscatedTicketAge, out ResumptionLookup? lookup)
    {
        lookup = null;
        string key = Convert.ToHexString(identity);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out Entry? entry))
                return false;

            // Expiry (RFC 8446 §4.6.1: the ticket is valid for ticket_lifetime seconds at most).
            if (now - entry.IssuedAt > TimeSpan.FromSeconds(entry.LifetimeSeconds))
            {
                _entries.Remove(key);
                return false;
            }

            lookup = new ResumptionLookup(entry.Psk, entry.CipherSuite,
                                          IsFresh(entry, obfuscatedTicketAge, now));
            return true;
        }
    }

    /// <summary>
    /// Consumes the ticket — the single-use guarantee of RFC 8446 §8.1. Returns <c>true</c> only to
    /// the FIRST caller; every replay gets <c>false</c> and must fall back to a full handshake.
    /// <para>Call only AFTER the PSK binder has been verified, and always evaluate the result: an
    /// atomic remove is what makes the guarantee hold even for concurrent ClientHellos carrying the
    /// same ticket.</para>
    /// </summary>
    public bool TryConsume(byte[] identity)
    {
        lock (_lock)
            return _entries.Remove(Convert.ToHexString(identity));
    }

    /// <summary>
    /// Freshness check (RFC 8446 §8.3): the client reports how old it believes the ticket to be
    /// (<c>obfuscated_ticket_age − ticket_age_add</c>, in ms). The server compares that against the
    /// age it measured itself; if the two differ by more than the window, the ClientHello is not
    /// plausibly recent and 0-RTT is rejected.
    /// <para>We estimate the round-trip time as 0 (the ClientHello is the first flight — no RTT
    /// sample exists yet); the window absorbs that, as the RFC anticipates.</para>
    /// </summary>
    private bool IsFresh(Entry entry, uint obfuscatedTicketAge, DateTimeOffset now)
    {
        // The subtraction wraps modulo 2^32, exactly as the client's addition did (§4.2.11.1).
        uint claimedAgeMs = unchecked(obfuscatedTicketAge - entry.AgeAdd);
        double measuredAgeMs = (now - entry.IssuedAt).TotalMilliseconds;
        return Math.Abs(measuredAgeMs - claimedAgeMs) <= _freshnessWindow.TotalMilliseconds;
    }

    private void EvictWhileOverCapacity()
    {
        while (_entries.Count > _maxEntries && _insertionOrder.Count > 0)
        {
            string oldest = _insertionOrder.Dequeue();
            _entries.Remove(oldest);
        }
        // Keys of already-consumed tickets pile up in the queue; compact it when it runs far ahead.
        if (_insertionOrder.Count > 4 * _maxEntries)
        {
            var live = new Queue<string>(_insertionOrder.Where(_entries.ContainsKey));
            _insertionOrder.Clear();
            foreach (string key in live)
                _insertionOrder.Enqueue(key);
        }
    }
}
