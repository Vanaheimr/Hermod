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
using System.Buffers.Binary;
using System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// A standard time-based one-time password generator/validator (RFC 6238 TOTP over RFC 4226 HOTP),
    /// compatible with Google Authenticator, Authy, YubiKey OATH, KeePassXC and the like. Used as the
    /// second factor delivered over keyboard-interactive. The clock comes from a <see cref="TimeProvider"/>
    /// so skew and tests are deterministic; validation is constant-time and single-use per window.
    /// </summary>
    public sealed class Totp
    {

        #region Data

        private const String Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        private readonly Byte[]             secret;
        private readonly Int32              digits;
        private readonly HashAlgorithmName  algorithm;
        private readonly Int32              stepSeconds;
        private readonly TimeProvider       timeProvider;
        private readonly HashSet<Int64>     consumedCounters = [];
        private readonly Lock               consumedLock     = new ();

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a TOTP instance from a raw shared secret.
        /// </summary>
        /// <param name="Secret">The shared secret bytes.</param>
        /// <param name="Digits">The code length (6 or 8).</param>
        /// <param name="Algorithm">The HMAC hash (SHA-1 by default; SHA-256/512 supported).</param>
        /// <param name="StepSeconds">The time step (30 s by default).</param>
        /// <param name="TimeProvider">The clock (system by default).</param>
        public Totp(Byte[]              Secret,
                    Int32               Digits        = 6,
                    HashAlgorithmName?  Algorithm     = null,
                    Int32               StepSeconds   = 30,
                    TimeProvider?       TimeProvider  = null)
        {

            if (Digits is < 6 or > 10)
                throw new ArgumentOutOfRangeException(nameof(Digits), "TOTP length must be between 6 and 10 digits.");

            this.secret        = Secret;
            this.digits        = Digits;
            this.algorithm     = Algorithm ?? HashAlgorithmName.SHA1;
            this.stepSeconds   = StepSeconds;
            this.timeProvider  = TimeProvider ?? System.TimeProvider.System;

        }

        #endregion

        #region (static) FromBase32(Secret, …)

        /// <summary>
        /// Create a TOTP instance from a base32-encoded shared secret (the enrolment format).
        /// </summary>
        public static Totp FromBase32(String              Secret,
                                      Int32               Digits        = 6,
                                      HashAlgorithmName?  Algorithm     = null,
                                      Int32               StepSeconds   = 30,
                                      TimeProvider?       TimeProvider  = null)

            => new (DecodeBase32(Secret), Digits, Algorithm, StepSeconds, TimeProvider);

        #endregion


        #region Compute(Time)

        /// <summary>
        /// Compute the code valid at the given instant.
        /// </summary>
        public String Compute(DateTimeOffset Time)
            => ComputeForCounter(CounterFor(Time));

        /// <summary>
        /// Compute the code valid now.
        /// </summary>
        public String ComputeNow()
            => ComputeForCounter(CounterFor(timeProvider.GetUtcNow()));

        #endregion

        #region Verify(Code, SkewSteps = 1)

        /// <summary>
        /// Verify a submitted code against the current time window (± <paramref name="SkewSteps"/> steps),
        /// constant-time. A matching code is consumed so it cannot be replayed within its validity window.
        /// </summary>
        public Boolean Verify(String Code, Int32 SkewSteps = 1)
        {

            var trimmed = Code.Trim();
            if (trimmed.Length != digits)
                return false;

            var center = CounterFor(timeProvider.GetUtcNow());

            for (var counter = center - SkewSteps; counter <= center + SkewSteps; counter++)
            {

                var expected = ComputeForCounter(counter);
                if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(trimmed)))
                    continue;

                lock (consumedLock)
                {
                    if (!consumedCounters.Add(counter))
                        return false;   // already used within its window (replay)
                }

                return true;

            }

            return false;

        }

        #endregion

        #region ProvisioningUri(Account, Issuer)

        /// <summary>
        /// The <c>otpauth://totp/…</c> provisioning URI for enrolment (scan as a QR code into an
        /// authenticator app).
        /// </summary>
        public String ProvisioningUri(String Account, String Issuer)
        {

            var label   = Uri.EscapeDataString($"{Issuer}:{Account}");
            var alg     = algorithm == HashAlgorithmName.SHA256 ? "SHA256"
                        : algorithm == HashAlgorithmName.SHA512 ? "SHA512"
                        : "SHA1";

            return $"otpauth://totp/{label}" +
                   $"?secret={EncodeBase32(secret)}" +
                   $"&issuer={Uri.EscapeDataString(Issuer)}" +
                   $"&algorithm={alg}&digits={digits}&period={stepSeconds}";

        }

        #endregion


        #region (private) counter / HOTP

        private Int64 CounterFor(DateTimeOffset Time)
            => Time.ToUnixTimeSeconds() / stepSeconds;   // T0 = 0

        private String ComputeForCounter(Int64 Counter)
        {

            Span<Byte> counterBytes = stackalloc Byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counterBytes, Counter);

            using var hmac = IncrementalHash.CreateHMAC(algorithm, secret);
            hmac.AppendData(counterBytes);
            var hash = hmac.GetHashAndReset();

            // RFC 4226 dynamic truncation.
            var offset  = hash[^1] & 0x0f;
            var binary  = ((hash[offset]     & 0x7f) << 24) |
                          ((hash[offset + 1] & 0xff) << 16) |
                          ((hash[offset + 2] & 0xff) << 8)  |
                           (hash[offset + 3] & 0xff);

            var modulo  = (Int32) Math.Pow(10, digits);
            return (binary % modulo).ToString().PadLeft(digits, '0');

        }

        #endregion

        #region (private) Base32

        private static Byte[] DecodeBase32(String Value)
        {

            var clean  = Value.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
            var bits    = 0;
            var value   = 0;
            var output  = new List<Byte>(clean.Length * 5 / 8);

            foreach (var c in clean)
            {
                var index = Base32Alphabet.IndexOf(c);
                if (index < 0)
                    throw new FormatException($"Invalid base32 character '{c}'.");

                value  = (value << 5) | index;
                bits  += 5;
                if (bits >= 8)
                {
                    output.Add((Byte) ((value >> (bits - 8)) & 0xff));
                    bits -= 8;
                }
            }

            return [.. output];

        }

        private static String EncodeBase32(ReadOnlySpan<Byte> Value)
        {

            var builder = new StringBuilder();
            var bits    = 0;
            var buffer  = 0;

            foreach (var b in Value)
            {
                buffer  = (buffer << 8) | b;
                bits   += 8;
                while (bits >= 5)
                {
                    builder.Append(Base32Alphabet[(buffer >> (bits - 5)) & 0x1f]);
                    bits -= 5;
                }
            }

            if (bits > 0)
                builder.Append(Base32Alphabet[(buffer << (5 - bits)) & 0x1f]);

            return builder.ToString();

        }

        #endregion

    }


    /// <summary>
    /// A keyboard-interactive second factor that collects a single non-echoing TOTP code and validates it
    /// against a <see cref="Totp"/> — the server side of the <c>publickey,keyboard-interactive</c> 2FA chain.
    /// </summary>
    public sealed class TotpKeyboardInteractive : ISshKeyboardInteractiveFactor
    {

        private readonly Totp    totp;
        private readonly Int32   skewSteps;

        /// <summary>
        /// Create a TOTP keyboard-interactive factor.
        /// </summary>
        public TotpKeyboardInteractive(Totp Totp, Int32 SkewSteps = 1)
        {
            this.totp       = Totp;
            this.skewSteps  = SkewSteps;
        }

        public String                    Name         => "";
        public String                    Instruction  => "";
        public IReadOnlyList<SshPrompt>  Prompts      => [ new SshPrompt("Verification code: ", Echo: false) ];

        public ValueTask<Boolean> ValidateAsync(IReadOnlyList<String> Responses, CancellationToken CancellationToken = default)
            => ValueTask.FromResult(Responses.Count == 1 && totp.Verify(Responses[0], skewSteps));

    }

}
