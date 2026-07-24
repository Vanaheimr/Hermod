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

    /// <summary>
    /// Where a redirect response points, and what the follow-up request should look
    /// like once the RFC 9110, Section 15.4 rewriting rules have been applied.
    /// </summary>
    /// <param name="Scheme">Scheme of the target.</param>
    /// <param name="Authority">Authority of the target, written as it would appear in <c>:authority</c> (a default port omitted).</param>
    /// <param name="Path">Path and query of the target; any fragment is dropped, as fragments never travel on the wire.</param>
    /// <param name="Method">Method for the follow-up request — possibly rewritten to GET.</param>
    /// <param name="KeepBody">Whether the original request body is replayed (307/308) or dropped (301/302/303).</param>
    /// <param name="SameOrigin">Whether the target is the same scheme + authority as the request that was redirected.</param>
    public sealed record HTTPRedirectTarget(String  Scheme,
                                            String  Authority,
                                            String  Path,
                                            String  Method,
                                            Boolean KeepBody,
                                            Boolean SameOrigin)
    {

        /// <summary>The target as an absolute URI string, for logging or handing onwards.</summary>
        public override String ToString()
            => $"{Scheme}://{Authority}{Path}";

    }


    /// <summary>
    /// Redirect handling (RFC 9110, Section 15.4): resolving <c>Location</c> against
    /// the request that produced it, and deciding what the follow-up request may
    /// look like.
    ///
    /// The method-rewriting rules are the part that surprises people, and they are
    /// not symmetrical:
    ///
    ///   * <b>301 / 302</b> — a user agent MAY rewrite POST to GET, and every one of
    ///     them does; §15.4.2 and §15.4.3 permit it explicitly "for historical
    ///     reasons". Other methods are preserved.
    ///   * <b>303</b> — always becomes GET (HEAD excepted, since a HEAD asking for a
    ///     representation's headers still wants headers), and the body is dropped.
    ///     That is the *point* of 303: "look over there instead", not "resend this".
    ///   * <b>307 / 308</b> — the method and body MUST be preserved. These exist
    ///     precisely because the older codes cannot be trusted to preserve them.
    ///
    /// Dropping the body is not optional bookkeeping: a follow-up GET that still
    /// carried the original <c>content-length</c> would be malformed.
    /// </summary>
    public static class HTTPRedirect
    {

        #region IsRedirect (Status)

        /// <summary>
        /// Whether this status names a redirection a client may follow automatically.
        /// 300 (Multiple Choices) and 304 (Not Modified) are deliberately excluded:
        /// the first needs a choice made, the second is an answer, not a redirect.
        /// </summary>
        public static Boolean IsRedirect(Int32 Status)

            => Status is 301 or 302 or 303 or 307 or 308;

        #endregion

        #region TryResolve (...)

        /// <summary>
        /// Resolve a redirect into the request that should follow it, or false when
        /// it cannot or should not be followed: a status that is not a redirect, a
        /// missing or unparseable <c>Location</c>, or a target whose scheme is not
        /// http(s) — a client must not be talked into speaking some other protocol.
        /// </summary>
        /// <param name="Status">The response status.</param>
        /// <param name="Location">The <c>Location</c> field value, absolute or relative.</param>
        /// <param name="RequestScheme">Scheme of the request being redirected.</param>
        /// <param name="RequestAuthority">Authority of the request being redirected.</param>
        /// <param name="RequestPath">Path of the request being redirected — the base for a relative reference.</param>
        /// <param name="RequestMethod">Method of the request being redirected.</param>
        /// <param name="Target">The resolved follow-up request.</param>
        public static Boolean TryResolve(Int32                     Status,
                                         String?                   Location,
                                         String                    RequestScheme,
                                         String                    RequestAuthority,
                                         String                    RequestPath,
                                         String                    RequestMethod,
                                         out HTTPRedirectTarget?   Target)
        {

            Target = null;

            if (!IsRedirect(Status) || String.IsNullOrWhiteSpace(Location))
                return false;

            // RFC 9110 §10.2.2 allows a relative reference, resolved against the
            // effective request URI (RFC 3986 §5).
            if (!Uri.TryCreate($"{RequestScheme}://{RequestAuthority}{RequestPath}", UriKind.Absolute, out var baseUri) ||
                !Uri.TryCreate(baseUri, Location.Trim(), out var target))
                return false;

            if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps)
                return false;

            var (method, keepBody) = Rewrite(Status, RequestMethod);

            Target = new HTTPRedirectTarget(
                         target.Scheme,
                         target.Authority,          // a default port is omitted, as in :authority
                         target.PathAndQuery,       // the fragment is intentionally not carried
                         method,
                         keepBody,
                         SameOrigin: target.Scheme.Equals(baseUri.Scheme,    StringComparison.OrdinalIgnoreCase) &&
                                     target.Authority.Equals(baseUri.Authority, StringComparison.OrdinalIgnoreCase)
                     );

            return true;

        }

        /// <summary>
        /// The method and body rules of RFC 9110, Section 15.4 — see the class
        /// remarks for why they differ per status.
        /// </summary>
        private static (String Method, Boolean KeepBody) Rewrite(Int32 Status, String Method)

            => Status switch {

                   // MUST preserve both.
                   307 or 308 => (Method, true),

                   // Always GET, except that HEAD stays HEAD.
                   303        => (Method.Equals("HEAD", StringComparison.Ordinal) ? "HEAD" : "GET", false),

                   // POST becomes GET (universal practice, permitted explicitly);
                   // anything else is preserved, body and all.
                   _          => Method.Equals("POST", StringComparison.Ordinal)
                                     ? ("GET",  false)
                                     : (Method, true)

               };

        #endregion

    }

}
