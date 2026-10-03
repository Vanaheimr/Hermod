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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    /// <summary>
    /// What was wrong with a line, if anything.
    /// </summary>
    public enum SMTPLineStatus
    {

        /// <summary>
        /// A well-formed line, ended by CR LF.
        /// </summary>
        Ok,

        /// <summary>
        /// The line contains a CR or a LF that is not part of a CR LF pair. It is
        /// kept as part of the line, never treated as its end (RFC 5321 §2.3.8).
        /// </summary>
        BareLineEnding,

        /// <summary>
        /// The line exceeded the limit. Everything up to its CR LF was read and
        /// dropped, so the next read starts at the next line.
        /// </summary>
        TooLong

    }


    /// <summary>
    /// One line read from an SMTP stream.
    /// </summary>
    /// <param name="Text">The line without its CR LF, one char per octet (Latin-1).</param>
    /// <param name="Status">Whether the line was well-formed.</param>
    public readonly record struct SMTPLine(String          Text,
                                           SMTPLineStatus  Status);


    /// <summary>
    /// A byte-exact SMTP line reader: a line ends at CR LF and nowhere else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 5321 §2.3.8: "Conforming implementations MUST NOT recognize or generate
    /// any other character or character sequence as a line terminator." The
    /// <see cref="StreamReader"/> this replaces ends a line at CR, at LF and at
    /// CR LF alike, which let <c>&lt;LF&gt;.&lt;LF&gt;</c> end DATA and the bytes after it
    /// run as a second, smuggled transaction (CVE-2023-51764 and relatives).
    /// </para>
    /// <para>
    /// A bare CR or LF stays inside the line and is reported, so the session
    /// decides what to do with it. The line limit is enforced while reading, not
    /// after: a peer that never sends CR LF cannot grow the buffer past it.
    /// </para>
    /// <para>
    /// The reader buffers. Whatever it has read past the current line belongs to
    /// it, so a session must not read the underlying stream directly once it has
    /// one — BDAT chunks go through <see cref="ReadExactlyAsync"/>. Replacing the
    /// reader (as STARTTLS does) discards the buffer, which is what RFC 3207 §4.2
    /// wants done with plaintext pipelined after STARTTLS.
    /// </para>
    /// </remarks>
    public sealed class SMTPLineReader(Stream Stream)
    {

        private const Byte CR = (Byte) '\r';
        private const Byte LF = (Byte) '\n';

        private readonly Byte[]  buffer = new Byte[16 * 1024];
        private Int32            start;
        private Int32            end;


        /// <summary>
        /// Make at least one unread byte available. False at the end of the stream.
        /// </summary>
        private async ValueTask<Boolean> FillAsync(CancellationToken CancellationToken)
        {

            if (start < end)
                return true;

            start = 0;
            end   = await Stream.ReadAsync(buffer, CancellationToken).ConfigureAwait(false);

            return end > 0;

        }


        /// <summary>
        /// Read one line, ended by CR LF.
        /// </summary>
        /// <param name="MaxLength">The longest acceptable line, CR LF included.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <returns>The line, or null when the stream ended before its CR LF.</returns>
        public async Task<SMTPLine?> ReadLineAsync(Int32              MaxLength,
                                                   CancellationToken  CancellationToken = default)
        {

            // Keep at most the content and the CR of the terminating CR LF.
            var  kept     = new MemoryStream();
            var  octets   = 0L;
            Byte previous = 0;

            while (await FillAsync(CancellationToken).ConfigureAwait(false))
            {

                var octet = buffer[start++];

                if (octet == LF && previous == CR)
                {

                    var contentLength = octets - 1;          // without the CR
                    var line          = kept.GetBuffer();

                    if (contentLength + 2 > MaxLength)
                        return new SMTPLine("", SMTPLineStatus.TooLong);

                    var text   = Encoding.Latin1.GetString(line, 0, (Int32) contentLength);
                    var status = Array.IndexOf(line, CR, 0, (Int32) contentLength) >= 0 ||
                                 Array.IndexOf(line, LF, 0, (Int32) contentLength) >= 0
                                     ? SMTPLineStatus.BareLineEnding
                                     : SMTPLineStatus.Ok;

                    return new SMTPLine(text, status);

                }

                if (octets < MaxLength)
                    kept.WriteByte(octet);

                octets++;
                previous = octet;

            }

            return null;

        }


        /// <summary>
        /// Read exactly <paramref name="Count"/> octets — a BDAT chunk (RFC 3030).
        /// </summary>
        /// <param name="Count">The number of octets.</param>
        /// <param name="Destination">Where to copy them, or null to read and discard.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <returns>False when the stream ended first.</returns>
        public async Task<Boolean> ReadExactlyAsync(Int64              Count,
                                                    Stream?            Destination,
                                                    CancellationToken  CancellationToken = default)
        {

            while (Count > 0)
            {

                if (!await FillAsync(CancellationToken).ConfigureAwait(false))
                    return false;

                var take = (Int32) Math.Min(Count, end - start);

                if (Destination is not null)
                    await Destination.WriteAsync(buffer.AsMemory(start, take), CancellationToken).ConfigureAwait(false);

                start += take;
                Count -= take;

            }

            return true;

        }

    }

}
