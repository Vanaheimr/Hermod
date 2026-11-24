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

using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    public abstract class AuthHandler
    {
        public abstract string MechanismName { get; }
        public abstract Task<AuthResponse> ProcessAsync(string? clientResponse, CancellationToken ct = default);
        public virtual void Reset() { }

        /// <summary>
        /// RFC 4954 §4: "If the server cannot [BASE64] decode any client response, it MUST reject
        /// the AUTH command with a 501 reply (and an enhanced status code of 5.5.2)." A response
        /// that decodes but says the wrong thing is a failed authentication (535); one that does
        /// not decode is this.
        /// </summary>
        protected static AuthResponse Undecodable
            => new (AuthResult.Fail, ErrorCode: "501 5.5.2 Cannot decode response");

        /// <summary>
        /// Decode a client response. RFC 4954 §4: a single "=" is the empty response; and
        /// "servers and clients MUST reject (and not ignore) any character not explicitly allowed
        /// by the BASE64 alphabet" - a space or a tab inside a response, which
        /// Convert.FromBase64String skips, makes it no base64.
        /// </summary>
        /// <returns>False when the response is not valid base64.</returns>
        protected static bool TryDecode(string clientResponse, out string decoded)
        {

            var octets = SaslBase64.Decode(clientResponse);

            decoded = octets is not null
                          ? Encoding.UTF8.GetString(octets)
                          : "";

            return octets is not null;

        }

        /// <summary>
        /// RFC 4954 §6: the user store could not be asked - "454 4.7.0 Temporary authentication
        /// failure", after which "The client SHOULD NOT prompt the user for another password".
        /// Not 535, which tells it the credentials are wrong.
        /// </summary>
        protected static AuthResponse TemporaryFailure
            => new (AuthResult.TemporaryFailure, ErrorCode: "454 4.7.0 Temporary authentication failure");
    }

}
