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
using System.IO.Pipelines;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The result of the SSH version exchange: the peer's identification string and its exact wire
    /// bytes (without CR LF), which are needed later as V_C / V_S in the key-exchange hash.
    /// </summary>
    /// <param name="Identification">The parsed peer identification string.</param>
    /// <param name="WireBytes">The peer's identification line as received, without the trailing CR LF.</param>
    public readonly record struct SshRemoteIdentification(SshIdentificationString  Identification,
                                                          Byte[]                   WireBytes);


    /// <summary>
    /// Sends and receives the SSH protocol version / identification string (RFC 4253, section 4.2)
    /// over an <see cref="System.IO.Pipelines"/> duplex before the binary packet protocol begins.
    /// </summary>
    public static class SshVersionExchange
    {

        /// <summary>
        /// The maximum number of bytes tolerated before the peer's "SSH-" line, to bound the memory a
        /// misbehaving or malicious peer can force us to buffer with endless pre-banner lines.
        /// </summary>
        public const Int32 MaxPreBannerBytes = 64 * 1024;


        #region WriteAsync(Writer, Identification, CancellationToken = default)

        /// <summary>
        /// Write our identification string followed by CR LF and flush it.
        /// </summary>
        /// <param name="Writer">The pipe writer.</param>
        /// <param name="Identification">Our identification string.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async ValueTask WriteAsync(PipeWriter                Writer,
                                                 SshIdentificationString   Identification,
                                                 CancellationToken         CancellationToken = default)
        {

            var line   = Identification.Line + "\r\n";
            var bytes  = Encoding.ASCII.GetBytes(line);

            await Writer.WriteAsync(bytes, CancellationToken).ConfigureAwait(false);
            await Writer.FlushAsync(CancellationToken).      ConfigureAwait(false);

        }

        #endregion

        #region ReadAsync(Reader, CancellationToken = default)

        /// <summary>
        /// Read the peer's identification string, skipping any lines that do not start with "SSH-"
        /// (a server may send banner lines beforehand, RFC 4253 section 4.2).
        /// </summary>
        /// <param name="Reader">The pipe reader.</param>
        /// <param name="CancellationToken">An optional cancellation token.</param>
        public static async ValueTask<SshRemoteIdentification> ReadAsync(PipeReader         Reader,
                                                                         CancellationToken  CancellationToken = default)
        {

            var skippedBytes = 0;

            while (true)
            {

                var result  = await Reader.ReadAsync(CancellationToken).ConfigureAwait(false);
                var buffer  = result.Buffer;

                while (TryReadLine(ref buffer, out var line))
                {

                    // An "SSH-" line is the identification string; anything before it is a banner to skip.
                    if (StartsWithSsh(line))
                    {

                        var wireBytes  = line.ToArray();
                        var text       = Encoding.ASCII.GetString(wireBytes);

                        Reader.AdvanceTo(buffer.Start);

                        return new SshRemoteIdentification(
                                   SshIdentificationString.Parse(text),
                                   wireBytes
                               );

                    }

                    skippedBytes += (Int32) Math.Min(line.Length + 1, Int32.MaxValue);
                    if (skippedBytes > MaxPreBannerBytes)
                        throw new SshWireException($"The peer sent more than {MaxPreBannerBytes} bytes before its SSH identification string!");

                }

                Reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    throw new SshWireException("The connection was closed before an SSH identification string was received!");

            }

        }

        #endregion


        #region (private) TryReadLine  (ref Buffer, out Line)

        // Extract one line (terminated by LF, with an optional preceding CR) from the buffer.
        private static Boolean TryReadLine(ref ReadOnlySequence<Byte> Buffer, out ReadOnlySequence<Byte> Line)
        {

            var position = Buffer.PositionOf((Byte) '\n');

            if (position is null)
            {

                // No line terminator yet; guard against an unbounded first line.
                if (Buffer.Length > SshIdentificationString.MaxLineLength + MaxPreBannerBytes)
                    throw new SshWireException("An SSH identification line exceeded the maximum length!");

                Line = default;
                return false;

            }

            var lineWithLf  = Buffer.Slice(0, position.Value);

            // Strip an optional trailing CR.
            Line    = TrimTrailingCarriageReturn(lineWithLf);
            Buffer  = Buffer.Slice(Buffer.GetPosition(1, position.Value));

            return true;

        }

        #endregion

        #region (private) TrimTrailingCarriageReturn(Line)

        private static ReadOnlySequence<Byte> TrimTrailingCarriageReturn(ReadOnlySequence<Byte> Line)
        {

            if (Line.Length == 0)
                return Line;

            // Peek the last byte.
            var lastPosition  = Line.GetPosition(Line.Length - 1);
            var lastSlice     = Line.Slice(lastPosition);

            return lastSlice.FirstSpan.Length > 0 && lastSlice.FirstSpan[0] == (Byte) '\r'
                       ? Line.Slice(0, Line.Length - 1)
                       : Line;

        }

        #endregion

        #region (private) StartsWithSsh(Line)

        private static Boolean StartsWithSsh(ReadOnlySequence<Byte> Line)
        {

            Span<Byte> prefix = stackalloc Byte[4];

            if (Line.Length < 4)
                return false;

            Line.Slice(0, 4).CopyTo(prefix);

            return prefix[0] == (Byte) 'S' &&
                   prefix[1] == (Byte) 'S' &&
                   prefix[2] == (Byte) 'H' &&
                   prefix[3] == (Byte) '-';

        }

        #endregion

    }

}
