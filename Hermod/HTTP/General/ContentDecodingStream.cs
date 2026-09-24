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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.IO.Compression;


    /// <summary>
    /// One content coding, undone as the bytes arrive — the streaming counterpart
    /// of <see cref="HTTPContentCoding.Decode(Byte[], String, Int64)"/>, and the
    /// place where the two awkward bits of reality about the BCL decoders are
    /// dealt with once instead of at every call site:
    ///
    /// <list type="number">
    ///   <item><b>"deflate" is two formats.</b> RFC 9110, Section 8.4.1.2 names the
    ///   zlib format (RFC 1950), and a good part of the web sends raw deflate
    ///   (RFC 1951) instead, which is the only one <c>DeflateStream</c> reads. The
    ///   array-shaped decoder just looks at the first two bytes; a stream cannot,
    ///   because when it is constructed those bytes have not necessarily arrived.
    ///   So the whole decision — for every coding, not only deflate — waits for the
    ///   first read, where waiting for two bytes is what the caller asked for.</item>
    ///   <item><b>The decoders disagree about their own exception.</b> gzip, zlib
    ///   and deflate raise <see cref="InvalidDataException"/> on octets that are not
    ///   valid for them; <c>BrotliStream</c> raises
    ///   <see cref="InvalidOperationException"/> for the identical condition. The
    ///   caller wants one answer to "this body is not what it said it was", so the
    ///   second is translated into the first.</item>
    /// </list>
    /// </summary>
    internal sealed class ContentDecodingStream : Stream
    {

        #region Data

        private readonly Stream   innerStream;
        private readonly String   coding;
        private readonly Boolean  leaveInnerStreamOpen;
        private          Stream?  decoder;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new stream undoing one content coding.
        /// </summary>
        /// <param name="InnerStream">The encoded octets.</param>
        /// <param name="Coding">One of <see cref="HTTPContentCoding.Supported"/>.</param>
        /// <param name="LeaveInnerStreamOpen">Whether disposing this stream leaves the inner stream open.</param>
        public ContentDecodingStream(Stream   InnerStream,
                                     String   Coding,
                                     Boolean  LeaveInnerStreamOpen = false)
        {

            this.innerStream           = InnerStream;
            this.coding                = Coding.Trim().ToLowerInvariant();
            this.leaveInnerStreamOpen  = LeaveInnerStreamOpen;

            if (!HTTPContentCoding.IsSupported(this.coding))
                throw new NotSupportedException($"Unsupported content coding '{Coding}'!");

        }

        #endregion

        #region Properties

        public override Boolean  CanRead
            => true;

        public override Boolean  CanSeek
            => false;

        public override Boolean  CanWrite
            => false;

        public override Int64    Length
            => throw new NotSupportedException();

        public override Int64    Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        #endregion


        #region (private) LooksLikeZLib(Prefix, Length)

        /// <summary>
        /// A zlib stream starts with CMF/FLG: the low nibble of CMF is the
        /// compression method, which must be 8, and the two bytes read as a
        /// big-endian 16-bit number are a multiple of 31. Raw deflate has no header
        /// at all, so this is the same test a browser applies.
        /// </summary>
        private static Boolean LooksLikeZLib(Byte[]  Prefix,
                                             Int32   Length)

            => Length >= 2 &&
               (Prefix[0] & 0x0F) == 8 &&
               ((Prefix[0] << 8) | Prefix[1]) % 31 == 0;

        #endregion

        #region (private) Wrap(Prefix, Length)

        private Stream Wrap(Byte[]  Prefix,
                            Int32   Length)
        {

            // The sniffed bytes go straight back in front of the source, so nothing
            // of the body is lost by having looked at it.
            var source = new PrefixStream(
                             Prefix.AsMemory(0, Length),
                             innerStream,
                             LeaveInnerStreamOpen: true
                         );

            return coding switch {
                "br"       => new BrotliStream (source, CompressionMode.Decompress),
                "gzip"     => new GZipStream   (source, CompressionMode.Decompress),
                "deflate"  => LooksLikeZLib(Prefix, Length)
                                  ? new ZLibStream   (source, CompressionMode.Decompress)
                                  : new DeflateStream(source, CompressionMode.Decompress),
                _          => throw new NotSupportedException($"Unsupported content coding '{coding}'!")
            };

        }

        #endregion

        #region (private) EnsureDecoder()

        private Stream EnsureDecoder()
        {

            if (decoder is not null)
                return decoder;

            var prefix  = new Byte[2];
            var length  = 0;

            while (length < 2)
            {

                var read = innerStream.Read(prefix, length, 2 - length);

                if (read <= 0)
                    break;

                length += read;

            }

            return decoder = Wrap(prefix, length);

        }

        private async ValueTask<Stream> EnsureDecoderAsync(CancellationToken CancellationToken)
        {

            if (decoder is not null)
                return decoder;

            var prefix  = new Byte[2];
            var length  = 0;

            while (length < 2)
            {

                var read = await innerStream.ReadAsync(
                                     prefix.AsMemory(length, 2 - length),
                                     CancellationToken
                                 ).ConfigureAwait(false);

                if (read <= 0)
                    break;

                length += read;

            }

            return decoder = Wrap(prefix, length);

        }

        #endregion

        #region Read(...) / ReadAsync(...)

        public override Int32 Read(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
        {

            try
            {
                return EnsureDecoder().Read(Buffer, Offset, Count);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidDataException($"The body is not valid '{coding}': {e.Message}", e);
            }

        }


        public override async ValueTask<Int32> ReadAsync(Memory<Byte>       Buffer,
                                                         CancellationToken  CancellationToken = default)
        {

            try
            {

                var currentDecoder = await EnsureDecoderAsync(CancellationToken).ConfigureAwait(false);

                return await currentDecoder.ReadAsync(
                                 Buffer,
                                 CancellationToken
                             ).ConfigureAwait(false);

            }
            catch (InvalidOperationException e)
            {
                throw new InvalidDataException($"The body is not valid '{coding}': {e.Message}", e);
            }

        }

        public override Task<Int32> ReadAsync(Byte[]             Buffer,
                                              Int32              Offset,
                                              Int32              Count,
                                              CancellationToken  CancellationToken)

            => ReadAsync(Buffer.AsMemory(Offset, Count), CancellationToken).AsTask();

        #endregion

        #region (unsupported write side)

        public override void Flush()
        { }

        public override Task FlushAsync(CancellationToken CancellationToken)
            => Task.CompletedTask;

        public override void Write(Byte[]  Buffer,
                                   Int32   Offset,
                                   Int32   Count)
            => throw new NotSupportedException();

        public override Int64 Seek(Int64       Offset,
                                   SeekOrigin  Origin)
            => throw new NotSupportedException();

        public override void SetLength(Int64 Value)
            => throw new NotSupportedException();

        #endregion

        #region Dispose(Disposing)

        protected override void Dispose(Boolean Disposing)
        {

            if (Disposing)
            {

                // The decoder owns the PrefixStream it was given, and that one was
                // told to leave the inner stream alone — so whether the inner
                // stream is closed stays this stream's decision, decoder or not.
                decoder?.Dispose();

                if (!leaveInnerStreamOpen)
                    innerStream.Dispose();

            }

            base.Dispose(Disposing);

        }

        #endregion

    }

}
