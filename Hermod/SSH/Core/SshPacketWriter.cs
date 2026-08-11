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
using System.Buffers;
using System.Numerics;
using System.Buffers.Binary;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A writer for the SSH binary wire format (RFC 4251, section 5) over an
    /// <see cref="IBufferWriter{Byte}"/>. All multi-byte integers are written big-endian
    /// ("network byte order").
    /// </summary>
    /// <remarks>
    /// Symmetric to <see cref="SshPacketReader"/>. Use e.g. an <see cref="ArrayBufferWriter{Byte}"/>
    /// as the destination and read the assembled bytes back via its written span/memory.
    /// </remarks>
    public ref struct SshPacketWriter
    {

        #region Data

        private readonly IBufferWriter<Byte>  buffer;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new SSH packet writer that appends to the given buffer writer.
        /// </summary>
        /// <param name="Buffer">The destination buffer writer.</param>
        public SshPacketWriter(IBufferWriter<Byte> Buffer)
        {
            this.buffer = Buffer ?? throw new ArgumentNullException(nameof(Buffer));
        }

        #endregion


        #region WriteByte(Value)

        /// <summary>
        /// Write a single byte (RFC 4251 'byte').
        /// </summary>
        public void WriteByte(Byte Value)
        {
            var span = buffer.GetSpan(1);
            span[0]  = Value;
            buffer.Advance(1);
        }

        #endregion

        #region WriteBoolean(Value)

        /// <summary>
        /// Write a boolean (RFC 4251 'boolean') as the byte 1 (true) or 0 (false).
        /// </summary>
        public void WriteBoolean(Boolean Value)
            => WriteByte(Value ? (Byte) 1 : (Byte) 0);

        #endregion

        #region WriteUInt32(Value)

        /// <summary>
        /// Write a big-endian unsigned 32-bit integer (RFC 4251 'uint32').
        /// </summary>
        public void WriteUInt32(UInt32 Value)
        {
            var span = buffer.GetSpan(4);
            BinaryPrimitives.WriteUInt32BigEndian(span, Value);
            buffer.Advance(4);
        }

        #endregion

        #region WriteUInt64(Value)

        /// <summary>
        /// Write a big-endian unsigned 64-bit integer (RFC 4251 'uint64').
        /// </summary>
        public void WriteUInt64(UInt64 Value)
        {
            var span = buffer.GetSpan(8);
            BinaryPrimitives.WriteUInt64BigEndian(span, Value);
            buffer.Advance(8);
        }

        #endregion

        #region WriteBinaryString(Value)

        /// <summary>
        /// Write a length-prefixed byte string (RFC 4251 'string').
        /// </summary>
        public void WriteBinaryString(ReadOnlySpan<Byte> Value)
        {

            WriteUInt32((UInt32) Value.Length);

            if (Value.Length > 0)
            {
                var span = buffer.GetSpan(Value.Length);
                Value.CopyTo(span);
                buffer.Advance(Value.Length);
            }

        }

        #endregion

        #region WriteString(Value, Encoding = null)

        /// <summary>
        /// Write a length-prefixed text string (RFC 4251 'string'), encoded with the given encoding
        /// (UTF-8 by default).
        /// </summary>
        /// <param name="Value">The text to write.</param>
        /// <param name="Encoding">The text encoding to use; defaults to UTF-8.</param>
        public void WriteString(String     Value,
                                Encoding?  Encoding = null)
        {

            Encoding ??= Encoding.UTF8;

            var count = Encoding.GetByteCount(Value);
            WriteUInt32((UInt32) count);

            if (count > 0)
            {
                var span = buffer.GetSpan(count);
                Encoding.GetBytes(Value, span);
                buffer.Advance(count);
            }

        }

        #endregion

        #region WriteNameList(Names)

        /// <summary>
        /// Write a name-list (RFC 4251 'name-list'): a length-prefixed, comma-separated list of
        /// US-ASCII names. Individual names must be non-empty, must not contain a comma and must be
        /// printable US-ASCII.
        /// </summary>
        /// <param name="Names">The names to write (an empty sequence writes an empty list).</param>
        public void WriteNameList(params String[] Names)
            => WriteNameList((IReadOnlyList<String>) Names);


        /// <summary>
        /// Write a name-list (RFC 4251 'name-list'): a length-prefixed, comma-separated list of
        /// US-ASCII names. Individual names must be non-empty, must not contain a comma and must be
        /// printable US-ASCII.
        /// </summary>
        /// <param name="Names">The names to write (an empty sequence writes an empty list).</param>
        public void WriteNameList(IReadOnlyList<String> Names)
        {

            for (var i = 0; i < Names.Count; i++)
            {

                var name = Names[i];

                if (String.IsNullOrEmpty(name))
                    throw new SshWireException("A name within an SSH name-list must not be empty!");

                foreach (var c in name)
                    if (c < 0x21 || c > 0x7E || c == ',')
                        throw new SshWireException($"The name '{name}' contains a character that is not allowed within an SSH name-list!");

            }

            WriteString(String.Join(',', Names), Encoding.ASCII);

        }

        #endregion

        #region WriteMPInt(Value)

        /// <summary>
        /// Write a multiple-precision integer (RFC 4251 'mpint') as a length-prefixed, two's-complement,
        /// big-endian number. The value zero is written as a zero-length string.
        /// </summary>
        public void WriteMPInt(BigInteger Value)
        {

            if (Value.IsZero)
            {
                WriteUInt32(0);
                return;
            }

            WriteBinaryString(Value.ToByteArray(isUnsigned: false, isBigEndian: true));

        }

        #endregion

    }

}
