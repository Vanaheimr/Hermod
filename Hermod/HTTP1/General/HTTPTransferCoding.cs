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

using System.Diagnostics.CodeAnalysis;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The transfer codings other than chunked (RFC 9112, Section 7): what a
    /// recipient has to undo below the framing before the octets are the
    /// message's content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A transfer coding is a property of the message, not of the
    /// representation, and "Transfer-Encoding: gzip, chunked" means the
    /// content was gzipped and the result chunked. A recipient that removes
    /// the chunks and stops there hands on gzip as if it were the content.
    /// Until 2026-11-27 the server did exactly that, for any coding at all in
    /// front of chunked, a made-up one included.
    /// </para>
    /// <para>
    /// gzip and deflate are undone, with the same decoders and the same limits
    /// as the content codings, and x-gzip is gzip (Section 7.2). compress
    /// (Section 7.2) is not: LZW has no decoder in the BCL, and nothing sends
    /// it. A request with a coding the server cannot undo is answered 501, as
    /// Section 6.1 has it, and a response with one is refused by the client.
    /// Brotli and zstd are content codings only; the Transfer Coding Registry
    /// does not have them.
    /// </para>
    /// </remarks>
    public static class HTTPTransferCoding
    {

        #region Data

        /// <summary>
        /// The transfer codings other than chunked that this stack undoes.
        /// </summary>
        public static readonly String[] Decodable = [ "gzip", "x-gzip", "deflate" ];

        #endregion


        #region IsDecodable(Coding)

        /// <summary>
        /// Whether the given transfer coding is one this stack undoes.
        /// </summary>
        /// <param name="Coding">A transfer coding.</param>
        public static Boolean IsDecodable(String Coding)

            => Decodable.Contains(Coding.Trim(), StringComparer.OrdinalIgnoreCase);

        #endregion

        #region TryGetCodingsToUndo(TransferCodings, out Codings, out Unsupported)

        /// <summary>
        /// The codings to undo below the framing: every coding a message
        /// declares but a final chunked, in the order they were applied, with
        /// x-gzip as gzip. False with the first coding that cannot be undone.
        /// </summary>
        /// <param name="TransferCodings">The transfer codings, as the message declares them.</param>
        /// <param name="Codings">The codings to undo, in the order the sender applied them.</param>
        /// <param name="Unsupported">The first coding this stack cannot undo.</param>
        public static Boolean TryGetCodingsToUndo(IEnumerable<String>                        TransferCodings,
                                                  out String[]                               Codings,
                                                  [NotNullWhen(false)] out String?           Unsupported)
        {

            var codings = TransferCodings.Select(coding => coding.Trim()).ToList();

            if (codings.Count > 0 &&
                codings[^1].Equals("chunked", StringComparison.OrdinalIgnoreCase))
            {
                codings.RemoveAt(codings.Count - 1);
            }

            Unsupported = codings.FirstOrDefault(coding => !IsDecodable(coding));

            Codings     = Unsupported is null
                              ? [.. codings.Select(coding => coding.Equals("x-gzip", StringComparison.OrdinalIgnoreCase)
                                                                ? "gzip"
                                                                : coding.ToLowerInvariant())]
                              : [];

            return Unsupported is null;

        }

        #endregion

        #region DecodeStream(Source, Codings, MaxDecodedSize, LeaveSourceOpen)

        /// <summary>
        /// The given octets with the given transfer codings undone, as a stream:
        /// the last applied undone first, and every step held to the given size.
        /// </summary>
        /// <param name="Source">The octets, with the framing already removed.</param>
        /// <param name="Codings">The codings to undo, in the order the sender applied them (see <see cref="TryGetCodingsToUndo"/>).</param>
        /// <param name="MaxDecodedSize">Hard ceiling on the output of every single step.</param>
        /// <param name="LeaveSourceOpen">Whether disposing the returned stream leaves <paramref name="Source"/> open.</param>
        /// <exception cref="HTTPInvalidTransferCodingException">Thrown by a read when the octets do not decode.</exception>
        /// <exception cref="HTTPBodyTooLargeException">Thrown by a read when a step's output exceeds <paramref name="MaxDecodedSize"/>.</exception>
        public static Stream DecodeStream(Stream    Source,
                                          String[]  Codings,
                                          UInt64    MaxDecodedSize,
                                          Boolean   LeaveSourceOpen)

            => new DecoderFailureStream(
                   HTTPContentCoding.DecodeStream(
                       Source,
                       Codings,
                       MaxDecodedSize,
                       LeaveSourceOpen
                   ),
                   String.Join(", ", Codings)
               );

        #endregion

        #region Decode(Data, Codings, MaxDecodedSize)

        /// <summary>
        /// The given octets with the given transfer codings undone.
        /// </summary>
        /// <param name="Data">The octets, with the framing already removed.</param>
        /// <param name="Codings">The codings to undo, in the order the sender applied them.</param>
        /// <param name="MaxDecodedSize">Hard ceiling on the output of every single step.</param>
        /// <exception cref="HTTPInvalidTransferCodingException">The octets do not decode.</exception>
        /// <exception cref="HTTPBodyTooLargeException">A step's output exceeds <paramref name="MaxDecodedSize"/>.</exception>
        public static Byte[] Decode(Byte[]    Data,
                                    String[]  Codings,
                                    UInt64    MaxDecodedSize)
        {

            using var decoded = DecodeStream(new MemoryStream(Data), Codings, MaxDecodedSize, LeaveSourceOpen: false);
            using var output  = new MemoryStream();

            decoded.CopyTo(output);

            return output.ToArray();

        }

        #endregion


        #region (private class) DecoderFailureStream

        /// <summary>
        /// A decoding pipeline whose decoder failures are reported as what they
        /// are on the wire: a message body that is not what its transfer codings
        /// say, which the server answers 400.
        /// </summary>
        private sealed class DecoderFailureStream(Stream  Inner,
                                                  String  Codings) : Stream
        {

            public override Boolean CanRead   => true;
            public override Boolean CanSeek   => false;
            public override Boolean CanWrite  => false;

            public override Int64   Length    => throw new NotSupportedException();

            public override Int64   Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override Int32 Read(Byte[] Buffer, Int32 Offset, Int32 Count)
            {
                try
                {
                    return Inner.Read(Buffer, Offset, Count);
                }
                catch (InvalidDataException e)
                {
                    throw Failure(e);
                }
            }

            public override async ValueTask<Int32> ReadAsync(Memory<Byte> Buffer, CancellationToken CancellationToken = default)
            {
                try
                {
                    return await Inner.ReadAsync(Buffer, CancellationToken).ConfigureAwait(false);
                }
                catch (InvalidDataException e)
                {
                    throw Failure(e);
                }
            }

            public override Task<Int32> ReadAsync(Byte[] Buffer, Int32 Offset, Int32 Count, CancellationToken CancellationToken)

                => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

            private HTTPInvalidTransferCodingException Failure(InvalidDataException Exception)

                => new ($"The message body is not valid '{Codings}' transfer coding: {Exception.Message}", Exception);

            public override void   Flush() { }
            public override Int64  Seek(Int64 Offset, SeekOrigin Origin)            => throw new NotSupportedException();
            public override void   SetLength(Int64 Value)                           => throw new NotSupportedException();
            public override void   Write(Byte[] Buffer, Int32 Offset, Int32 Count)  => throw new NotSupportedException();

            protected override void Dispose(Boolean Disposing)
            {
                if (Disposing)
                    Inner.Dispose();
                base.Dispose(Disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                await Inner.DisposeAsync().ConfigureAwait(false);
                await base.DisposeAsync().ConfigureAwait(false);
            }

        }

        #endregion

    }

}
