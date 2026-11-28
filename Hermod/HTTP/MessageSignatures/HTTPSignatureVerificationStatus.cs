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
    /// The outcome of verifying an HTTP message signature (RFC 9421, Section
    /// 3.2). Everything but <see cref="Valid"/> is a failed verification; the
    /// rest say which step failed, because "no signature", "a signature in a
    /// key we do not know" and "a forged signature" call for different answers.
    /// </summary>
    public enum HTTPSignatureVerificationStatus
    {

        /// <summary>The signature verified, and met every requirement.</summary>
        Valid,

        /// <summary>The message carries no <c>Signature-Input</c> and no <c>Signature</c>.</summary>
        NotPresent,

        /// <summary><c>Signature-Input</c> or <c>Signature</c> does not parse, a label is in one and not the other, or a label occurs twice (Sections 4.1 and 4.2).</summary>
        Malformed,

        /// <summary>No signature has the label or the tag asked for (Section 3.2, step 1.1).</summary>
        NoMatchingSignature,

        /// <summary>A component the application requires is not covered (Section 3.2.1).</summary>
        InsufficientCoverage,

        /// <summary>The application requires <c>created</c>, and there is none.</summary>
        CreatedMissing,

        /// <summary><c>created</c> is later than now, by more than the clock skew allowed.</summary>
        CreatedInTheFuture,

        /// <summary><c>created</c> is longer ago than the application accepts.</summary>
        TooOld,

        /// <summary><c>expires</c> has passed.</summary>
        Expired,

        /// <summary>No key is known for the signature (Section 3.2, step 5).</summary>
        UnknownKey,

        /// <summary>The algorithm is not one the application accepts (Section 3.2, step 6.1).</summary>
        AlgorithmNotAllowed,

        /// <summary>The <c>alg</c> parameter names another algorithm than the key's (Section 3.2, step 6.5).</summary>
        AlgorithmMismatch,

        /// <summary>The signature base cannot be created from the message (Section 2.5).</summary>
        SignatureBaseError,

        /// <summary>The signature does not verify (Section 3.2, step 8).</summary>
        Invalid

    }

}
