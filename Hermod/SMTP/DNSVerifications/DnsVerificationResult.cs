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

    public sealed record DnsVerificationResult(
        SPFResult   Spf,
        String?     SpfRecord,
        DkimResult  Dkim,
        String?     DkimDetails,
        DmarcResult Dmarc,
        String?     DmarcPolicy,
        String[]    MxRecords,
        String?           DkimDomain = null,   // the d= tag of the evaluated DKIM signature - the author's, where one of theirs holds
        DmarcEvaluation?  DmarcDetail = null,  // full DMARC outcome for policy + reporting (RFC 7489 §7)
        ArcResult         Arc = ArcResult.None,// Authenticated Received Chain status (RFC 8617)
        Boolean           DkimAligned = false, // a DKIM signature of the From: domain holds - aligned as its DMARC record asks, relaxed without one
        IReadOnlyList<String>? Blocklists = null // the DNS blocklists (RFC 5782) that name the client's address, where they only mark (SMTPServerConfig.DnsBlocklists)
    );

}
