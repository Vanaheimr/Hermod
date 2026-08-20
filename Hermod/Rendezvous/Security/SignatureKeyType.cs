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

namespace org.GraphDefined.Vanaheimr.Hermod.Rendezvous
{

    /// <summary>
    /// The key types of the rendezvous control protocol.
    ///
    /// This is what a key *is*, while <see cref="SignatureAlgorithm"/> is what
    /// travels on the wire. The two are not the same: COSE identifies both
    /// Ed25519 and Ed448 as "EdDSA" (-8) and leaves the curve to the key, so
    /// only the key can tell how long its signatures are.
    /// </summary>
    public enum SignatureKeyType
    {

        /// <summary>
        /// Ed25519 (RFC 8032): 32 byte keys, 64 byte signatures, ~128 bit classical security.
        /// </summary>
        Ed25519,

        /// <summary>
        /// Ed448 (RFC 8032): 57 byte keys, 114 byte signatures, ~224 bit classical security.
        /// </summary>
        Ed448,

        /// <summary>
        /// ML-DSA-44 (FIPS 204): 1312 byte keys, 2420 byte signatures, NIST security level 2.
        /// </summary>
        MLDsa44,

        /// <summary>
        /// ML-DSA-65 (FIPS 204): 1952 byte keys, 3309 byte signatures, NIST security level 3.
        /// </summary>
        MLDsa65,

        /// <summary>
        /// ML-DSA-87 (FIPS 204): 2592 byte keys, 4627 byte signatures, NIST security level 5.
        /// </summary>
        MLDsa87

    }


    /// <summary>
    /// Extension methods for signature key types.
    /// </summary>
    public static class SignatureKeyTypeExtensions
    {

        #region PublicKeySize(this KeyType)

        /// <summary>
        /// Return the size of a public key of the given key type in bytes.
        /// </summary>
        /// <param name="KeyType">A key type.</param>
        public static Int32 PublicKeySize(this SignatureKeyType KeyType)

            => KeyType switch {
                   SignatureKeyType.Ed25519  =>   32,
                   SignatureKeyType.Ed448    =>   57,
                   SignatureKeyType.MLDsa44  => 1312,
                   SignatureKeyType.MLDsa65  => 1952,
                   SignatureKeyType.MLDsa87  => 2592,
                   _                         => throw new ArgumentException($"Unknown key type: {KeyType}!", nameof(KeyType))
               };

        #endregion

        #region SignatureSize(this KeyType)

        /// <summary>
        /// Return the size of a signature of the given key type in bytes.
        /// </summary>
        /// <param name="KeyType">A key type.</param>
        public static Int32 SignatureSize(this SignatureKeyType KeyType)

            => KeyType switch {
                   SignatureKeyType.Ed25519  =>   64,
                   SignatureKeyType.Ed448    =>  114,
                   SignatureKeyType.MLDsa44  => 2420,
                   SignatureKeyType.MLDsa65  => 3309,
                   SignatureKeyType.MLDsa87  => 4627,
                   _                         => throw new ArgumentException($"Unknown key type: {KeyType}!", nameof(KeyType))
               };

        #endregion

        #region Algorithm(this KeyType)

        /// <summary>
        /// Return the COSE signature algorithm a key of the given type signs with.
        /// </summary>
        /// <param name="KeyType">A key type.</param>
        public static SignatureAlgorithm Algorithm(this SignatureKeyType KeyType)

            => KeyType switch {
                   SignatureKeyType.Ed25519  => SignatureAlgorithm.EdDSA,
                   SignatureKeyType.Ed448    => SignatureAlgorithm.EdDSA,
                   SignatureKeyType.MLDsa44  => SignatureAlgorithm.MLDsa44,
                   SignatureKeyType.MLDsa65  => SignatureAlgorithm.MLDsa65,
                   SignatureKeyType.MLDsa87  => SignatureAlgorithm.MLDsa87,
                   _                         => throw new ArgumentException($"Unknown key type: {KeyType}!", nameof(KeyType))
               };

        #endregion

        #region IsPostQuantum(this KeyType)

        /// <summary>
        /// Whether the given key type is post-quantum secure.
        /// </summary>
        /// <param name="KeyType">A key type.</param>
        public static Boolean IsPostQuantum(this SignatureKeyType KeyType)

            => KeyType is SignatureKeyType.MLDsa44 or
                          SignatureKeyType.MLDsa65 or
                          SignatureKeyType.MLDsa87;

        #endregion

        #region SignatureSizesOf(Algorithm)

        /// <summary>
        /// Return every signature size the given COSE algorithm can produce.
        /// EdDSA has two, as it covers both Ed25519 and Ed448.
        /// </summary>
        /// <param name="Algorithm">A signature algorithm.</param>
        public static IEnumerable<Int32> SignatureSizesOf(SignatureAlgorithm Algorithm)

            => Algorithm switch {
                   SignatureAlgorithm.EdDSA    => [SignatureKeyType.Ed25519.SignatureSize(),
                                                   SignatureKeyType.Ed448.  SignatureSize()],
                   SignatureAlgorithm.MLDsa44  => [SignatureKeyType.MLDsa44.SignatureSize()],
                   SignatureAlgorithm.MLDsa65  => [SignatureKeyType.MLDsa65.SignatureSize()],
                   SignatureAlgorithm.MLDsa87  => [SignatureKeyType.MLDsa87.SignatureSize()],
                   _                           => []
               };

        #endregion

    }

}
