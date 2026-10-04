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

#region Usings

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// The outcome of a DANE TLSA lookup for one MX host.
/// </summary>
/// <param name="Status">The usability of the lookup.</param>
/// <param name="Records">The TLSA records returned (only trustworthy when <see cref="Status"/> is <see cref="DaneStatus.Secure"/>).</param>
/// <param name="Detail">An optional human-readable explanation.</param>
public sealed record DaneResult(DaneStatus            Status,
                                IReadOnlyList<TLSA>   Records,
                                String?               Detail   = null)
{

    /// <summary>
    /// DANE applies and the certificate must be matched against the usable <see cref="Records"/>:
    /// a "secure" TLSA RRset with at least one usable record (RFC 7672 §2.2).
    /// </summary>
    public Boolean  IsUsable
        => Status == DaneStatus.Secure && Records.Any(IsUsableRecord);

    /// <summary>
    /// The server has committed to TLS: a "secure" TLSA RRset, usable or not. RFC 7672 §2.2: with only
    /// unusable records "Any connection to the MTA MUST be made via TLS, but authentication is not
    /// required"; §2.2.3: "The SMTP client MUST NOT deliver mail via the corresponding host unless a
    /// TLS session is negotiated via STARTTLS."
    /// </summary>
    public Boolean  RequiresTls
        => Status == DaneStatus.Secure && Records.Count > 0;

    /// <summary>
    /// Whether DANE applies cannot be known, or the records cannot be trusted: delivery via this
    /// server must be deferred (RFC 7672 §2.1.2).
    /// </summary>
    public Boolean  MustDefer
        => Status is DaneStatus.Bogus or DaneStatus.LookupFailed;

    /// <summary>
    /// A record SMTP can use (RFC 7672 §3.1): DANE-TA(2) or DANE-EE(3), a full certificate or a
    /// SubjectPublicKeyInfo, as they are or as SHA-256 or SHA-512. PKIX-TA(0) and PKIX-EE(1) are
    /// not used for SMTP (§3.1.3).
    /// </summary>
    public static Boolean IsUsableRecord(TLSA Record)
        => (TLSA_CertificateUsage) Record.CertificateUsage is TLSA_CertificateUsage.DANE_TA or TLSA_CertificateUsage.DANE_EE &&
           (TLSA_Selector)         Record.Selector         is TLSA_Selector.FullCertificate or TLSA_Selector.SubjectPublicKeyInfo &&
           (TLSA_MatchingType)     Record.MatchingType     is TLSA_MatchingType.Full or TLSA_MatchingType.SHA256 or TLSA_MatchingType.SHA512;

    public static DaneResult None(String? Detail = null)
        => new (DaneStatus.NoRecord, [], Detail);

    public static DaneResult Failed(String Detail)
        => new (DaneStatus.LookupFailed, [], Detail);

}
