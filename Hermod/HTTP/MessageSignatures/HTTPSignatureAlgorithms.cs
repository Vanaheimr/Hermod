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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The HTTP Signature Algorithms registry (RFC 9421, Section 6.2.2): all
    /// six of its initial entries, which are all it has.
    /// </summary>
    public static class HTTPSignatureAlgorithms
    {

        /// <summary>RSASSA-PSS with SHA-512, MGF1 with SHA-512 and a salt of 64 bytes (Section 3.3.1).</summary>
        public const String RSAPSSSHA512     = "rsa-pss-sha512";

        /// <summary>RSASSA-PKCS1-v1_5 with SHA-256 (Section 3.3.2).</summary>
        public const String RSAV15SHA256     = "rsa-v1_5-sha256";

        /// <summary>HMAC with SHA-256 (Section 3.3.3).</summary>
        public const String HMACSHA256       = "hmac-sha256";

        /// <summary>ECDSA on P-256 with SHA-256, r and s of 32 octets each (Section 3.3.4).</summary>
        public const String ECDSAP256SHA256  = "ecdsa-p256-sha256";

        /// <summary>ECDSA on P-384 with SHA-384, r and s of 48 octets each (Section 3.3.5).</summary>
        public const String ECDSAP384SHA384  = "ecdsa-p384-sha384";

        /// <summary>Ed25519, over the signature base itself, without a prehash (Section 3.3.6).</summary>
        public const String Ed25519          = "ed25519";


        /// <summary>
        /// Every registered algorithm. A verifier accepts these unless it is told
        /// to accept fewer (Section 3.2, step 6.1).
        /// </summary>
        public static readonly IReadOnlySet<String> Registered = new HashSet<String>(StringComparer.Ordinal) {
                                                                     RSAPSSSHA512,
                                                                     RSAV15SHA256,
                                                                     HMACSHA256,
                                                                     ECDSAP256SHA256,
                                                                     ECDSAP384SHA384,
                                                                     Ed25519
                                                                 };

    }

}
