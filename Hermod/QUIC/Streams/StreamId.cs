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

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Streams;

/// <summary>
/// A QUIC stream ID (RFC 9000 §2.1). The two low-order bits encode initiator and direction:
/// bit 0x1 = initiator (0 = client, 1 = server), bit 0x2 = direction (0 = bidirectional,
/// 1 = unidirectional). The remaining bits are the stream's running index per category.
/// </summary>
public readonly struct StreamId : IEquatable<StreamId>, IComparable<StreamId>
{
    public ulong Value { get; }

    public StreamId(ulong value) => Value = value;

    public bool IsClientInitiated => (Value & 0x1) == 0;
    public bool IsServerInitiated => (Value & 0x1) != 0;
    public bool IsBidirectional => (Value & 0x2) == 0;
    public bool IsUnidirectional => (Value & 0x2) != 0;

    /// <summary>
    /// Running index of this stream within its category (Value >> 2).
    /// </summary>
    public ulong Index => Value >> 2;

    /// <summary>
    /// Builds a stream ID from initiator, direction and index.
    /// </summary>
    public static StreamId Create(bool clientInitiated, bool bidirectional, ulong index)
    {
        ulong bits = (clientInitiated ? 0UL : 0x1) | (bidirectional ? 0UL : 0x2);
        return new StreamId((index << 2) | bits);
    }

    public int CompareTo(StreamId other) => Value.CompareTo(other.Value);
    public bool Equals(StreamId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is StreamId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"Stream {Value} ({(IsClientInitiated ? "client" : "server")}/{(IsBidirectional ? "bidi" : "uni")})";

    public static bool operator ==(StreamId left, StreamId right) => left.Equals(right);
    public static bool operator !=(StreamId left, StreamId right) => !left.Equals(right);
}
