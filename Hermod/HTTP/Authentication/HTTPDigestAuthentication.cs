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
    /// The credentials of an RFC 7616 <c>Digest</c> <c>Authorization</c> header:
    /// a challenge-response scheme that never sends the password over the wire.
    /// The server issues a <c>nonce</c>; the client answers with
    /// <c>response = H(HA1:nonce:nc:cnonce:qop:HA2)</c> where
    /// <c>HA1 = H(username:realm:password)</c> and
    /// <c>HA2 = H(method:request-target)</c> (Section 3.4).
    ///
    /// This type only *parses and reserializes* those credentials. Whether they
    /// are valid is <c>DigestAuthenticationScheme</c>'s question, because
    /// answering it needs the nonce's own integrity and age, the realm, and a
    /// password lookup — none of which a value read off a header can know.
    ///
    /// Until 2026-09-24 this type was something else entirely. It parsed
    /// <c>Digest base64(username):base64(secret)</c> — a password sent in the
    /// clear, under the name of the one scheme whose entire purpose is not to
    /// send one — and its own documentation called the second field "a
    /// time-based one-time password", which it was not either. Nothing
    /// referenced it, not even the <c>Authorization</c> parser, so no caller is
    /// affected; but the name would have misled every reader and curl's
    /// <c>--digest</c> could never have interoperated with it. That is finding
    /// H-3 of the HTTP/1.1 conformance suite.
    /// </summary>
    public sealed class HTTPDigestAuthentication : IHTTPAuthentication,
                                                   IEquatable<HTTPDigestAuthentication>,
                                                   IComparable<HTTPDigestAuthentication>,
                                                   IComparable
    {

        #region Properties

        /// <summary>
        /// The username, or — when <see cref="Userhash"/> is set — the hash of
        /// <c>username:realm</c> that stands in for it (RFC 7616, Section 3.4.4).
        /// </summary>
        public String   Username       { get; }

        /// <summary>
        /// The protection space the client hashed into HA1. A server must check
        /// this against its own realm: the same password under a different realm
        /// yields a different HA1.
        /// </summary>
        public String   Realm          { get; }

        /// <summary>
        /// The server-issued nonce this response was computed against.
        /// </summary>
        public String   Nonce          { get; }

        /// <summary>
        /// The request-target the client hashed into HA2. RFC 7616 calls this
        /// field <c>uri</c>; it is the effective request URI as the client sent
        /// it, and a server must compare it with the one it actually received.
        /// </summary>
        public String   DigestURI      { get; }

        /// <summary>
        /// The computed response — the only field that proves anything.
        /// </summary>
        public String   Response       { get; }

        /// <summary>
        /// The hash algorithm, a *token* rather than a quoted string
        /// (Section 3.3): "SHA-256", "MD5", or either with a "-sess" suffix.
        /// Absent means MD5, for compatibility with RFC 2617.
        /// </summary>
        public String?  Algorithm      { get; }

        /// <summary>
        /// The quality of protection actually applied — "auth" or "auth-int".
        /// Absent means the legacy RFC 2069 form, in which <see cref="NonceCount"/>
        /// and <see cref="ClientNonce"/> are absent too and the response is
        /// computed without them.
        /// </summary>
        public String?  QoP            { get; }

        /// <summary>
        /// The nonce count, an 8-digit hex counter of how many requests the client
        /// has sent with this nonce. Its purpose is replay detection, so a server
        /// that does not remember the highest one it has seen gains nothing from it.
        /// </summary>
        public String?  NonceCount     { get; }

        /// <summary>
        /// The client's own nonce, which keeps the server from choosing the whole
        /// input to the hash.
        /// </summary>
        public String?  ClientNonce    { get; }

        /// <summary>
        /// The opaque value echoed back unchanged from the challenge.
        /// </summary>
        public String?  Opaque         { get; }

        /// <summary>
        /// Whether <see cref="Username"/> carries a hash of <c>username:realm</c>
        /// rather than the name itself (Section 3.4.4).
        /// </summary>
        public Boolean  Userhash       { get; }

        /// <summary>
        /// The credentials exactly as they arrived — everything after the
        /// <c>Digest</c> token. Kept verbatim because a validator must work from
        /// what the client sent rather than from a reserialization of it.
        /// </summary>
        public String   Credentials    { get; }

        /// <summary>
        /// The HTTP request header representation.
        /// </summary>
        public String   HTTPText

            => $"Digest {Credentials}";

        #endregion

        #region Constructor(s)

        private HTTPDigestAuthentication(String   Username,
                                         String   Realm,
                                         String   Nonce,
                                         String   DigestURI,
                                         String   Response,
                                         String   Credentials,
                                         String?  Algorithm     = null,
                                         String?  QoP           = null,
                                         String?  NonceCount    = null,
                                         String?  ClientNonce   = null,
                                         String?  Opaque        = null,
                                         Boolean  Userhash      = false)
        {

            this.Username     = Username;
            this.Realm        = Realm;
            this.Nonce        = Nonce;
            this.DigestURI    = DigestURI;
            this.Response     = Response;
            this.Credentials  = Credentials;
            this.Algorithm    = Algorithm;
            this.QoP          = QoP;
            this.NonceCount   = NonceCount;
            this.ClientNonce  = ClientNonce;
            this.Opaque       = Opaque;
            this.Userhash     = Userhash;

        }

        #endregion


        #region (static) TryParseHTTPHeader(Text, out DigestAuthentication)

        /// <summary>
        /// Try to parse an <c>Authorization: Digest …</c> header value.
        ///
        /// Requires at least username, realm, nonce, uri and response
        /// (Section 3.4) — the five without which nothing can be verified.
        /// Everything else is optional and passed through as it came. A
        /// <c>qop</c> additionally requires <c>nc</c> and <c>cnonce</c>, because
        /// the response is computed over them: a credential naming a qop and
        /// omitting them is not merely incomplete, it cannot be checked against
        /// any password at all.
        /// </summary>
        /// <param name="Text">The header value, including the scheme token.</param>
        /// <param name="DigestAuthentication">The parsed credentials.</param>
        public static Boolean TryParseHTTPHeader(String                                             Text,
                                                 [NotNullWhen(true)] out HTTPDigestAuthentication?  DigestAuthentication)
        {

            DigestAuthentication = null;

            if (Text is null)
                return false;

            var text = Text.Trim();

            if (text.Length < 8 ||
               !text.StartsWith("Digest", StringComparison.OrdinalIgnoreCase) ||
               !Char.IsWhiteSpace(text[6]))
            {
                return false;
            }

            var credentials  = text[7..].Trim();
            var parameters   = HTTPAuthParams.Parse(credentials);

            if (!parameters.TryGetValue("username", out var username) || username.Length == 0 ||
                !parameters.TryGetValue("realm",    out var realm)                            ||
                !parameters.TryGetValue("nonce",    out var nonce)                            ||
                !parameters.TryGetValue("uri",      out var digestURI)                        ||
                !parameters.TryGetValue("response", out var response) || response.Length == 0)
            {
                return false;
            }

            parameters.TryGetValue("algorithm", out var algorithm);
            parameters.TryGetValue("qop",       out var qop);
            parameters.TryGetValue("nc",        out var nonceCount);
            parameters.TryGetValue("cnonce",    out var clientNonce);
            parameters.TryGetValue("opaque",    out var opaque);
            parameters.TryGetValue("userhash",  out var userhash);

            if (qop is not null &&
                (nonceCount is null || clientNonce is null))
            {
                return false;
            }

            DigestAuthentication = new HTTPDigestAuthentication(
                                       username,
                                       realm,
                                       nonce,
                                       digestURI,
                                       response,
                                       credentials,
                                       algorithm,
                                       qop,
                                       nonceCount,
                                       clientNonce,
                                       opaque,
                                       userhash is not null &&
                                           userhash.Equals("true", StringComparison.OrdinalIgnoreCase)
                                   );

            return true;

        }

        #endregion

        #region (static) TryParse          (Text)

        /// <summary>
        /// Try to parse an <c>Authorization: Digest …</c> header value, returning
        /// null when it is not one.
        /// </summary>
        /// <param name="Text">The header value, including the scheme token.</param>
        public static HTTPDigestAuthentication? TryParse(String Text)

            => TryParseHTTPHeader(Text, out var digestAuthentication)
                   ? digestAuthentication
                   : null;

        #endregion


        #region Operator overloading

        public static Boolean operator == (HTTPDigestAuthentication? DigestAuthentication1,
                                           HTTPDigestAuthentication? DigestAuthentication2)
        {

            if (ReferenceEquals(DigestAuthentication1, DigestAuthentication2))
                return true;

            if (DigestAuthentication1 is null || DigestAuthentication2 is null)
                return false;

            return DigestAuthentication1.Equals(DigestAuthentication2);

        }

        public static Boolean operator != (HTTPDigestAuthentication? DigestAuthentication1,
                                           HTTPDigestAuthentication? DigestAuthentication2)

            => !(DigestAuthentication1 == DigestAuthentication2);

        #endregion

        #region IComparable<HTTPDigestAuthentication> Members

        public Int32 CompareTo(Object? Object)

            => Object is HTTPDigestAuthentication digestAuthentication
                   ? CompareTo(digestAuthentication)
                   : throw new ArgumentException("The given object is not an HTTP Digest authentication!", nameof(Object));

        public Int32 CompareTo(HTTPDigestAuthentication? DigestAuthentication)
        {

            if (DigestAuthentication is null)
                return 1;

            var c = String.Compare(Username,  DigestAuthentication.Username,  StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(Realm,     DigestAuthentication.Realm,     StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(Nonce,     DigestAuthentication.Nonce,     StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(DigestURI, DigestAuthentication.DigestURI, StringComparison.Ordinal);

            if (c == 0)
                c = String.Compare(Response,  DigestAuthentication.Response,  StringComparison.Ordinal);

            return c;

        }

        #endregion

        #region IEquatable<HTTPDigestAuthentication> Members

        public override Boolean Equals(Object? Object)

            => Object is HTTPDigestAuthentication digestAuthentication &&
                   Equals(digestAuthentication);

        public Boolean Equals(HTTPDigestAuthentication? DigestAuthentication)

            => DigestAuthentication is not null &&
               String.Equals(Username,  DigestAuthentication.Username,  StringComparison.Ordinal) &&
               String.Equals(Realm,     DigestAuthentication.Realm,     StringComparison.Ordinal) &&
               String.Equals(Nonce,     DigestAuthentication.Nonce,     StringComparison.Ordinal) &&
               String.Equals(DigestURI, DigestAuthentication.DigestURI, StringComparison.Ordinal) &&
               String.Equals(Response,  DigestAuthentication.Response,  StringComparison.Ordinal);

        #endregion

        #region (override) GetHashCode()

        public override Int32 GetHashCode()

            => HashCode.Combine(Username, Realm, Nonce, DigestURI, Response);

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object — deliberately without the
        /// response, which is the one field worth keeping out of a log.
        /// </summary>
        public override String ToString()

            => $"Digest {Username}@{Realm}{(QoP       is not null ? $", qop={QoP}"             : "")}" +
                                          $"{(Algorithm is not null ? $", algorithm={Algorithm}" : "")}";

        #endregion

    }

}
