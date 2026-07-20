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
    /// The full outcome of a DMARC evaluation for one inbound message — everything needed
    /// both to enforce policy and to populate an aggregate/forensic report (RFC 7489 §7).
    /// Produced by <c>DNSVerifier.VerifyDmarcAsync</c> when a DMARC record was found.
    /// </summary>
    public sealed record DmarcEvaluation(

        String            HeaderFromDomain,   // RFC5322.From domain
        String            PolicyDomain,       // domain the DMARC record was published at (From or org)

        // Published policy (for <policy_published>)
        String            RequestedPolicy,    // p=
        String?           SubdomainPolicy,    // sp=
        String            EffectivePolicy,    // the policy that actually applies to this message
        Int32             Percent,            // pct=
        Boolean           StrictSpf,          // aspf=s
        Boolean           StrictDkim,         // adkim=s
        String?           Rua,                // aggregate report URIs
        String?           Ruf,                // forensic report URIs
        String?           FailureOptions,     // fo=

        // Authentication + alignment (for <auth_results> and <policy_evaluated>)
        SPFResult         SpfResult,
        String?           SpfDomain,          // MAIL FROM domain SPF authenticated
        Boolean           SpfAligned,
        DkimResult        DkimResult,
        String?           DkimDomain,         // d= of the passing signature
        Boolean           DkimAligned,

        // Result + applied disposition
        DmarcResult       Result,             // Pass / Fail
        DmarcDisposition  Disposition

    )
    {

        /// <summary>True when the message failed DMARC (neither SPF nor DKIM aligned).</summary>
        public Boolean Failed => Result == DmarcResult.Fail;

    }

}
