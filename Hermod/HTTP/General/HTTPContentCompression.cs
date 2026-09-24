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

    /// <summary>
    /// Compressing a response the client said it could decompress
    /// (RFC 9110, Sections 8.4 and 12.5.3) — for every handler at once, rather
    /// than one handler at a time.
    ///
    /// <see cref="SinglePageAppHandler"/> has done this for static files all
    /// along, and does it better: it compresses each file once at startup and
    /// serves the result, where this compresses per response. That is the
    /// trade a general filter makes, and it is why this one is off by default
    /// and skips anything small enough that the saving would not pay for the
    /// header it adds.
    ///
    /// The list of reasons *not* to compress is longer than the compressing,
    /// and each of them is a way to be wrong rather than merely inefficient.
    /// </summary>
    public static class HTTPContentCompression
    {

        #region Data

        /// <summary>
        /// Below this, compressing tends to cost more than it saves: a gzip
        /// member has some twenty bytes of framing of its own, a TCP segment
        /// holds well over a kilobyte, and the CPU is spent either way.
        /// </summary>
        public const UInt64 DefaultMinimumSize = 1024;

        #endregion


        #region (static) ShouldCompress(Request, Response, MinimumSize, out Coding)

        /// <summary>
        /// Whether this response may be compressed, and with what — separate from
        /// doing it, because every one of these conditions is a statement about
        /// RFC 9110 that is worth being able to test on its own.
        /// </summary>
        /// <param name="Request">The request this is an answer to.</param>
        /// <param name="Response">The response as the handler built it.</param>
        /// <param name="MinimumSize">Bodies below this are left alone.</param>
        /// <param name="Coding">The coding to apply, when this returns true.</param>
        public static Boolean ShouldCompress(HTTPRequest   Request,
                                             HTTPResponse  Response,
                                             UInt64        MinimumSize,
                                             out String?   Coding)
        {

            Coding = null;

            // Nothing to do, or nothing this filter can work on. A response whose
            // body is still a stream belongs to whoever is writing it — a live
            // chunked worker or an event source — and taking it over here would
            // break the framing they own.
            if (Response.HTTPBody is not Byte[] body || body.Length == 0)
                return false;

            if (Response.HTTPBodyStream is not null)
                return false;

            // A body the semantics forbid, or one already chunked: in the chunked
            // case the length must not be restated, and the trailers belong to the
            // stream rather than to us.
            if (Response.HTTPStatusCode.Code is >= 100 and < 200 or 204 or 205 or 304)
                return false;

            if (Response.IsChunkedTransferEncoding ||
                Response.AutomaticallyChunkContent)
                return false;

            // The handler already chose a coding — SinglePageAppHandler does, from
            // files it compressed once rather than per request.
            if (Response.ContentEncoding.Any())
                return false;

            // RFC 9110, Section 14.4: a 206 carries part of the *selected*
            // representation, and a content coding applies to the representation as
            // a whole. Compressing after ranging would describe neither.
            if (Response.HTTPStatusCode.Code == 206 ||
                Response.ContentRange is not null)
                return false;

            if (Response.ContentType is null ||
                !ContentNegotiation.IsCompressible(Response.ContentType))
                return false;

            if ((UInt64) body.LongLength < MinimumSize)
                return false;

            // RFC 9110, Section 12.5.3. Note that the server offers br and gzip
            // where the client accepts br, gzip and deflate: "deflate" is the one
            // the wire disagrees about, so we read it and do not write it.
            Coding = ContentNegotiation.SelectContentCoding(Request.AcceptEncoding);

            return Coding is not null;

        }

        #endregion

        #region (static) Apply(Request, Response, MinimumSize = DefaultMinimumSize)

        /// <summary>
        /// The given response, compressed if it should be and returned unchanged if
        /// it should not — including when compressing turned out not to help, which
        /// happens with content that is already compressed under a type that only
        /// looks textual.
        /// </summary>
        /// <param name="Request">The request this is an answer to.</param>
        /// <param name="Response">The response as the handler built it.</param>
        /// <param name="MinimumSize">Bodies below this are left alone.</param>
        public static HTTPResponse Apply(HTTPRequest   Request,
                                         HTTPResponse  Response,
                                         UInt64        MinimumSize   = DefaultMinimumSize)
        {

            if (!ShouldCompress(Request, Response, MinimumSize, out var coding) ||
                coding is null)
            {
                return Response;
            }

            var body       = Response.HTTPBody!;
            var compressed = HTTPContentCoding.Encode(body, coding);

            // Sending more bytes and calling them compressed helps nobody.
            if (compressed.LongLength >= body.LongLength)
                return Response;

            var builder = new HTTPResponse.Builder(
                              Response.HTTPRequest ?? Request,
                              Response.Timestamp,
                              Response.Runtime
                          ) {
                              HTTPStatusCode = Response.HTTPStatusCode
                          };

            // Everything the handler said, verbatim, and then the four fields that
            // stop being true once the octets change. Copying rather than mutating
            // because an HTTPResponse serialises from the header text it was built
            // with, so a field changed afterwards would reach the log and not the
            // wire.
            foreach (var field in Response)
                builder.Set(field.Key, field.Value);

            builder.ContentEncoding  = [ coding ];
            builder.Content          = compressed;
            builder.ContentLength    = (UInt64) compressed.LongLength;

            // RFC 9110, Section 12.5.5: the response now depends on a request
            // header, and a cache that does not know that will serve gzip to a
            // client that never asked for it.
            builder.Vary             = WithAcceptEncoding(Response.Vary);

            // RFC 9110, Section 8.8.3: the encoded and the identity form are
            // different representations, so a strong validator has to tell them
            // apart — otherwise a range request against a cached identity copy is
            // answered from the compressed one.
            if (Response.ETag is not null)
                builder.ETag         = ContentNegotiation.ETagForCoding(Response.ETag, coding);

            return builder.AsImmutable;

        }

        #endregion

        #region (static) WithAcceptEncoding(Vary)

        /// <summary>
        /// Add "Accept-Encoding" to a Vary field value without disturbing whatever
        /// was already in it — and without adding it twice.
        /// </summary>
        public static String WithAcceptEncoding(String? Vary)
        {

            if (Vary is null || Vary.Trim().Length == 0)
                return "Accept-Encoding";

            // "Vary: *" already says the response depends on everything.
            var fields = Vary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (fields.Any(field => field == "*" ||
                                    field.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase)))
            {
                return Vary;
            }

            return $"{Vary}, Accept-Encoding";

        }

        #endregion

    }

}
