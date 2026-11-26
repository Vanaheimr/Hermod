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
    /// one-time password of a shared secret, bound to the connection it is sent in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the token of "HTTP TOTP" (<see cref="TOTPGenerator"/>), bound as over TLS v1.3: with 32 bytes
    /// of exporter material under the label <c>EXPORTER-Time-Based-One-Time-Password-v1</c> and an empty
    /// context. Over SSH the material comes from the keying material exporter of the connection
    /// (<see cref="SshExporter"/>), with the session identifier as its nonce:
    /// </para>
    /// <code>
    /// binding = SSH-Exporter(nonce = session_id, "EXPORTER-Time-Based-One-Time-Password-v1", "", 32)
    /// slot    = floor(unixtime / validity)                      // 8 bytes, big-endian
    /// inner   = HMAC(hash, key = shared secret (UTF-8), slot)
    /// bound   = HMAC(hash, key = binding,                inner)
    /// totp    = length characters of the alphabet, read off bound as off an unbound token
    /// </code>
    /// <para>
    /// A password and an answer to a keyboard-interactive prompt are read by whoever the session ends at,
    /// and are good at the real server for as long as they are valid. This one is good in the connection it
    /// was made for and nowhere else: a server that is not the one meant, or somebody between the two, has
    /// another connection with the real server, and so nothing to sign in there with. The binding is a
    /// secret of the two ends, unlike the session identifier, which SSH agents and debug logs see. The
    /// shared secret itself never travels.
    /// </para>
    /// <para>
    /// See the white papers "SSH Authentication Extensions for OCPP" and "SSH Keying Material Exporter"
    /// (section 8.1) of Open Charging Technology.
    /// </para>
    /// </remarks>
    public static class SshSessionBoundTotp
    {

        /// <summary>
        /// The name of the authentication method (RFC 4251 §6: a name of one's own carries one's domain).
        /// </summary>
        public const String MethodName     = UserAuthentication.SessionBoundTotpMethod;

        /// <summary>
        /// The exporter label of the binding: the one a TLS v1.3 connection binds the same token with.
        /// </summary>
        public const String ExporterLabel  = TOTPGenerator.TLSExporterLabel;

        /// <summary>
        /// How many bytes of exporter material a token is bound with, as over TLS v1.3.
        /// </summary>
        public const Int32  BindingLength  = TOTPGenerator.TLSExporterLength;


        #region BindingOf(Exporter, SessionId)

        /// <summary>
        /// The exporter material a one-time password of this connection is bound with: 32 bytes under
        /// <see cref="ExporterLabel"/>, an empty context, and the session identifier as nonce - which both
        /// ends have without sending anything, which differs for every connection, and whose length, that
        /// of the key exchange hash, is fixed.
        /// </summary>
        /// <param name="Exporter">The keying material exporter of the connection.</param>
        /// <param name="SessionId">The session identifier of the connection.</param>
        /// <exception cref="ArgumentException">An empty session identifier.</exception>
        public static Byte[] BindingOf(SshExporter         Exporter,
                                       ReadOnlySpan<Byte>  SessionId)
        {

            ArgumentNullException.ThrowIfNull(Exporter);

            if (SessionId.IsEmpty)
                throw new ArgumentException("A bound one-time password needs the session identifier.", nameof(SessionId));

            return Exporter.Export(SessionId, ExporterLabel, [], BindingLength);

        }

        #endregion

        #region Generate(Config, Binding, Timestamp = null)

        /// <summary>
        /// The one-time password of the given configuration, bound with the given exporter material, at the
        /// given time.
        /// </summary>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="Binding">The exporter material of the connection, see <see cref="BindingOf"/>.</param>
        /// <param name="Timestamp">The time, or null for now.</param>
        public static String Generate(TOTPConfig          Config,
                                      ReadOnlySpan<Byte>  Binding,
                                      DateTimeOffset?     Timestamp   = null)
        {

            if (Binding.IsEmpty)
                throw new ArgumentException("A bound one-time password needs the exporter material of its connection.", nameof(Binding));

            return TOTPGenerator.GenerateTOTP(
                       Config.SharedSecret,
                       Config.ValidityTime,
                       Config.Length ?? 12,
                       Config.Alphabet,
                       Timestamp,
                       Binding.ToArray(),
                       Config.HashAlgorithm
                   ).Current;

        }

        #endregion

        #region Verify(Config, Totp, Binding, Timestamp = null)

        /// <summary>
        /// Whether the given one-time password is the one of the given configuration, bound with the given
        /// exporter material, at the given time - or the one of the time slot before or after it, for two
        /// clocks that do not agree to the second. Compared in constant time.
        /// </summary>
        /// <remarks>
        /// A token need not be remembered to keep it from being used twice: it is good in one connection,
        /// and a connection is authenticated once.
        /// </remarks>
        /// <param name="Config">The shared secret and how a token is made of it.</param>
        /// <param name="Totp">The one-time password as it was sent.</param>
        /// <param name="Binding">The exporter material of the connection, see <see cref="BindingOf"/>.</param>
        /// <param name="Timestamp">The time, or null for now.</param>
        public static Boolean Verify(TOTPConfig          Config,
                                     String              Totp,
                                     ReadOnlySpan<Byte>  Binding,
                                     DateTimeOffset?     Timestamp   = null)
        {

            if (Binding.IsEmpty || String.IsNullOrEmpty(Totp))
                return false;

            var (previous, current, next, _, _) = TOTPGenerator.GenerateTOTPs(
                                                      Config.SharedSecret,
                                                      Config.ValidityTime,
                                                      Config.Length ?? 12,
                                                      Config.Alphabet,
                                                      Timestamp,
                                                      Binding.ToArray(),
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
