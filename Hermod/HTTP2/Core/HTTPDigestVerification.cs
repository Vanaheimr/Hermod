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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    /// <summary>
    /// The outcome of checking an RFC 9530 digest field against the octets it
    /// claims to describe.
    ///
    /// The three non-matching outcomes are deliberately kept apart: "there was no
    /// digest" and "there was one we cannot compute" are *not* verifications, and
    /// a caller that requires integrity has to treat them as such. Collapsing
    /// either into a boolean <c>true</c> would quietly turn an unchecked body into
    /// a checked one.
    /// </summary>
    public enum HTTPDigestVerification
    {

        /// <summary>No digest field was present — nothing was checked.</summary>
        NotPresent,

        /// <summary>
        /// A digest field was present, but named only algorithms this stack will
        /// not compute (see <see cref="HTTPDigest.Supported"/>) — nothing was
        /// checked.
        /// </summary>
        Unsupported,

        /// <summary>The digest was computed and matched the octets.</summary>
        Match,

        /// <summary>The digest was computed and did <b>not</b> match the octets.</summary>
        Mismatch

    }

}
