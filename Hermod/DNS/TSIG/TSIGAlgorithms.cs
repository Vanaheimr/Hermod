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

using Crypto = System.Security.Cryptography;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The TSIG algorithm names of RFC 8945 §6, and the HMACs behind them.
    /// </summary>
    /// <remarks>
    /// The algorithm names here collide with the .NET class names for the same
    /// primitives, so this file reaches them through the <c>Crypto</c> alias
    /// rather than an open <c>using</c>. Without it, <c>HMACSHA256.HashData</c>
    /// silently binds to the property below rather than to the algorithm.
    /// </remarks>
    public static class TSIGAlgorithms
    {

        #region Data

        /// <summary>
        /// HMAC-SHA256. RFC 8945 §6 makes this one mandatory to implement, and
        /// it is what every current generator produces by default.
        /// </summary>
        public static DomainName HMACSHA256    { get; } = DomainName.Parse("hmac-sha256.");

        /// <summary>
        /// HMAC-SHA1. Weakening, but still widely deployed.
        /// </summary>
        public static DomainName HMACSHA1      { get; } = DomainName.Parse("hmac-sha1.");

        /// <summary>
        /// HMAC-SHA224. Named for completeness; .NET has no primitive for it.
        /// </summary>
        public static DomainName HMACSHA224    { get; } = DomainName.Parse("hmac-sha224.");

        /// <summary>
        /// HMAC-SHA384.
        /// </summary>
        public static DomainName HMACSHA384    { get; } = DomainName.Parse("hmac-sha384.");

        /// <summary>
        /// HMAC-SHA512.
        /// </summary>
        public static DomainName HMACSHA512    { get; } = DomainName.Parse("hmac-sha512.");

        #endregion


        #region (static) IsSupported(Algorithm)

        /// <summary>
        /// Whether this implementation can actually compute the given algorithm.
        /// </summary>
        /// <param name="Algorithm">A TSIG algorithm name.</param>
        /// <remarks>
        /// HMAC-SHA224 is a registered name that this returns false for, and
        /// HMAC-MD5 is absent entirely: RFC 8945 §6 lists it as MAY, and .NET's
        /// MD5 is unavailable on a FIPS-restricted host, which would let the
        /// build environment decide whether the code path exists.
        /// </remarks>
        public static Boolean IsSupported(DomainName Algorithm)

            => Algorithm == HMACSHA1   ||
               Algorithm == HMACSHA256 ||
               Algorithm == HMACSHA384 ||
               Algorithm == HMACSHA512;

        #endregion

        #region (static) ComputeHMAC(Algorithm, Secret, Data)

        /// <summary>
        /// Compute the HMAC of the given data under the given algorithm.
        /// </summary>
        /// <param name="Algorithm">A TSIG algorithm name.</param>
        /// <param name="Secret">The shared secret.</param>
        /// <param name="Data">The data to authenticate.</param>
        public static Byte[] ComputeHMAC(DomainName  Algorithm,
                                         Byte[]      Secret,
                                         Byte[]      Data)
        {

            if (Algorithm == HMACSHA256)  return Crypto.HMACSHA256.HashData(Secret, Data);
            if (Algorithm == HMACSHA1)    return Crypto.HMACSHA1.  HashData(Secret, Data);
            if (Algorithm == HMACSHA384)  return Crypto.HMACSHA384.HashData(Secret, Data);
            if (Algorithm == HMACSHA512)  return Crypto.HMACSHA512.HashData(Secret, Data);

            throw new NotSupportedException($"Unsupported TSIG algorithm '{Algorithm}'.");

        }

        #endregion

    }

}
