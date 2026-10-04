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
/// Matches a TLS server certificate against DANE TLSA records (RFC 6698 §2.1, RFC 7672 §3.1).
/// </summary>
public static class DaneAuthenticator
{

    /// <summary>
    /// Whether the presented server certificate (and chain) satisfies at least one
    /// of the given TLSA records under SMTP DANE rules.
    /// </summary>
    /// <param name="Records">The DNSSEC-validated TLSA records.</param>
    /// <param name="Leaf">The server's end-entity certificate.</param>
    /// <param name="Chain">The certificate chain presented by the server (may be null/incomplete).</param>
    /// <param name="ReferenceIdentifiers">
    /// The names the certificate must carry when a DANE-TA(2) record authenticates it (RFC 7672
    /// §3.2.2, §3.2.3): the TLSA base domain - the MX host or the relay - and, for an MX host, the
    /// next-hop domain.
    /// </param>
    /// <param name="Logger">A logger.</param>
    public static Boolean Matches(IReadOnlyList<TLSA>          Records,
                                  X509Certificate2             Leaf,
                                  X509Chain?                   Chain,
                                  IReadOnlyCollection<String>  ReferenceIdentifiers,
                                  ILogger                      Logger)
    {

        foreach (var record in Records)
        {

            switch ((TLSA_CertificateUsage) record.CertificateUsage)
            {

                // DANE-EE(3): the TLSA record directly authenticates the end-entity
                // certificate. No PKIX path or name check is required (RFC 7672 §3.1.1).
                case TLSA_CertificateUsage.DANE_EE:
                    if (SelectedDataMatches(record, Leaf))
                        return true;
                    break;

                // DANE-TA(2): the TLSA record names a trust anchor that must appear in the
                // certificate chain presented by the server (RFC 7672 §3.1.2) - and the server
                // certificate must carry one of the reference identifiers (§3.2.2): "With DANE-TA(2),
                // the server certificate MUST contain a name that matches one of the reference
                // identifiers". Else any certificate the trust anchor issued would do - with a
                // public CA's intermediate as the anchor, any certificate of that CA.
                case TLSA_CertificateUsage.DANE_TA:
                    var anchored = Chain is not null &&
                                   Chain.ChainElements.Any(element => SelectedDataMatches(record, element.Certificate));
                    if (anchored && NameMatches(Leaf, ReferenceIdentifiers))
                        return true;
                    if (anchored)
                        Logger.Log(LogLevel.Warning,
                                   $"DANE: the certificate '{Leaf.Subject}' chains to the DANE-TA trust anchor but names none of {String.Join(", ", ReferenceIdentifiers)}");
                    break;

                // PKIX-TA(0) / PKIX-EE(1) additionally require WebPKI validation, which is
                // unreliable for SMTP MX certificates; RFC 7672 §3.1.3 says these SHOULD NOT
                // be published and receivers commonly ignore them. We do not honour them.
                default:
                    Logger.Log(LogLevel.Debug,
                               $"DANE: ignoring unusable TLSA usage {record.CertificateUsage} (SMTP uses only DANE-EE/DANE-TA)");
                    break;

            }

        }

        return false;

    }


    // RFC 7672 §3.2.3: the subject alternative names, or the common name when there are none; a
    // wildcard stands for the left-most label only.
    private static Boolean NameMatches(X509Certificate2 Certificate, IReadOnlyCollection<String> ReferenceIdentifiers)
        => ReferenceIdentifiers.Any(name => Certificate.MatchesHostname(name.TrimEnd('.'), allowWildcards: true, allowCommonName: true));

    // Compare the selected certificate data (per Selector + MatchingType) against the
    // record's association data in constant time.
    private static Boolean SelectedDataMatches(TLSA record, X509Certificate2 certificate)
    {

        Byte[]? selected = (TLSA_Selector) record.Selector switch {
            TLSA_Selector.FullCertificate       => certificate.RawData,
            TLSA_Selector.SubjectPublicKeyInfo  => certificate.PublicKey.ExportSubjectPublicKeyInfo(),
            _                                   => null
        };

        if (selected is null)
            return false;

        Byte[]? computed = (TLSA_MatchingType) record.MatchingType switch {
            TLSA_MatchingType.Full    => selected,
            TLSA_MatchingType.SHA256  => SHA256.HashData(selected),
            TLSA_MatchingType.SHA512  => SHA512.HashData(selected),
            _                        => null
        };

        if (computed is null)
            return false;

        return CryptographicOperations.FixedTimeEquals(computed, record.CertificateAssociationData);

    }

}
