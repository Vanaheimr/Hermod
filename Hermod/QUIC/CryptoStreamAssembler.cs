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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic;

/// <summary>
/// Reassembles the CRYPTO byte stream of an encryption level from (possibly unordered,
/// cross-packet) fragments (RFC 9000 §19.6, §7.5). Each encryption level has its own CRYPTO
/// stream starting at offset 0. Duplicates/overlaps are handled idempotently.
/// </summary>
public sealed class CryptoStreamAssembler
{
    private readonly SortedDictionary<ulong, byte[]> _fragments = new();

    /// <summary>
    /// Adds a fragment starting at <paramref name="offset"/>.
    /// </summary>
    public void Add(ulong offset, ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return;

        // Ignore fragments already fully covered by the contiguous prefix.
        // (For the handshake this simple, non-overlap-splitting storage suffices.)
        _fragments[offset] = data.ToArray();
    }

    /// <summary>
    /// The contiguous prefix from offset 0. Stops at the first gap – the rest waits for
    /// missing fragments.
    /// </summary>
    public byte[] Contiguous()
    {
        using var ms = new MemoryStream();
        ulong pos = 0;
        foreach ((ulong offset, byte[] data) in _fragments)
        {
            if (offset > pos)
                break; // gap

            // Overlap: only append the part not yet written.
            ulong skip = pos - offset;
            if (skip >= (ulong)data.Length)
                continue; // fully redundant

            ms.Write(data, (int)skip, data.Length - (int)skip);
            pos += (ulong)data.Length - skip;
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Length of the currently contiguous prefix.
    /// </summary>
    public long ContiguousLength => Contiguous().Length;
}
