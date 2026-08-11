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

using System.Text;
using System.Numerics;
using System.Buffers.Binary;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A forward-only reader for the SSH binary wire format (RFC 4251, section 5) over a
    /// contiguous byte span. All multi-byte integers are big-endian ("network byte order").
    /// </summary>
    /// <remarks>
    /// Every method operates on untrusted, peer-supplied data. Before allocating or slicing,
    /// each length field is validated against both the remaining bytes and a configurable maximum,
    /// so a malformed length can never cause an over-read or an oversized allocation; it raises an
    /// <see cref="SshWireException"/> instead.
    /// </remarks>
    public ref struct SshPacketReader
    {

        #region Data

        private readonly ReadOnlySpan<Byte>  buffer;
        private          Int32               position;

        /// <summary>
        /// The default maximum accepted length (256 KiB) of a single length-prefixed field.
        /// Callers that legitimately expect larger blobs pass an explicit maximum.
        /// </summary>
        public const     Int32               DefaultMaxLength = 256 * 1024;

        #endregion

        #region Properties

        /// <summary>The current read offset within the underlying buffer.</summary>
        public readonly Int32    Position       => position;

        /// <summary>The number of bytes not yet consumed.</summary>
        public readonly Int32    Remaining      => buffer.Length - position;

        /// <summary>Whether at least one more byte is available.</summary>
        public readonly Boolean  HasMoreData    => position < buffer.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SSH packet reader over the given byte span.
        /// </summary>
        /// <param name="Buffer">The bytes to read from (typically one decrypted packet payload).</param>
        public SshPacketReader(ReadOnlySpan<Byte> Buffer)
        {
            this.buffer    = Buffer;
            this.position  = 0;
        }

        #endregion


        #region (private) EnsureAvailable(Count)

        private readonly void EnsureAvailable(Int32 Count)
        {
            if (Count < 0)
                throw new SshWireException($"Negative read length ({Count})!");

            if (Remaining < Count)
                throw new SshWireException($"Unexpected end of SSH data: {Count} byte(s) requested, but only {Remaining} remaining!");
        }

        #endregion


        #region ReadByte()

        /// <summary>
        /// Read a single byte (RFC 4251 'byte').
        /// </summary>
        public Byte ReadByte()
        {
            EnsureAvailable(1);
            return buffer[position++];
        }

        #endregion

        #region ReadBytes(Count)

        /// <summary>
        /// Read <paramref name="Count"/> raw bytes (not length-prefixed) as a span into the underlying
        /// buffer. Used for fixed-size fields such as the 16-byte KEXINIT cookie.
        /// </summary>
        /// <param name="Count">The number of bytes to read.</param>
        public ReadOnlySpan<Byte> ReadBytes(Int32 Count)
        {
            EnsureAvailable(Count);
            var slice  = buffer.Slice(position, Count);
            position  += Count;
            return slice;
        }

        #endregion

        #region ReadBoolean()

        /// <summary>
        /// Read a boolean (RFC 4251 'boolean'): the byte 0 is false, any non-zero value is true.
        /// </summary>
        public Boolean ReadBoolean()
            => ReadByte() != 0;

        #endregion

        #region ReadUInt32()

        /// <summary>
        /// Read a big-endian unsigned 32-bit integer (RFC 4251 'uint32').
        /// </summary>
        public UInt32 ReadUInt32()
        {
            EnsureAvailable(4);
            var value  = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(position, 4));
            position  += 4;
            return value;
        }

        #endregion

        #region ReadUInt64()

        /// <summary>
        /// Read a big-endian unsigned 64-bit integer (RFC 4251 'uint64').
        /// </summary>
        public UInt64 ReadUInt64()
        {
            EnsureAvailable(8);
            var value  = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(position, 8));
            position  += 8;
            return value;
        }

        #endregion

        #region ReadBinaryStringSpan(MaxLength = DefaultMaxLength)

        /// <summary>
        /// Read a length-prefixed byte string (RFC 4251 'string') and return it as a span into the
        /// underlying buffer (zero-copy).
        /// </summary>
        /// <param name="MaxLength">The maximum accepted length; longer strings raise an exception.</param>
        public ReadOnlySpan<Byte> ReadBinaryStringSpan(Int32 MaxLength = DefaultMaxLength)
        {

            var length = ReadUInt32();

            if (length > (UInt32) MaxLength)
                throw new SshWireException($"SSH string length {length} exceeds the maximum of {MaxLength} byte(s)!");

            if (length > (UInt32) Remaining)
                throw new SshWireException($"SSH string length {length} exceeds the {Remaining} remaining byte(s)!");

            var slice  = buffer.Slice(position, (Int32) length);
            position  += (Int32) length;

            return slice;

        }

        #endregion

        #region ReadBinaryString(MaxLength = DefaultMaxLength)

        /// <summary>
        /// Read a length-prefixed byte string (RFC 4251 'string') as a newly allocated byte array.
        /// </summary>
        /// <param name="MaxLength">The maximum accepted length; longer strings raise an exception.</param>
        public Byte[] ReadBinaryString(Int32 MaxLength = DefaultMaxLength)
            => ReadBinaryStringSpan(MaxLength).ToArray();

        #endregion

        #region ReadString(Encoding = null, MaxLength = DefaultMaxLength)

        /// <summary>
        /// Read a length-prefixed text string (RFC 4251 'string'), decoded with the given encoding
        /// (UTF-8 by default).
        /// </summary>
        /// <param name="Encoding">The text encoding to use; defaults to UTF-8.</param>
        /// <param name="MaxLength">The maximum accepted length; longer strings raise an exception.</param>
        public String ReadString(Encoding? Encoding   = null,
                                  Int32     MaxLength  = DefaultMaxLength)

            => (Encoding ?? Encoding.UTF8).GetString(ReadBinaryStringSpan(MaxLength));

        #endregion

        #region ReadNameList(MaxLength = DefaultMaxLength)

        /// <summary>
        /// Read a name-list (RFC 4251 'name-list'): a length-prefixed, comma-separated list of
        /// US-ASCII names. An empty list is returned as an empty array.
        /// </summary>
        /// <param name="MaxLength">The maximum accepted length; longer lists raise an exception.</param>
        public String[] ReadNameList(Int32 MaxLength = DefaultMaxLength)
        {

            var text = ReadString(Encoding.ASCII, MaxLength);

            return text.Length == 0
                       ? []
                       : text.Split(',');

        }

        #endregion

        #region ReadMPInt(MaxLength = DefaultMaxLength)

        /// <summary>
        /// Read a multiple-precision integer (RFC 4251 'mpint'): a length-prefixed, two's-complement,
        /// big-endian number. A zero-length string represents the value zero.
        /// </summary>
        /// <param name="MaxLength">The maximum accepted length; longer numbers raise an exception.</param>
        public BigInteger ReadMPInt(Int32 MaxLength = DefaultMaxLength)
        {

            var span = ReadBinaryStringSpan(MaxLength);

            return span.Length == 0
                       ? BigInteger.Zero
                       : new BigInteger(span, isUnsigned: false, isBigEndian: true);

        }

        #endregion

    }

}
