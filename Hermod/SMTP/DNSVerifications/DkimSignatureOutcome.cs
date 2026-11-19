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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    /// <summary>
    /// One DKIM signature of a message as it was verified: its d=, i= and s= tags and its
    /// result - what a failure report names (RFC 6591 §3.2.3: DKIM-Domain, DKIM-Identity,
    /// DKIM-Selector; RFC 7489 §7.3.1).
    /// </summary>
    /// <param name="Domain">The d= tag: the signing domain.</param>
    /// <param name="Identity">The i= tag, if the signature has one; RFC 6376 §3.5: the default is "@" d=.</param>
    /// <param name="Selector">The s= tag.</param>
    /// <param name="Result">Whether the signature holds.</param>
    public sealed record DkimSignatureOutcome(String      Domain,
                                              String?     Identity,
                                              String?     Selector,
                                              DkimResult  Result)
    {

        /// <summary>
        /// The signing identity: the i= tag, or "@" and the signing domain (RFC 6376 §3.5).
        /// </summary>
        public String AgentOrUserIdentifier
            => String.IsNullOrEmpty(Identity) ? "@" + Domain : Identity;

    }

}
