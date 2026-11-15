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

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The one-time password of the SSH authentication method <c>totp@open.charging.cloud</c>: a time-based
    /// one-time password of a shared secret, bound to the session it is sent in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the token of "HTTP TOTP" (<see cref="TOTPGenerator"/>) with the SSH session identifier - the
    /// exchange hash H of the first key exchange (RFC 4253 §7.2), which both ends worked out and nobody
    /// between them can choose - where a TLS connection has its exporter material:
    /// </para>
    /// <code>
    /// slot  = floor(unixtime / validity)                      // 8 bytes, big-endian
    /// inner = HMAC(hash, key = shared secret (UTF-8), slot)
    /// bound = HMAC(hash, key = session identifier,    inner)
    /// totp  = length characters of the alphabet, read off bound as off an unbound token
    /// </code>
    /// <para>
    /// A password and an answer to a keyboard-interactive prompt are read by whoever the session ends at,
    /// and are good at the real server for as long as they are valid. This one is good in the session it
    /// was made for and nowhere else: a server that is not the one meant, or somebody between the two,
    /// has a different session identifier with the real server, and so nothing to sign in there with.
    /// The shared secret itself never travels.
    /// </para>
    /// </remarks>
    public static class SshSessionBoundTotp
    {

        /// <summary>
        /// The name of the authentication method (RFC 4251 §6: a name of one's own carries one's domain).
        /// </summary>
        public const String MethodName = UserAuthentication.SessionBoundTotpMethod;


        #region Generate(Config, SessionId, Timestamp = null)

        /// <summary>
        /// The one-time password of the given configuration, bound to the given session, at the given time.
        /// </summary>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="SessionId">The session identifier.</param>
        /// <param name="Timestamp">The time, or null for now.</param>
        public static String Generate(TOTPConfig          Config,
                                      ReadOnlySpan<Byte>  SessionId,
                                      DateTimeOffset?     Timestamp   = null)
        {

            if (SessionId.IsEmpty)
                throw new ArgumentException("A session-bound one-time password needs the session identifier.", nameof(SessionId));

            return TOTPGenerator.GenerateTOTP(
                       Config.SharedSecret,
                       Config.ValidityTime,
                       Config.Length ?? 12,
                       Config.Alphabet,
                       Timestamp,
                       SessionId.ToArray(),
                       Config.HashAlgorithm
                   ).Current;

        }

        #endregion

        #region Verify(Config, Totp, SessionId, Timestamp = null)

        /// <summary>
        /// Whether the given one-time password is the one of the given configuration, bound to the given
        /// session, at the given time - or the one of the time slot before or after it, for two clocks
        /// that do not agree to the second. Compared in constant time.
        /// </summary>
        /// <remarks>
        /// A token need not be remembered to keep it from being used twice: it is good in one session,
        /// and a session is authenticated once.
        /// </remarks>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="Totp">The one-time password as it was sent.</param>
        /// <param name="SessionId">The session identifier.</param>
        /// <param name="Timestamp">The time, or null for now.</param>
        public static Boolean Verify(TOTPConfig          Config,
                                     String              Totp,
                                     ReadOnlySpan<Byte>  SessionId,
                                     DateTimeOffset?     Timestamp   = null)
        {

            if (SessionId.IsEmpty || String.IsNullOrEmpty(Totp))
                return false;

            var (previous, current, next, _, _) = TOTPGenerator.GenerateTOTPs(
                                                      Config.SharedSecret,
                                                      Config.ValidityTime,
                                                      Config.Length ?? 12,
                                                      Config.Alphabet,
                                                      Timestamp,
                                                      SessionId.ToArray(),
                                                      Config.HashAlgorithm
                                                  );

            var sent = Encoding.UTF8.GetBytes(Totp);

            // All three compared, whichever matches: how long it takes says nothing about which.
            var matches = false;

            foreach (var expected in new[] { previous, current, next })
                matches |= CryptographicOperations.FixedTimeEquals(sent, Encoding.UTF8.GetBytes(expected));

            return matches;

        }

        #endregion

    }

}
