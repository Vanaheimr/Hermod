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
    /// The result of verifying an HTTP message signature.
    /// </summary>
    /// <param name="Status">What the verification came to.</param>
    /// <param name="Label">The label of the signature it came to that for, if one was chosen.</param>
    /// <param name="Parameters">That signature's parameters, if they parsed.</param>
    /// <param name="Key">The key it was verified with, if one was found.</param>
    /// <param name="Error">Why it failed, in words, when it did.</param>
    public sealed record HTTPSignatureVerification(HTTPSignatureVerificationStatus  Status,
                                                   String?                          Label        = null,
                                                   HTTPSignatureParameters?         Parameters   = null,
                                                   HTTPSignatureKey?                Key          = null,
                                                   String?                          Error        = null)
    {

        /// <summary>
        /// Whether the signature verified and met every requirement.
        /// </summary>
        public Boolean IsValid
            => Status == HTTPSignatureVerificationStatus.Valid;

        /// <inheritdoc/>
        public override String ToString()

            => Error is null
                   ? $"{Status}{(Label is null ? "" : $" ({Label})")}"
                   : $"{Status}{(Label is null ? "" : $" ({Label})")}: {Error}";

    }

}
