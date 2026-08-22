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

using NUnit.Framework;

using Newtonsoft.Json.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TOTP
{

    /// <summary>
    /// The token format of the TOTP generator is shared with the TypeScript implementation
    /// TOTP.ts (https://github.com/OpenChargingCloud/TOTP.ts). The example token values
    /// that used to be duplicated here live in the canonical conformance vectors now
    /// (TOTP/TestVectors/, run by TOTPVectorTests); this fixture keeps what the vectors
    /// cannot express - API-typed error cases, hash algorithm parsing, structural
    /// properties of the frozen format, and the JSON config round-trip.
    /// </summary>
    [TestFixture]
    public class TOTPGeneratorTests
    {

        #region Data

        private const String            sharedSecret   = "secure!Charging!";

        private static readonly DateTimeOffset  timestamp1  = DateTimeOffset.FromUnixTimeMilliseconds(1718611200000);
        private static readonly DateTimeOffset  timestamp2  = new (2024, 5, 23, 0, 23, 5, TimeSpan.Zero);

        #endregion


        #region DefaultHashAlgorithm_IsHMACSHA256

        /// <summary>
        /// Both implementations default to HMAC-SHA256: a TOTP generated without naming a
        /// hash algorithm must be the very same token as one generated with SHA256.
        /// </summary>
        [Test]
        public void DefaultHashAlgorithm_IsHMACSHA256()
        {

            var (_, byDefault,  _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp2);
            var (_, explicitly, _, _, _) = TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp2, null, TOTPHashAlgorithm.SHA256);

            Assert.Multiple(() => {
                Assert.That(byDefault,                        Is.EqualTo(explicitly));
                Assert.That(byDefault,                        Is.EqualTo("CN63y502maVh"));
                Assert.That(default(TOTPHashAlgorithm),       Is.EqualTo(TOTPHashAlgorithm.SHA256));
                Assert.That(TOTPHashAlgorithm.SHA256.AsText(), Is.EqualTo("sha256"));
            });

        }

        #endregion

        #region UnknownHashAlgorithm_Throws

        [Test]
        public void UnknownHashAlgorithm_Throws()
        {

            var exception = Assert.Throws<ArgumentException>(
                                () => TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, timestamp2, null, (TOTPHashAlgorithm) 42)
                            );

            Assert.That(exception?.Message, Does.StartWith("The hash algorithm must be one of: sha256, sha384, sha512!"));

        }

        #endregion

        #region InvalidTOTPLength_Throws

        /// <summary>
        /// The bound of 4 to 255 characters is part of the shared format contract:
        /// TOTP.ts rejects both ends of it, therefore this implementation does as well.
        /// </summary>
        [Test]
        public void InvalidTOTPLength_Throws()
        {

            var tooShort = Assert.Throws<ArgumentException>(
                               () => TOTPGenerator.GenerateTOTPs(sharedSecret, null, 3, null, timestamp2)
                           );

            var tooLong  = Assert.Throws<ArgumentException>(
                               () => TOTPGenerator.GenerateTOTPs(sharedSecret, null, 256, null, timestamp2)
                           );

            Assert.Multiple(() => {
                Assert.That(tooShort?.Message, Does.StartWith("The expected length of the TOTP must be between 4 and 255 characters!"));
                Assert.That(tooLong?. Message, Does.StartWith("The expected length of the TOTP must be between 4 and 255 characters!"));
            });

        }

        #endregion

        #region InvalidValidityTime_Throws

        /// <summary>
        /// The validity time must be a positive integer number of seconds -
        /// TOTP.ts enforces the same rule, therefore this implementation does
        /// as well (zero and negative values used to reach the slot arithmetic,
        /// fractional values used to be accepted silently).
        /// </summary>
        [Test]
        public void InvalidValidityTime_Throws()
        {

            var zero        = Assert.Throws<ArgumentException>(
                                  () => TOTPGenerator.GenerateTOTPs(sharedSecret, TimeSpan.Zero,               null, null, timestamp2)
                              );

            var negative    = Assert.Throws<ArgumentException>(
                                  () => TOTPGenerator.GenerateTOTPs(sharedSecret, TimeSpan.FromSeconds(-30),   null, null, timestamp2)
                              );

            var fractional  = Assert.Throws<ArgumentException>(
                                  () => TOTPGenerator.GenerateTOTPs(sharedSecret, TimeSpan.FromSeconds(4.5),   null, null, timestamp2)
                              );

            Assert.Multiple(() => {
                Assert.That(zero?.      Message, Does.StartWith("The validity time must be a positive integer number of seconds!"));
                Assert.That(negative?.  Message, Does.StartWith("The validity time must be a positive integer number of seconds!"));
                Assert.That(fractional?.Message, Does.StartWith("The validity time must be a positive integer number of seconds!"));
            });

        }

        #endregion

        #region TimestampBeforeUnixEpoch_Throws

        /// <summary>
        /// Timestamps before the Unix epoch are outside the domain of the token
        /// format - TOTP.ts rejects them with the same message. (They used to
        /// saturate to slot 0 silently.)
        /// </summary>
        [Test]
        public void TimestampBeforeUnixEpoch_Throws()
        {

            var exception = Assert.Throws<ArgumentException>(
                                () => TOTPGenerator.GenerateTOTPs(sharedSecret, null, null, null, DateTimeOffset.FromUnixTimeSeconds(-1000))
                            );

            Assert.That(exception?.Message, Does.StartWith("The timestamp must be a non-negative Unix timestamp in milliseconds!"));

        }

        #endregion


        #region TokenFormat_HashIsReadAsARingBuffer

        /// <summary>
        /// The token characters are read from the HMAC output at (offset + i) % hash length,
        /// therefore a 64 character SHA256 token repeats its first 32 characters verbatim.
        /// </summary>
        [Test]
        public void TokenFormat_HashIsReadAsARingBuffer()
        {

            var (_, current, _, _, _) = TOTPGenerator.GenerateTOTPs(
                                            sharedSecret,
                                            TimeSpan.FromSeconds(30),
                                            64,
                                            null,
                                            timestamp1
                                        );

            Assert.That(current, Is.EqualTo("akF3c7qY2uiuO4rpyU0SC0W8VFE6nvxz" +
                                            "akF3c7qY2uiuO4rpyU0SC0W8VFE6nvxz"));

        }

        #endregion

        #region TokenFormat_SHA512DoesNotRepeatWithin64Characters

        [Test]
        public void TokenFormat_SHA512DoesNotRepeatWithin64Characters()
        {

            var (_, current, _, _, _) = TOTPGenerator.GenerateTOTPs(
                                            sharedSecret,
                                            TimeSpan.FromSeconds(30),
                                            64,
                                            null,
                                            timestamp1,
                                            null,
                                            TOTPHashAlgorithm.SHA512
                                        );

            Assert.That(current[32..], Is.Not.EqualTo(current[..32]));

        }

        #endregion

        #region TokenFormat_PreviousSlotWrapsWithinTheFirstSlotAfterTheUnixEpoch

        /// <summary>
        /// Within the first slot after the Unix epoch the previous slot is calculated by
        /// unchecked UInt64 arithmetic and wraps to 2^64 - 1, which TOTP.ts mirrors via
        /// BigInt.asUintN(64, ...).
        /// </summary>
        [Test]
        public void TokenFormat_PreviousSlotWrapsWithinTheFirstSlotAfterTheUnixEpoch()
        {

            var (previous, current, next, remainingTime, _) = TOTPGenerator.GenerateTOTPs(
                                                                  sharedSecret,
                                                                  TimeSpan.FromSeconds(30),
                                                                  12,
                                                                  null,
                                                                  DateTimeOffset.FromUnixTimeSeconds(0)
                                                              );

            Assert.Multiple(() => {
                Assert.That(previous,       Is.EqualTo("SzcwtcR5qcY7"));
                Assert.That(current,        Is.EqualTo("u5CoKdo5HUS1"));
                Assert.That(next,           Is.EqualTo("tVGiyLys7Y1V"));
                Assert.That(remainingTime,  Is.EqualTo(TimeSpan.FromSeconds(30)));
            });

        }

        #endregion


        #region HashAlgorithm_ParseAndAsText

        [Test]
        public void HashAlgorithm_ParseAndAsText()
        {

            Assert.Multiple(() => {

                Assert.That(TOTPHashAlgorithmExtensions.TryParse("sha256",      out var sha256),  Is.True);
                Assert.That(sha256,                                                               Is.EqualTo(TOTPHashAlgorithm.SHA256));

                Assert.That(TOTPHashAlgorithmExtensions.TryParse("SHA-384",     out var sha384),  Is.True);
                Assert.That(sha384,                                                               Is.EqualTo(TOTPHashAlgorithm.SHA384));

                Assert.That(TOTPHashAlgorithmExtensions.TryParse("HMAC_SHA512", out var sha512),  Is.True);
                Assert.That(sha512,                                                               Is.EqualTo(TOTPHashAlgorithm.SHA512));

                Assert.That(TOTPHashAlgorithmExtensions.TryParse("sha1",        out _),           Is.False);

                Assert.That(TOTPHashAlgorithm.SHA384.AsText(),                                    Is.EqualTo("sha384"));
                Assert.That(TOTPHashAlgorithm.SHA512.AsText(),                                    Is.EqualTo("sha512"));

            });

        }

        #endregion

        #region TOTPConfig_HashAlgorithmJSONRoundtrip

        [Test]
        public void TOTPConfig_HashAlgorithmJSONRoundtrip()
        {

            Assert.That(TOTPConfig.TryParse(
                            new JObject(
                                new JProperty("sharedSecret",   sharedSecret),
                                new JProperty("hashAlgorithm",  "sha384")
                            ),
                            out var totpConfig,
                            out var errorResponse
                        ),
                        Is.True,
                        errorResponse ?? "");

            Assert.Multiple(() => {
                Assert.That(totpConfig?.HashAlgorithm,                          Is.EqualTo(TOTPHashAlgorithm.SHA384));
                Assert.That(totpConfig?.ToJSON()["hashAlgorithm"]?.Value<String>(),  Is.EqualTo("sha384"));
            });

        }

        #endregion

    }

}
