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

using System.IO.Compression;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.WebSocket
{

    /// <summary>
    /// A "permessage-deflate" WebSocket extension (RFC 7692) for a single connection.
    ///
    /// Note on limitations: .NET's DeflateStream does not expose the DEFLATE window
    /// size (LZ77 sliding window), so only the maximum window (15 bits) is supported
    /// and the compression context is not carried over between messages. This
    /// implementation therefore always negotiates "no_context_takeover" in both
    /// directions, which RFC 7692 explicitly permits.
    /// </summary>
    public sealed class WebSocketPerMessageDeflate
    {

        #region Data

        /// <summary>
        /// The DEFLATE sync flush trailer (an empty non-compressed block, RFC 7692 Section 7.2.1).
        /// </summary>
        private static readonly Byte[] SyncFlushTail = [ 0x00, 0x00, 0xff, 0xff ];

        #endregion

        #region Properties

        /// <summary>
        /// The negotiated compression level for outgoing messages.
        /// </summary>
        public CompressionLevel  CompressionLevel    { get; }

        /// <summary>
        /// The maximum allowed size of a decompressed message, to guard against
        /// decompression bombs. Null means the frame parser's default limit.
        /// </summary>
        public UInt64?           MaxDecompressedSize    { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new "permessage-deflate" extension instance.
        /// </summary>
        /// <param name="CompressionLevel">The compression level for outgoing messages.</param>
        public WebSocketPerMessageDeflate(CompressionLevel CompressionLevel = CompressionLevel.Optimal)
        {
            this.CompressionLevel = CompressionLevel;
        }

        #endregion


        #region Compress  (Payload)

        /// <summary>
        /// Compress the given message payload (RFC 7692 Section 7.2.1).
        /// </summary>
        /// <param name="Payload">The uncompressed message payload.</param>
        /// <returns>The compressed message payload (without the trailing sync flush marker).</returns>
        public Byte[] Compress(Byte[] Payload)
        {

            var output   = new MemoryStream();

            // The DEFLATE stream must end with a sync flush (an empty non-compressed
            // block, producing the trailing '0x00 0x00 0xff 0xff'), NOT a final block:
            // A final block (BFINAL=1, written by Dispose) would terminate a peer's
            // persistent inflater when context takeover is in use (RFC 7692 Section 7.2.1).
            // Therefore we Flush (sync flush) and capture the output BEFORE disposing.
            var deflate  = new DeflateStream(output, CompressionLevel, leaveOpen: true);
            deflate.Write(Payload, 0, Payload.Length);
            deflate.Flush();

            var compressed = output.ToArray();

            deflate.Dispose();
            output.Dispose();

            // RFC 7692 Section 7.2.1: If the compressed data ends with the empty
            // DEFLATE block '0x00 0x00 0xff 0xff', those 4 octets are removed.
            if (compressed.Length >= 4 &&
                compressed[^4] == 0x00 &&
                compressed[^3] == 0x00 &&
                compressed[^2] == 0xff &&
                compressed[^1] == 0xff)
            {
                Array.Resize(ref compressed, compressed.Length - 4);
            }

            // A compressed empty message must not be empty on the wire; RFC 7692
            // Section 7.2.3.6 represents it as a single octet 0x00.
            if (compressed.Length == 0)
                compressed = [ 0x00 ];

            return compressed;

        }

        #endregion

        #region Decompress(Payload, out ErrorResponse)

        /// <summary>
        /// Decompress the given compressed message payload (RFC 7692 Section 7.2.2).
        /// </summary>
        /// <param name="Payload">The compressed message payload.</param>
        /// <param name="Decompressed">The decompressed message payload.</param>
        /// <param name="ErrorResponse">An error response in case of a decompression failure or a size limit violation.</param>
        /// <param name="ExceededSizeLimit">Whether the failure was a size limit violation (close with 1009 Message Too Big) rather than corrupt/invalid compressed data (close with 1007 Invalid Payload Data).</param>
        public Boolean TryDecompress(Byte[]                Payload,
                                     out Byte[]            Decompressed,
                                     out String?           ErrorResponse,
                                     out Boolean           ExceededSizeLimit)
        {

            Decompressed       = [];
            ErrorResponse      = null;
            ExceededSizeLimit  = false;

            try
            {

                using var input = new MemoryStream(Payload.Length + 4);
                input.Write(Payload,       0, Payload.Length);
                // RFC 7692 Section 7.2.2: Append the sync flush marker before inflating.
                input.Write(SyncFlushTail, 0, SyncFlushTail.Length);
                input.Position = 0;

                using var deflate  = new DeflateStream(input, CompressionMode.Decompress);
                using var output   = new MemoryStream();

                var maxSize   = MaxDecompressedSize ?? WebSocketFrame.DefaultMaxPayloadSize;
                var buffer    = new Byte[16 * 1024];
                var total     = 0UL;
                int read;

                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {

                    total += (UInt64) read;

                    // Guard against decompression bombs!
                    if (total > maxSize)
                    {
                        ErrorResponse      = $"The decompressed message exceeds the maximum allowed size of {maxSize} bytes!";
                        ExceededSizeLimit  = true;
                        return false;
                    }

                    output.Write(buffer, 0, read);

                }

                Decompressed = output.ToArray();
                return true;

            }
            catch (Exception e)
            {
                // Corrupt or invalid DEFLATE data (RFC 7692 Section 8: close with 1007).
                ErrorResponse = $"The compressed message could not be decompressed: {e.Message}";
                return false;
            }

        }

        #endregion


        #region (static) TryNegotiate(OfferedExtensions, out Deflate, out ResponseHeader)

        /// <summary>
        /// Server-side negotiation: Given the value of a client's 'Sec-WebSocket-Extensions'
        /// request header, decide whether to enable "permessage-deflate".
        /// </summary>
        /// <param name="OfferedExtensions">The value of the client's 'Sec-WebSocket-Extensions' header (may be null).</param>
        /// <param name="Deflate">The negotiated extension instance, if enabled.</param>
        /// <param name="ResponseHeader">The value for the server's 'Sec-WebSocket-Extensions' response header, if enabled.</param>
        /// <param name="CompressionLevel">The compression level to use for outgoing messages.</param>
        public static Boolean TryNegotiateAsServer(String?                              OfferedExtensions,
                                                   out WebSocketPerMessageDeflate?      Deflate,
                                                   out String?                          ResponseHeader,
                                                   CompressionLevel                     CompressionLevel = CompressionLevel.Optimal)
        {

            Deflate         = null;
            ResponseHeader  = null;

            if (OfferedExtensions.IsNullOrEmpty())
                return false;

            // A client may offer multiple extensions / multiple permessage-deflate
            // variants, separated by commas. We accept the first permessage-deflate
            // offer whose parameters we can honour.
            foreach (var offer in OfferedExtensions!.Split(','))
            {

                var parameters  = offer.Split(';').Select(p => p.Trim()).ToArray();

                if (parameters.Length == 0 ||
                    !parameters[0].Equals("permessage-deflate", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // We can only honour the maximum window (15 bits). If the client
                // requires the *server* to use a smaller window, we cannot comply
                // (DeflateStream does not expose the window size), so skip this offer.
                // Per RFC 7692 Section 7.1.2.1 a 'server_max_window_bits' offer MUST
                // carry a value of 8..15; a missing or unparseable value is malformed
                // and treated as un-honourable rather than silently ignored.
                var canHonourOffer = true;

                foreach (var parameter in parameters.Skip(1))
                {

                    var kv     = parameter.Split('=', 2);
                    var name   = kv[0].Trim();
                    var value  = kv.Length > 1 ? kv[1].Trim().Trim('"') : null;

                    if (name.Equals("server_max_window_bits", StringComparison.OrdinalIgnoreCase) &&
                        (value is null ||
                         !Byte.TryParse(value, out var bits) ||
                         bits != 15))
                    {
                        canHonourOffer = false;
                    }

                }

                if (!canHonourOffer)
                    continue;

                // Accept the offer, always forcing no_context_takeover in both directions.
                Deflate         = new WebSocketPerMessageDeflate(CompressionLevel);
                ResponseHeader  = "permessage-deflate; client_no_context_takeover; server_no_context_takeover";
                return true;

            }

            return false;

        }

        #endregion

        #region (static) ClientOfferHeader

        /// <summary>
        /// The 'Sec-WebSocket-Extensions' header value a client should offer.
        /// </summary>
        public static String ClientOfferHeader
            => "permessage-deflate; client_no_context_takeover; server_no_context_takeover";

        #endregion

        #region (static) ServerAcceptedDeflate(ResponseExtensions)

        /// <summary>
        /// Client-side negotiation: Given the value of the server's 'Sec-WebSocket-Extensions'
        /// response header, decide whether "permessage-deflate" was enabled.
        /// </summary>
        /// <param name="ResponseExtensions">The value of the server's 'Sec-WebSocket-Extensions' header (may be null).</param>
        /// <param name="CompressionLevel">The compression level to use for outgoing messages.</param>
        public static WebSocketPerMessageDeflate? ServerAcceptedDeflate(String?           ResponseExtensions,
                                                                        CompressionLevel  CompressionLevel = CompressionLevel.Optimal)
        {

            if (ResponseExtensions.IsNullOrEmpty())
                return null;

            foreach (var extension in ResponseExtensions!.Split(','))
            {

                var parameters  = extension.Split(';').Select(p => p.Trim()).ToArray();

                if (parameters.Length == 0 ||
                    !parameters[0].Equals("permessage-deflate", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // We always compress with the full 15-bit window. A response that
                // requires a smaller *client* window (client_max_window_bits < 15)
                // cannot be satisfied, so the extension is unusable. (A smaller
                // *server* window is fine: our 15-bit inflater decodes it.)
                // Note: our offer never includes 'client_max_window_bits', so a
                // conforming server (RFC 7692 Section 7.1.2.2) will not send it;
                // this only guards against a misbehaving server. If we decline
                // here while the server enabled the extension, the first compressed
                // (RSV1) frame will fail the connection cleanly.
                var canHonour = true;

                foreach (var parameter in parameters.Skip(1))
                {

                    var kv     = parameter.Split('=', 2);
                    var name   = kv[0].Trim();
                    var value  = kv.Length > 1 ? kv[1].Trim().Trim('"') : null;

                    if (name.Equals("client_max_window_bits", StringComparison.OrdinalIgnoreCase) &&
                        value is not null &&
                        Byte.TryParse(value, out var bits) &&
                        bits < 15)
                    {
                        canHonour = false;
                    }

                }

                return canHonour
                           ? new WebSocketPerMessageDeflate(CompressionLevel)
                           : null;

            }

            return null;

        }

        #endregion

    }

}
