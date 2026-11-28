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
    /// What an application requires of an HTTP message signature before it
    /// accepts it (RFC 9421, Section 3.2.1), beyond the requirements of the RFC
    /// itself, which always apply.
    /// </summary>
    /// <remarks>
    /// The defaults accept any registered algorithm, any coverage, a signature
    /// without <c>created</c> and one of any age, and reject one whose
    /// <c>expires</c> has passed. That is the least an application can ask for,
    /// and Section 7.2.1 explains why it is rarely enough: a signature that
    /// covers nothing proves only that someone had the key once.
    /// </remarks>
    public sealed class HTTPSignatureVerificationOptions
    {

        /// <summary>
        /// The key to verify a signature with, from its parameters - <c>keyid</c>
        /// and <c>alg</c> above all - or null when there is none that may verify
        /// it (Section 3.2, step 5).
        /// </summary>
        public required Func<HTTPSignatureParameters, HTTPSignatureKey?>  KeyResolver              { get; init; }

        /// <summary>
        /// The label of the signature to verify. Without one, every signature
        /// is a candidate, in the order of <c>Signature-Input</c>, and the first
        /// that verifies is the one.
        /// </summary>
        public String?                                                    Label                    { get; init; }

        /// <summary>
        /// The tag a signature must have to be a candidate (Section 2.3).
        /// </summary>
        public String?                                                    Tag                      { get; init; }

        /// <summary>
        /// The components a signature must cover, in any order.
        /// </summary>
        public IReadOnlyList<HTTPSignatureComponent>                      RequiredComponents       { get; init; } = [];

        /// <summary>
        /// The algorithms to accept (Section 3.2, step 6.1).
        /// </summary>
        public IReadOnlySet<String>                                       AllowedAlgorithms        { get; init; } = HTTPSignatureAlgorithms.Registered;

        /// <summary>
        /// Whether a signature must have <c>created</c>.
        /// </summary>
        public Boolean                                                    RequireCreated           { get; init; }

        /// <summary>
        /// How long ago <c>created</c> may be, if there is a limit. Implies
        /// <see cref="RequireCreated"/>.
        /// </summary>
        public TimeSpan?                                                  MaximumAge               { get; init; }

        /// <summary>
        /// Whether a signature whose <c>expires</c> has passed is refused.
        /// </summary>
        public Boolean                                                    RejectExpired            { get; init; } = true;

        /// <summary>
        /// How far the signer's clock may be ahead of this one.
        /// </summary>
        public TimeSpan                                                   ClockSkew                { get; init; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The time to verify at, now by default.
        /// </summary>
        public DateTimeOffset?                                            Now                      { get; init; }

        /// <summary>
        /// The Structured Field types for <c>sf</c> and <c>key</c>,
        /// <see cref="HTTPSignatureBase.KnownStructuredFields"/> by default.
        /// </summary>
        public IReadOnlyDictionary<String, HTTPStructuredFieldType>?      StructuredFieldTypes     { get; init; }

    }

}
