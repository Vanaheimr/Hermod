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

using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// EDNS COOKIE option (RFC 7873).
    /// Lightweight transaction authentication to protect against
    /// off-path spoofing and DNS amplification attacks.
    ///
    /// Wire format:
    ///   Client Cookie:  8 bytes  (always present)
    ///   Server Cookie:  8-32 bytes (present in responses, absent in first query)
    /// </summary>
    /// <remarks>See RFC 7873 for the DNS Cookies specification.</remarks>
    public class EDNSCookieOption : EDNSOption
    {

        #region Properties

        /// <summary>
        /// The 8-byte client cookie, generated pseudo-randomly
        /// and kept stable per (client, server) pair.
        /// </summary>
        public Byte[]   ClientCookie    { get; }

        /// <summary>
        /// The 8-32 byte server cookie, or null if this is an initial query.
        /// </summary>
        public Byte[]?  ServerCookie    { get; }

        /// <summary>
        /// Whether a server cookie is present.
        /// </summary>
        public Boolean  HasServerCookie
            => ServerCookie is not null && ServerCookie.Length > 0;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EDNS COOKIE option.
        /// </summary>
        /// <param name="ClientCookie">The 8-byte client cookie.</param>
        /// <param name="ServerCookie">The optional 8-32 byte server cookie.</param>
        public EDNSCookieOption(Byte[]   ClientCookie,
                                Byte[]?  ServerCookie = null)

            : base(EDNSOptionCode.Cookie,
                   Serialize(ClientCookie, ServerCookie))

        {

            if (ClientCookie.Length != 8)
                throw new ArgumentException("Client cookie must be exactly 8 bytes!", nameof(ClientCookie));

            if (ServerCookie is not null && (ServerCookie.Length < 8 || ServerCookie.Length > 32))
                throw new ArgumentException("Server cookie must be 8-32 bytes!", nameof(ServerCookie));

            this.ClientCookie = ClientCookie;
            this.ServerCookie = ServerCookie;

        }

        #endregion


        #region (static) CreateInitial()

        /// <summary>
        /// Create a new EDNS COOKIE option with a randomly generated client cookie
        /// and no server cookie (initial query).
        /// </summary>
        /// <remarks>The client cookie is generated using a cryptographically secure random number generator (RFC 7873 Section 4).</remarks>
        public static EDNSCookieOption CreateInitial()
        {

            var clientCookie = new Byte[8];
            RandomNumberGenerator.Fill(clientCookie);

            return new EDNSCookieOption(clientCookie);

        }

        #endregion

        #region (private static) Serialize(...)

        private static Byte[] Serialize(Byte[]   ClientCookie,
                                        Byte[]?  ServerCookie)
        {

            var length = 8 + (ServerCookie?.Length ?? 0);
            var data   = new Byte[length];

            Array.Copy(ClientCookie, 0, data, 0, 8);

            if (ServerCookie is not null)
                Array.Copy(ServerCookie, 0, data, 8, ServerCookie.Length);

            return data;

        }

        #endregion

        #region (static) Parse(Data)

        /// <summary>
        /// Parse an EDNS COOKIE option from raw data bytes.
        /// </summary>
        /// <param name="Data">The raw option data.</param>
        public static EDNSCookieOption Parse(Byte[] Data)
        {

            if (Data.Length < 8)
                throw new ArgumentException("EDNS COOKIE option must contain at least 8 bytes (client cookie)!", nameof(Data));

            var clientCookie = new Byte[8];
            Array.Copy(Data, 0, clientCookie, 0, 8);

            Byte[]? serverCookie = null;

            if (Data.Length > 8)
            {
                serverCookie = new Byte[Data.Length - 8];
                Array.Copy(Data, 8, serverCookie, 0, serverCookie.Length);
            }

            return new EDNSCookieOption(clientCookie, serverCookie);

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this EDNS COOKIE option.
        /// </summary>
        public override String ToString()

            => HasServerCookie
                   ? $"COOKIE client={Convert.ToHexString(ClientCookie).ToLower()} server={Convert.ToHexString(ServerCookie!).ToLower()}"
                   : $"COOKIE client={Convert.ToHexString(ClientCookie).ToLower()} (no server cookie)";

        #endregion

    }

}
