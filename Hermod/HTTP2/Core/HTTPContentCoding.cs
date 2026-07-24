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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.IO.Compression;


    /// <summary>
    /// Content codings (RFC 9110, Section 8.4.1): gzip, deflate and br, in both
    /// directions.
    ///
    /// The encode direction is what a server applies to a response it is about to
    /// send (<see cref="HTTPSemantics"/>); the decode direction is what a client
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
        /// preference order a server uses when several are acceptable.
        /// </summary>
        public static readonly String[] Supported = ["br", "gzip", "deflate"];

        /// <summary>
        /// A ready-made <c>accept-encoding</c> field value advertising exactly
        /// <see cref="Supported"/>, in that order.
        /// </summary>
        public const String AcceptEncoding = "br, gzip, deflate";

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
                "gzip"    => new GZipStream   (input, CompressionMode.Decompress),
                "deflate" => OpenDeflate(input, Data),
                _         => throw new InvalidOperationException($"Unsupported content coding '{Coding}'")
            };

            using var output = new MemoryStream();

            var buffer = new Byte[81920];
            int read;

            // Copy manually rather than CopyTo: the cap has to bite *during*
            // decompression, or the bomb has already gone off by the time we look.
            while ((read = decompressor.Read(buffer, 0, buffer.Length)) > 0)
            {

                if (output.Length + read > MaxDecodedSize)
                    throw new InvalidDataException(
                        $"Decoded '{Coding}' content exceeds the {MaxDecodedSize}-byte limit");

                output.Write(buffer, 0, read);

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
