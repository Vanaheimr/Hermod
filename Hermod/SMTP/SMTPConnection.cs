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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// One SMTP reply, all its lines (RFC 5321 §4.2.1).
    /// </summary>
    /// <param name="Lines">The lines as received, without their line ends.</param>
    internal sealed record SMTPReply(IReadOnlyList<String> Lines)
    {

        /// <summary>
        /// The reply code, from the final line; 0 when it has none.
        /// </summary>
        public Int32 Code
            => Lines.Count > 0 && Lines[^1].Length >= 3 && Int32.TryParse(Lines[^1].AsSpan(0, 3), out var code)
                   ? code
                   : 0;

        /// <summary>
        /// A 2yz, Positive Completion reply. RFC 5321 §4.2.1: the client reads a code by "the
        /// first digit only" - a 252 to MAIL is as good as a 250.
        /// </summary>
        public Boolean IsPositiveCompletion
            => Code is >= 200 and < 300;

        /// <summary>
        /// A 3yz, Positive Intermediate reply: a 350 to DATA is as good as a 354.
        /// </summary>
        public Boolean IsPositiveIntermediate
            => Code is >= 300 and < 400;

        /// <summary>
        /// The text of every line, without the codes, joined by a space.
        /// </summary>
        public String Text
            => String.Join(" ", Lines.Select(line => line.Length > 4 ? line[4..] : "").Where(text => text.Length > 0));

        /// <summary>
        /// "code text", as a one-line reply would read.
        /// </summary>
        public override String ToString()
            => Text.Length > 0 ? $"{Code} {Text}" : $"{Code}";

    }


    /// <summary>
    /// The I/O of one client session: commands written, replies read whole - however many lines
    /// they have and however TCP cuts them - and every read and write within its timeout.
    /// </summary>
    /// <remarks>
    /// A socket's ReceiveTimeout and SendTimeout do not apply to asynchronous I/O; the timeouts
    /// here are cancellation deadlines per operation, surfaced as a <see cref="TimeoutException"/>.
    /// </remarks>
    internal sealed class SMTPConnection(Stream    Stream,
                                         TimeSpan  ReadTimeout,
                                         TimeSpan  WriteTimeout)
    {

        // A reply line longer than this is not one (RFC 5321 §4.5.3.1.5 allows 512 octets).
        private const Int32 MaxLineLength  = 64 * 1024;

        // Nor is a reply with more lines than this.
        private const Int32 MaxReplyLines  = 1000;

        private          Stream  stream  = Stream;
        private readonly Byte[]  buffer  = new Byte[16 * 1024];
        private          Int32   start;
        private          Int32   end;

        /// <summary>
        /// The stream the session currently runs on.
        /// </summary>
        public Stream Stream
            => stream;

        /// <summary>
        /// Continue on another stream - the TLS stream after STARTTLS. Whatever was read and not
        /// yet taken is discarded: it arrived in cleartext (RFC 3207 §4.2).
        /// </summary>
        public void SwitchTo(Stream NewStream)
        {
            stream = NewStream;
            start  = 0;
            end    = 0;
        }


        /// <summary>
        /// Read one complete reply: lines up to the one without a '-' after its code.
        /// </summary>
        /// <param name="CancellationToken">A cancellation token.</param>
        /// <param name="Timeout">How long each read may take, when not the read timeout - the reply to the end of data has a longer one (RFC 5321 §4.5.3.2.6).</param>
        /// <exception cref="TimeoutException">The server did not send it within the read timeout.</exception>
        /// <exception cref="IOException">The connection closed first.</exception>
        public async Task<SMTPReply> ReadReplyAsync(CancellationToken  CancellationToken,
                                                    TimeSpan?          Timeout   = null)
        {

            var lines = new List<String>();

            while (true)
            {

                var line = await ReadLineAsync(Timeout ?? ReadTimeout, CancellationToken).ConfigureAwait(false)
                               ?? throw new IOException(lines.Count == 0
                                                            ? "The connection closed instead of a reply."
                                                            : "The connection closed in the middle of a reply.");

                lines.Add(line);

                if (line.Length < 4 || line[3] != '-')
                    return new SMTPReply(lines);

                if (lines.Count >= MaxReplyLines)
                    throw new IOException($"A reply of more than {MaxReplyLines} lines.");

            }

        }


        /// <summary>
        /// One line, without its CR LF (a bare LF is taken as a line end too: what the server says
        /// is read leniently); null when the connection closed before any octet of it.
        /// </summary>
        private async Task<String?> ReadLineAsync(TimeSpan Timeout, CancellationToken CancellationToken)
        {

            var line = new MemoryStream();

            while (true)
            {

                for (var i = start; i < end; i++)
                {
                    if (buffer[i] == (Byte) '\n')
                    {
                        line.Write(buffer, start, i - start);
                        start = i + 1;
                        var octets = line.ToArray();
                        var length = octets.Length > 0 && octets[^1] == (Byte) '\r' ? octets.Length - 1 : octets.Length;
                        return Encoding.UTF8.GetString(octets, 0, length);
                    }
                }

                line.Write(buffer, start, end - start);
                start = end;

                if (line.Length > MaxLineLength)
                    throw new IOException($"A reply line longer than {MaxLineLength} octets.");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                cts.CancelAfter(Timeout);

                try
                {
                    start = 0;
                    end   = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
                {
                    end = 0;
                    throw new TimeoutException($"No reply within {Timeout.TotalSeconds:0.#} s.");
                }

                if (end == 0)
                    return line.Length == 0
                               ? null
                               : throw new IOException("The connection closed in the middle of a line.");

            }

        }


        /// <summary>
        /// Write one command line; CR LF is appended.
        /// </summary>
        public Task WriteLineAsync(String Line, CancellationToken CancellationToken)
            => WriteAsync(Encoding.UTF8.GetBytes(Line + "\r\n"), CancellationToken);


        // RFC 5321 §4.5.3.2.5: the write timeout is "for each TCP SEND call transmitting a chunk of
        // data", so that "the overall timeout is inherently proportional to the size of the message".
        private const Int32 WriteBlockSize = 64 * 1024;

        /// <summary>
        /// Write octets as they are, each block of them within the write timeout.
        /// </summary>
        /// <exception cref="TimeoutException">The server did not take a block within the write timeout.</exception>
        public async Task WriteAsync(Byte[] Octets, CancellationToken CancellationToken)
        {

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            try
            {

                for (var offset = 0; offset < Octets.Length; offset += WriteBlockSize)
                {
                    cts.CancelAfter(WriteTimeout);
                    await stream.WriteAsync(Octets.AsMemory(offset, Math.Min(WriteBlockSize, Octets.Length - offset)), cts.Token).ConfigureAwait(false);
                }

                cts.CancelAfter(WriteTimeout);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);

            }
            catch (OperationCanceledException) when (!CancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Could not send within {WriteTimeout.TotalSeconds:0.#} s.");
            }

        }

    }

}
