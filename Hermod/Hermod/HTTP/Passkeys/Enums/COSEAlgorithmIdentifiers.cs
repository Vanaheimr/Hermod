/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Runtime.CompilerServices;

namespace org.GraphDefined.Vanaheimr.Hermod.Passkeys
{

    // https://www.iana.org/assignments/cose/cose.xhtml#algorithms

    public enum COSEAlgorithmIdentifiers : Int32
    {

        /// <summary>
        /// RSASSA PKCS1 v1.5/SHA-512
        /// </summary>
        [Obsolete("Use ES512 instead!")]
        RS512   =  -259,

        /// <summary>
        /// RSASSA PKCS1 v1.5/SHA-384
        /// </summary>
        [Obsolete("Use ES384 instead!")]
        RS384   =  -258,

        /// <summary>
        /// RSASSA PKCS1 v1.5/SHA-256
        /// </summary>
        [Obsolete("Use ES256/ES256K instead!")]
        RS256   =  -257,


        /// <summary>
        ///  ECDSA secp256k1/SHA-256 [RFC8812] [RFC9053]
        /// </summary>
        ES256K  =   -36,


        /// <summary>
        ///  ECDSA P-521/SHA-512 [RFC9053]
        /// </summary>
        ES512   =   -36,

        /// <summary>
        /// ECDSA P-384/SHA-384 [RFC9053]
        /// </summary>
        ES384   =   -35,

        /// <summary>
        /// EdDSA Ed25519 [RFC9053]
        /// </summary>
        EdDSA   =    -8,

        /// <summary>
        /// ECDSA P-256/SHA-256 [RFC9053]
        /// </summary>
        ES256   =    -7

    }

}
