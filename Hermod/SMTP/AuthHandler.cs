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
        /// Decode a client response. RFC 4954 §4: a single "=" is the empty response - which
        /// Convert.FromBase64String would reject.
        /// </summary>
        /// <returns>False when the response is not valid base64.</returns>
        protected static bool TryDecode(string clientResponse, out string decoded)
        {

            decoded = "";

            if (clientResponse == "=")
                return true;

            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(clientResponse));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }

        }
    }

}
