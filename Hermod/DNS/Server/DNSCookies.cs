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

using System.Buffers.Binary;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The server half of a DNS Cookie (RFC 7873): a value a server issues to a
    /// client and recognises when it comes back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A server cookie is only ever checked by the server that made it, which is
    /// what lets RFC 7873 §6 leave its construction to local policy. It has one
    /// job: be cheap to verify, impossible to guess, and impossible to move —
    /// tied to the client cookie and the client's address, so that a value
    /// observed on the wire is worth nothing to anyone else and worth nothing
    /// from anywhere else.
    /// </para>
    /// <para>
    /// The layout here follows the shape RFC 9018 §4 later standardised — a
    /// version octet, a timestamp, and a truncated keyed hash — but not its
    /// algorithm, which is SipHash-2-4. That difference is deliberate and
    /// harmless in the ordinary case: the only reason RFC 9018 pins the algorithm
    /// is so that the members of an anycast cluster sharing one secret can
    /// validate each other's cookies. A single server validating its own needs
    /// agreement with nobody.
    /// </para>
    /// <para>
    /// The timestamp is what keeps a stolen cookie from being useful forever, and
    /// it is inside the hash rather than beside it — otherwise a client could
    /// move it forward and extend its own cookie's life.
    /// </para>
    /// </remarks>
    public static class DNSCookies
    {

        #region Data

        /// <summary>The size of the server cookies produced here: 16 octets, the smallest RFC 7873 §4.2 allows.</summary>
        public const Int32 ServerCookieSize = 16;

        /// <summary>The version octet this implementation writes and accepts.</summary>
        public const Byte  Version          = 1;

        /// <summary>
        /// How long a server cookie stays valid, and how much clock skew towards
        /// the future is tolerated.
        /// </summary>
        /// <remarks>
        /// An hour of validity and five minutes of skew, which is what RFC 9018
        /// §4.3 recommends for the same fields. The asymmetry is the point: a
        /// cookie from the past is merely old, while one from the future can only
        /// come from a clock that disagrees — or from someone guessing.
        /// </remarks>
        public static readonly TimeSpan DefaultValidity  = TimeSpan.FromHours(1);
        public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(5);

        #endregion

        #region (static) Create(ClientCookie, ClientAddress, Secret, Timestamp = null)

        /// <summary>
        /// Issue a server cookie for this client cookie and address.
        /// </summary>
        /// <param name="ClientCookie">The 8-octet client cookie the query carried.</param>
        /// <param name="ClientAddress">The address the query came from.</param>
        /// <param name="Secret">The server's secret. Never leaves the server and never goes on the wire.</param>
        /// <param name="Timestamp">When the cookie is issued; now when omitted.</param>
        public static Byte[] Create(Byte[]            ClientCookie,
                                    IIPAddress        ClientAddress,
                                    Byte[]            Secret,
                                    DateTimeOffset?   Timestamp   = null)
        {

            if (ClientCookie.Length != 8)
                throw new ArgumentException("A client cookie is exactly 8 octets (RFC 7873 §4.1).", nameof(ClientCookie));

            var cookie = new Byte[ServerCookieSize];

            cookie[0] = Version;
            // cookie[1..4] stay zero: reserved.

            BinaryPrimitives.WriteUInt32BigEndian(
                cookie.AsSpan(4, 4),
                (UInt32) (Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
            );

            Hash(ClientCookie, ClientAddress, Secret, cookie.AsSpan(0, 8)).
                CopyTo(cookie.AsSpan(8, 8));

            return cookie;

        }

        #endregion

        #region (static) Validate(ServerCookie, ClientCookie, ClientAddress, Secret, ...)

        /// <summary>
        /// Whether this server cookie is one we issued, to this client cookie,
        /// from this address, recently enough.
        /// </summary>
        /// <param name="ServerCookie">The server cookie the query returned.</param>
        /// <param name="ClientCookie">The client cookie of the same query.</param>
        /// <param name="ClientAddress">The address the query came from.</param>
        /// <param name="Secret">The server's secret.</param>
        /// <param name="Now">The current time; taken from the clock when omitted.</param>
        /// <param name="Validity">How long a cookie stays usable.</param>
        /// <param name="ClockSkew">How far into the future a timestamp may sit.</param>
        public static Boolean Validate(Byte[]           ServerCookie,
                                       Byte[]           ClientCookie,
                                       IIPAddress       ClientAddress,
                                       Byte[]           Secret,
                                       DateTimeOffset?  Now         = null,
                                       TimeSpan?        Validity    = null,
                                       TimeSpan?        ClockSkew   = null)
        {

            if (ServerCookie.Length != ServerCookieSize ||
                ClientCookie.Length != 8                ||
                ServerCookie[0]     != Version)
            {
                return false;
            }

            var now       = Now ?? DateTimeOffset.UtcNow;
            var issued    = DateTimeOffset.FromUnixTimeSeconds(
                                BinaryPrimitives.ReadUInt32BigEndian(ServerCookie.AsSpan(4, 4))
                            );

            if (issued > now + (ClockSkew ?? DefaultClockSkew) ||
                issued < now - (Validity  ?? DefaultValidity))
            {
                return false;
            }

            // Fixed-time, because this compares a value an attacker supplies
            // against one only the server can compute — which is exactly the
            // shape a timing oracle needs to be useful.
            return CryptographicOperations.FixedTimeEquals(
                       Hash(ClientCookie, ClientAddress, Secret, ServerCookie.AsSpan(0, 8)),
                       ServerCookie.AsSpan(8, 8)
                   );

        }

        #endregion

        #region (static) GenerateSecret()

        /// <summary>
        /// A fresh server secret.
        /// </summary>
        public static Byte[] GenerateSecret()
            => RandomNumberGenerator.GetBytes(32);

        #endregion

        #region (private static) Hash(ClientCookie, ClientAddress, Secret, Preamble)

        /// <summary>
        /// The keyed hash over everything the cookie is bound to.
        /// </summary>
        /// <remarks>
        /// The preamble — version, reserved and timestamp — is hashed along with
        /// the rest so that none of it can be edited after the fact. The client
        /// address is in there because without it a cookie is a bearer token:
        /// anyone who saw one on the wire could present it from anywhere, and a
        /// mechanism whose whole purpose is to prove where a query came from
        /// would be proving nothing.
        /// </remarks>
        private static Byte[] Hash(Byte[]              ClientCookie,
                                   IIPAddress          ClientAddress,
                                   Byte[]              Secret,
                                   ReadOnlySpan<Byte>  Preamble)
        {

            var addressBytes = ClientAddress.GetBytes();
            var input        = new Byte[ClientCookie.Length + Preamble.Length + addressBytes.Length];

            ClientCookie.CopyTo(input.AsSpan(0));
            Preamble.    CopyTo(input.AsSpan(ClientCookie.Length));
            addressBytes.CopyTo(input.AsSpan(ClientCookie.Length + Preamble.Length));

            return HMACSHA256.HashData(Secret, input)[..8];

        }

        #endregion

    }

}
