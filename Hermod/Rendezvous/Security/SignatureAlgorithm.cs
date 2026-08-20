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
    /// The signature algorithms of the rendezvous control protocol.
    ///
    /// There are deliberately only two families: Ed25519 as the small and fast
    /// classical signature, and ML-DSA (FIPS 204) as its post-quantum successor.
    /// Requiring one of each is the usual hybrid stance during the migration -
    /// see RendezvousOptions.RequiredSignatures.
    ///
    /// The numeric values are the COSE algorithm identifications of the IANA
    /// COSE Algorithms registry, so that the wire format does not invent yet
    /// another numbering.
    /// </summary>
    public enum SignatureAlgorithm
    {

        /// <summary>
        /// EdDSA over Curve25519 (Ed25519, RFC 8032): 32 byte keys, 64 byte signatures.
        /// </summary>
        EdDSA      =  -8,

        /// <summary>
        /// ML-DSA-44 (FIPS 204): 1312 byte keys, 2420 byte signatures.
        /// </summary>
        MLDsa44    = -48,

        /// <summary>
        /// ML-DSA-65 (FIPS 204): 1952 byte keys, 3309 byte signatures.
        /// </summary>
        MLDsa65    = -49,

        /// <summary>
        /// ML-DSA-87 (FIPS 204): 2592 byte keys, 4627 byte signatures.
        /// </summary>
        MLDsa87    = -50

    }


    /// <summary>
    /// Extension methods for signature algorithms.
    /// </summary>
    public static class SignatureAlgorithmExtensions
    {

        /// <summary>
        /// Whether the given signature algorithm is post-quantum secure.
        /// </summary>
        /// <param name="Algorithm">A signature algorithm.</param>
        public static Boolean IsPostQuantum(this SignatureAlgorithm Algorithm)

            => Algorithm is SignatureAlgorithm.MLDsa44 or
                            SignatureAlgorithm.MLDsa65 or
                            SignatureAlgorithm.MLDsa87;

        /// <summary>
        /// Whether the given signature algorithm is a known one.
        /// </summary>
        /// <param name="Algorithm">A signature algorithm.</param>
        public static Boolean IsDefined(this SignatureAlgorithm Algorithm)

            => Algorithm is SignatureAlgorithm.EdDSA   or
                            SignatureAlgorithm.MLDsa44 or
                            SignatureAlgorithm.MLDsa65 or
                            SignatureAlgorithm.MLDsa87;

    }

}
