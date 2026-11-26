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
    /// Content codings (RFC 9110, Section 8.4.1): gzip, deflate, br and zstd
    /// (RFC 8878, held to RFC 9659 for HTTP), in both directions.
    ///
    /// The encode direction is what a server applies to a response it is about to
    /// send (<see cref="HTTPResourceSemantics"/>); the decode direction is what a client
    /// applies to a response it has just received. They are the same three
    /// algorithms, so they live together here rather than once per role — the
    /// codings a peer may send us are exactly the codings we can produce.
    ///
    /// Decoding is the dangerous direction, and the reason this class insists on a
    /// size limit: a few kilobytes of compressed input can expand to gigabytes
    /// ("decompression bomb"). <see cref="Decode"/> therefore never trusts the
    /// declared length and stops the moment the *decoded* output exceeds the cap,
    /// rather than decompressing first and checking afterwards.
    /// </summary>
    public static class HTTPContentCoding
    {

        #region Data

        /// <summary>
        /// The codings this stack can produce and consume, best first. Also the
        /// preference order a server uses when several are acceptable: br before
        /// zstd, so that a client that was answered with br before zstd was
        /// offered still is.
        /// </summary>
        public static readonly String[] Supported = ["br", "zstd", "gzip", "deflate"];

        /// <summary>
        /// A ready-made <c>accept-encoding</c> field value advertising exactly
        /// <see cref="Supported"/>, in that order.
        /// </summary>
        public const String AcceptEncoding = "br, zstd, gzip, deflate";

        /// <summary>
        /// The largest window a zstd frame may need, as a power of two: 8 MB.
        /// RFC 9659, Section 3: an encoder MUST NOT use a larger window for HTTP,
        /// and a decoder is not obliged to take one. The decoder here takes none,
        /// so a frame cannot make it hold more than 8 MB of history per message.
        /// </summary>
        public const Int32 ZstandardMaxWindowLog2 = 23;

        /// <summary>
        /// The name of the assembly the BCL's decoders live in: an IOException
        /// thrown from there is a decoder's own, not one of the stream it reads.
        /// </summary>
        private static readonly String? CompressionAssembly = typeof(ZstandardStream).Assembly.GetName().Name;

        #endregion


        #region IsSupported (Coding)

        /// <summary>
        /// Whether a coding token names something we can actually decode.
        /// <c>identity</c> is deliberately *not* included: it means "no coding
        /// applied", which is handled by not decoding at all.
        /// </summary>
        public static Boolean IsSupported(String Coding)

            => Supported.Contains(Coding.Trim(), StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Encode (Data, Coding)

        /// <summary>
        /// Apply one content coding.
        /// </summary>
        /// <param name="Data">The identity representation.</param>
        /// <param name="Coding">One of <see cref="Supported"/>.</param>
        public static Byte[] Encode(Byte[] Data, String Coding)
        {

            using var output = new MemoryStream();

            using (Stream compressor = Coding switch
            {
                "br"      => new BrotliStream (output, CompressionLevel.Optimal, leaveOpen: true),
                "zstd"    => ZstandardEncoder(output, Data.LongLength),
                "gzip"    => new GZipStream   (output, CompressionLevel.Optimal, leaveOpen: true),
                "deflate" => new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true),
                _         => throw new InvalidOperationException($"Unsupported content coding '{Coding}'")
            })
                compressor.Write(Data, 0, Data.Length);

            return output.ToArray();

        }

        #endregion

        #region Decode (Data, Coding, MaxDecodedSize)

        /// <summary>
        /// Reverse one content coding, refusing to produce more than
        /// <paramref name="MaxDecodedSize"/> bytes.
        /// </summary>
        /// <param name="Data">The encoded octets as received.</param>
        /// <param name="Coding">One of <see cref="Supported"/>.</param>
        /// <param name="MaxDecodedSize">Hard ceiling on the decoded size.</param>
        /// <exception cref="InvalidDataException">
        /// The decoded content exceeds <paramref name="MaxDecodedSize"/>, or the
        /// encoded octets are not valid for this coding.
        /// </exception>
        public static Byte[] Decode(Byte[] Data, String Coding, Int64 MaxDecodedSize)
        {

            using var input = new MemoryStream(Data, writable: false);

            using Stream decompressor = Coding switch
            {
                "br"      => new BrotliStream (input, CompressionMode.Decompress),
                "zstd"    => ZstandardDecoder(input),
                "gzip"    => new GZipStream   (input, CompressionMode.Decompress),
                "deflate" => OpenDeflate(input, Data),
                _         => throw new InvalidOperationException($"Unsupported content coding '{Coding}'")
            };

            using var output = new MemoryStream();

            var buffer = new Byte[81920];
            int read;

            try
            {

                // Copy manually rather than CopyTo: the cap has to bite *during*
                // decompression, or the bomb has already gone off by the time we look.
                while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0)
                {

                    if (output.Length + read > MaxDecodedSize)
                        throw new InvalidDataException(
                            $"Decoded '{Coding}' content exceeds the {MaxDecodedSize}-byte limit");

                    output.Write(buffer, 0, read);

                }

            }
            catch (IOException e) when (IsDecoderFailure(e))
            {
                throw new InvalidDataException($"The '{Coding}' content cannot be decoded: {e.Message}", e);
            }

            return output.ToArray();

        }

        /// <summary>
        /// "deflate" is the one coding the wire disagrees about: RFC 9110 names
        /// RFC 1950 (zlib-wrapped), but plenty of servers send RFC 1951 (raw) —
        /// .NET's <see cref="DeflateStream"/> reads only the latter. Sniff the
        /// two-byte zlib header (CMF/FLG, whose big-endian value is a multiple of
        /// 31, with compression method 8) and pick accordingly, the same way
        /// browsers cope with it.
        /// </summary>
        private static Stream OpenDeflate(MemoryStream Input, Byte[] Data)
        {

            var looksLikeZLib = Data.Length >= 2 &&
                                (Data[0] & 0x0F) == 8 &&
                                ((Data[0] << 8) | Data[1]) % 31 == 0;

            return looksLikeZLib
                       ? new ZLibStream  (Input, CompressionMode.Decompress)
                       : new DeflateStream(Input, CompressionMode.Decompress);

        }

        #endregion

        #region (internal) ZstandardEncoder(Output, SourceLength)

        /// <summary>
        /// A zstd encoder (RFC 8878) of zstd's own default quality, 3, writing a
        /// frame that declares its content size: a decoder then knows a frame of
        /// a small body needs no window beyond that body.
        ///
        /// The quality is set, not taken from a <see cref="CompressionLevel"/>: the
        /// window follows from it, and RFC 9659 holds HTTP to 8 MB. Quality 3 uses
        /// 2 MB; <see cref="CompressionLevel.SmallestSize"/> would use 128 MB, which
        /// a decoder that keeps to RFC 9659 refuses.
        /// </summary>
        internal static ZstandardStream ZstandardEncoder(Stream  Output,
                                                         Int64   SourceLength)
        {

            var encoder = new ZstandardStream(
                              Output,
                              new ZstandardCompressionOptions {
                                  Quality = ZstandardCompressionOptions.DefaultQuality
                              },
                              leaveOpen: true
                          );

            encoder.SetSourceLength(SourceLength);

            return encoder;

        }

        #endregion

        #region (internal) ZstandardDecoder(Input)

        /// <summary>
        /// A zstd decoder that takes no frame needing a window above 8 MB
        /// (<see cref="ZstandardMaxWindowLog2"/>), and disposes its input with it.
        /// </summary>
        internal static ZstandardStream ZstandardDecoder(Stream Input)

            => new (Input,
                    new ZstandardDecompressionOptions {
                        MaxWindowLog2 = ZstandardMaxWindowLog2
                    },
                    leaveOpen: false);

        #endregion

        #region (internal) IsDecoderFailure(Exception)

        /// <summary>
        /// Whether an IOException is a decoder's verdict on the octets rather than
        /// a failure of the stream they are read from. ZstandardStream answers a
        /// frame whose window is over its limit with an IOException, which a
        /// caller would take for the connection failing; one the source throws
        /// goes through a decoder as it is, and keeps its own Source.
        /// </summary>
        internal static Boolean IsDecoderFailure(IOException Exception)

            => Exception.Source is not null &&
               Exception.Source == CompressionAssembly;

        #endregion

        #region DecodeStream (Source, Codings, MaxDecodedSize, LeaveSourceOpen = true)

        /// <summary>
        /// The same reversal as <see cref="Decode"/>, but as a pipeline over a
        /// stream rather than over a byte array — for the bodies that are never a
        /// byte array in the first place: chunked, close-delimited, and the event
        /// streams that are supposed to arrive a piece at a time.
        ///
        /// <c>Content-Encoding: gzip, br</c> means gzip was applied first and
        /// Brotli to its output, so the wire has to be un-Brotli'd before it can be
        /// un-gzipped: the list is walked backwards, exactly as in <see cref="Decode"/>,
        /// and each step wraps the one before it.
        ///
        /// Every step is bounded separately, not just the last one. A bomb in an
        /// intermediate coding would otherwise be invisible to a cap on the final
        /// output: two kilobytes of gzip can expand to a gigabyte that the next
        /// coding compresses back down to nothing the caller ever sees, while the
        /// gigabyte is very much real.
        /// </summary>
        /// <param name="Source">The encoded octets, as they arrive.</param>
        /// <param name="Codings">The content codings, in the order the sender applied them.</param>
        /// <param name="MaxDecodedSize">Hard ceiling on the output of every single step.</param>
        /// <param name="LeaveSourceOpen">Whether disposing the returned stream leaves <paramref name="Source"/> open. The rest of the pipeline is always disposed with it.</param>
        /// <exception cref="NotSupportedException">A coding is not one this stack can undo. Nothing is wrapped in that case.</exception>
        public static Stream DecodeStream(Stream               Source,
                                          IEnumerable<String>  Codings,
                                          UInt64               MaxDecodedSize,
                                          Boolean              LeaveSourceOpen   = true)
        {

            var codings = Codings.Select(coding => coding.Trim()).
                                  Where (coding => !coding.Equals("identity", StringComparison.OrdinalIgnoreCase)).
                                  ToArray();

            foreach (var coding in codings)
            {
                if (!IsSupported(coding))
                    throw new NotSupportedException($"Unsupported content coding '{coding}'!");
            }

            var stream = Source;

            for (var i = codings.Length - 1; i >= 0; i--)
            {

                // Only the innermost decoder touches Source, so it is the only one
                // that gets a say in whether Source survives being disposed.
                var leaveOpen = i == codings.Length - 1 && LeaveSourceOpen;

                stream = new MaximumLengthStream(
                             new ContentDecodingStream(stream, codings[i], leaveOpen),
                             MaxDecodedSize,
                             LeaveInnerStreamOpen: false
                         );

            }

            return stream;

        }

        #endregion

        #region DecodeBody (Headers, Body, MaxDecodedSize)

        /// <summary>
        /// Undo whatever <c>content-encoding</c> a received message declares,
        /// returning the identity representation and the coding(s) that were
        /// removed (null if there was nothing to do).
        ///
        /// <c>Content-Encoding</c> is a *list*, applied left to right by the sender,
        /// so it is undone right to left (RFC 9110, Section 8.4). A coding we do not
        /// know stops the process: the message is handed back exactly as received,
        /// header intact, because a body we cannot decode is still a body the caller
        /// may want — silently returning compressed bytes labelled as identity would
        /// be worse than doing nothing.
        ///
        /// On success <c>content-encoding</c> is removed (it no longer describes the
        /// bytes) and <c>content-length</c> is rewritten to the decoded length rather
        /// than deleted: the body is fully buffered here, so a correct length is
        /// strictly more useful than none, and leaving the transferred length in
        /// place would simply be false.
        /// </summary>
        /// <param name="Headers">The received field lines; modified in place on success.</param>
        /// <param name="Body">The received (possibly encoded) octets.</param>
        /// <param name="MaxDecodedSize">Hard ceiling on the decoded size, at every step.</param>
        public static (Byte[] Body, String? Decoded) DecodeBody(List<(String Name, String Value)> Headers,
                                                                Byte[]                            Body,
                                                                Int64                             MaxDecodedSize)
        {

            var index = Headers.FindIndex(header => header.Name == "content-encoding");

            if (index < 0 || Body.Length == 0)
                return (Body, null);

            var codings = Headers[index].Value
                              .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .Select(coding => coding.ToLowerInvariant())
                              .Where (coding => coding != "identity")
                              .ToArray();

            if (codings.Length == 0)
            {
                // "identity" only — nothing was applied, so nothing to undo, but the
                // header is still noise once we say the body is identity.
                Headers.RemoveAt(index);
                return (Body, null);
            }

            if (codings.Any(coding => !IsSupported(coding)))
                return (Body, null);

            var decoded = Body;

            for (var i = codings.Length - 1; i >= 0; i--)
                decoded = Decode(decoded, codings[i], MaxDecodedSize);

            Headers.RemoveAt(index);
            Headers.RemoveAll(header => header.Name == "content-length");
            Headers.Add(("content-length", decoded.Length.ToString()));

            return (decoded, String.Join(", ", codings));

        }

        #endregion

    }

}
