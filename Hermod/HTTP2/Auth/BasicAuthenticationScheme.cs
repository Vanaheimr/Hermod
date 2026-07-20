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

    using System.Text;

    /// <summary>
    /// RFC 7617 "Basic" HTTP authentication: credentials are
    /// <c>base64(userid ":" password)</c>. The scheme decodes them; the supplied
    /// validator decides whether that user/password pair is valid (this framework
    /// stays BCL-only and store-agnostic — no password database baked in).
    ///
    /// Basic transmits the password on every request, protected only by TLS —
    /// which this stack always uses. A validator SHOULD compare secrets in
    /// constant time to avoid timing oracles; that's the app's responsibility.
    /// </summary>
    public sealed class BasicAuthenticationScheme : IHTTPAuthenticationScheme
    {

        private readonly Func<string, string, CancellationToken, Task<HTTPAuthenticatedIdentity?>> validate;

        public BasicAuthenticationScheme(Func<string, string, CancellationToken, Task<HTTPAuthenticatedIdentity?>> Validate)
        {
            validate = Validate;
        }

        public string SchemeName => "Basic";

        // charset="UTF-8" (RFC 7617, Section 2.1) tells the client how to encode
        // non-ASCII credentials before base64.
        public string BuildChallenge(string Realm)
            => $"Basic realm=\"{Realm}\", charset=\"UTF-8\"";

        public async Task<HTTPAuthenticatedIdentity?> AuthenticateAsync(
            string Credentials, string Method, string RequestTarget, CancellationToken CancellationToken)
        {

            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(Credentials.Trim()));
            }
            catch (FormatException)
            {
                return null;   // Not valid base64 — malformed credentials.
            }

            // RFC 7617: split on the FIRST colon; the password may itself contain colons.
            var colon = decoded.IndexOf(':');
            if (colon < 0)
                return null;

            return await validate(decoded[..colon], decoded[(colon + 1)..], CancellationToken);

        }

    }

}
