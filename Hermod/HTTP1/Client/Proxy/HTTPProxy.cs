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

using System.Net.Security;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A forward proxy that a client connects through. The client asks it with
    /// CONNECT (RFC 9110, Section 9.3.6) for a tunnel to the server, and then
    /// speaks to the server through that tunnel as it would directly: its TLS
    /// to the server runs end to end, and the proxy sees none of it.
    /// </summary>
    /// <remarks>
    /// What the client says to the proxy is said in Proxy-Authorization, which
    /// takes any HTTP authentication: HTTP Basic, a token, a one-time password
    /// under a login. The one-time password can be bound to the TLS connection
    /// to the proxy, with keying material of that connection (RFC 8446,
    /// Section 7.5). A password read off that connection is then of no use on
    /// any other. Binding it needs an "https" proxy whose key is pinned
    /// (<see cref="ServerKeyPins"/>), because only the TLS stack that checks
    /// key pins exports that material; SslStream does not.
    /// </remarks>
    public sealed class HTTPProxy
    {

        #region Properties

        /// <summary>
        /// Where the proxy is: "http://proxy.example.org:3128" for a plain TCP
        /// connection to it, "https://proxy.example.org" for TLS to the proxy.
        /// The TLS to the server runs inside the tunnel either way.
        /// </summary>
        public URL                                   URL                         { get; }

        /// <summary>
        /// The host the TCP connection goes to.
        /// </summary>
        public String                                Host
            => URL.Host.ToString();

        /// <summary>
        /// The TCP port the TCP connection goes to: that of the URL, else 443
        /// for "https" and 80 for "http".
        /// </summary>
        public IPPort                                Port
            => URL.Port ?? (IsTLS ? IPPort.HTTPS : IPPort.HTTP);

        /// <summary>
        /// Whether the connection to the proxy speaks TLS.
        /// </summary>
        public Boolean                               IsTLS
            => URL.Scheme == URIScheme.https;

        /// <summary>
        /// What goes in Proxy-Authorization: any HTTP authentication.
        /// </summary>
        public IHTTPAuthentication?                  Authentication              { get; init; }

        /// <summary>
        /// The configuration of a one-time password, which goes in
        /// Proxy-Authorization as "TOTP" under <see cref="TOTPLogin"/>. A new
        /// password is made for every connection, a reconnect included. Where
        /// the configuration asks for TLS channel binding, the password is
        /// bound to the TLS connection to the proxy.
        /// </summary>
        public TOTPConfig?                           TOTPConfig                  { get; init; }

        /// <summary>
        /// The login the one-time password of <see cref="TOTPConfig"/> is sent under.
        /// </summary>
        public String?                               TOTPLogin                   { get; init; }

        /// <summary>
        /// Checks the certificate of an "https" proxy where its key is not
        /// pinned. Without one, the certificate must be valid for the proxy's
        /// host under the roots of the operating system.
        /// </summary>
        public RemoteCertificateValidationCallback?  RemoteCertificateValidator  { get; init; }

        /// <summary>
        /// The key pins of an "https" proxy. Where set, the proxy's key is
        /// checked against them and against nothing else, and the TLS to the
        /// proxy exports what a one-time password is bound with.
        /// </summary>
        public KeyPinPolicy?                         ServerKeyPins               { get; init; }

        /// <summary>
        /// Told the result of the key pin check of the TLS to the proxy.
        /// </summary>
        public Action<KeyPinValidation>?             OnServerKeyPinValidated     { get; init; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// A forward proxy at the given URL.
        /// </summary>
        /// <param name="URL">Where the proxy is, as "http://proxy.example.org:3128" or "https://proxy.example.org".</param>
        /// <exception cref="ArgumentException">The URL is neither "http" nor "https".</exception>
        public HTTPProxy(URL URL)
        {

            if (URL.Scheme != URIScheme.http &&
                URL.Scheme != URIScheme.https)
                throw new ArgumentException($"A forward proxy is reached over \"http\" or \"https\", and '{URL}' is neither.", nameof(URL));

            this.URL = URL;

        }

        #endregion


        #region WhyItCannotBeUsed()

        /// <summary>
        /// Why this proxy cannot be connected through as it is configured, or
        /// null where it can.
        /// </summary>
        /// <remarks>
        /// Each case is a configuration that would otherwise send something it
        /// does not say: a login with no password, two things for the one
        /// Proxy-Authorization header, a password weaker than was asked for.
        /// </remarks>
        public String? WhyItCannotBeUsed()
        {

            if (TOTPLogin is not null && TOTPLogin.Trim().Length == 0)
                return "An empty TOTP login was given for the proxy; a one-time password goes in Proxy-Authorization under a login.";

            if (TOTPLogin is not null && TOTPConfig is null)
                return $"A TOTP login ('{TOTPLogin}') was given for the proxy without a TOTP configuration to make its one-time password from.";

            if (TOTPConfig is not null && TOTPLogin is null)
                return "A TOTP configuration was given for the proxy without a login; a one-time password goes in Proxy-Authorization under a login.";

            if (TOTPLogin is not null && Authentication is not null)
                return "Both an HTTP authentication and a TOTP login were given for the proxy, and a CONNECT has one Proxy-Authorization header.";

            if (TOTPConfig?.TLSChannelBinding == true && !IsTLS)
                return $"The TOTP configuration of the proxy asks for TLS channel binding, and '{URL}' is reached without TLS.";

            if (TOTPConfig?.TLSChannelBinding == true && ServerKeyPins is null)
                return "The TOTP configuration of the proxy asks for TLS channel binding, and a one-time password is bound to the TLS of a proxy " +
                       "whose key is pinned (ServerKeyPins) and to no other: SslStream exports no keying material to bind one with.";

            if (Authentication?.HTTPText is String text && (text.Contains('\r') || text.Contains('\n')))
                return "The HTTP authentication of the proxy holds a line break.";

            return null;

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => URL.ToString();

        #endregion

    }

}
