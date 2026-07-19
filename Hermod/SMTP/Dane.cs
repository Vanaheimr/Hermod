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

#region DANE status / result

/// <summary>
/// The usability of a DANE (RFC 7672) TLSA lookup for a given MX host.
/// </summary>
public enum DaneStatus
{

    /// <summary>No TLSA records are published (or their absence is DNSSEC-authenticated). DANE does not apply; delivery may proceed opportunistically.</summary>
    NoRecord,

    /// <summary>Usable TLSA records were returned and DNSSEC-validated. TLS MUST be enforced and the server certificate MUST match a TLSA record.</summary>
    Secure,

    /// <summary>TLSA records were returned but DNSSEC validation failed (bogus / indeterminate). The destination MUST be treated as broken and delivery deferred.</summary>
    Bogus,

    /// <summary>TLSA records were returned but the zone is not DNSSEC-signed, so they are not authenticated and (per RFC 7672 §2.2) not usable for DANE.</summary>
    Insecure

}


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

    /// <summary>DANE applies and the certificate must be matched against <see cref="Records"/>.</summary>
    public Boolean  IsUsable
        => Status == DaneStatus.Secure && Records.Count > 0;

    /// <summary>The lookup proved the destination is DANE-protected but the records could not be trusted; delivery must be deferred.</summary>
    public Boolean  MustDefer
        => Status == DaneStatus.Bogus;

    public static DaneResult None(String? Detail = null)
        => new (DaneStatus.NoRecord, [], Detail);

}

#endregion

#region DANE resolver

/// <summary>
/// Resolves and DNSSEC-validates DANE TLSA records (RFC 6698 / RFC 7672) for outbound SMTP delivery.
/// </summary>
public sealed class DaneResolver
{

    private readonly DNSClient        dnsClient;
    private readonly DNSSECValidator  dnssecValidator;
    private readonly ILogger          logger;

    /// <summary>
    /// Create a new DANE resolver.
    /// </summary>
    /// <param name="DNSClient">A DNS client. Its DNSSEC-OK (DO) bit is enabled so RRSIG records are returned.</param>
    /// <param name="Logger">A logger.</param>
    /// <param name="DNSSECValidator">An optional DNSSEC validator; if omitted, one is created with the IANA root trust anchor.</param>
    public DaneResolver(DNSClient        DNSClient,
                        ILogger          Logger,
                        DNSSECValidator? DNSSECValidator   = null)
    {

        this.dnsClient        = DNSClient;
        this.logger           = Logger;
        this.dnssecValidator  = DNSSECValidator ?? DNS.DNSSECValidator.WithRootTrustAnchor(DNSClient);

        // DANE is meaningless without DNSSEC: make sure every query requests the
        // RRSIG/DNSKEY/DS records the validator needs (RFC 4035 §3.2.1).
        this.dnsClient.DnssecOK = true;

    }


    /// <summary>
    /// Look up and DNSSEC-validate the TLSA records for the given MX host and port
    /// (owner name "_&lt;port&gt;._tcp.&lt;mxHost&gt;", RFC 7672 §2.1).
    /// </summary>
    /// <param name="MxHost">The MX host name.</param>
    /// <param name="Port">The SMTP port (25 for MX delivery).</param>
    /// <param name="CancellationToken">An optional cancellation token.</param>
    public async Task<DaneResult> ResolveTlsaAsync(String             MxHost,
                                                   UInt16             Port                = 25,
                                                   CancellationToken  CancellationToken   = default)
    {

        var owner = $"_{Port}._tcp.{MxHost.TrimEnd('.')}";

        DNSInfo response;

        try
        {
            response = await dnsClient.Query(
                                 DNSServiceName.Parse(owner),
                                 [ DNSResourceRecordTypes.TLSA ],
                                 CancellationToken: CancellationToken
                             ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Warning, $"DANE: TLSA lookup for '{owner}' failed: {ex.Message}");
            return DaneResult.None($"lookup error: {ex.Message}");
        }

        var tlsaRecords = response.Answers.OfType<TLSA>().ToList();

        if (tlsaRecords.Count == 0)
            return DaneResult.None("no TLSA records");

        // The records only count if the zone is DNSSEC-signed and validates.
        var dnssec = await dnssecValidator.ValidateAsync(response, CancellationToken).ConfigureAwait(false);

        logger.Log(LogLevel.Debug,
                   $"DANE: '{owner}' returned {tlsaRecords.Count} TLSA record(s), DNSSEC={dnssec}");

        return dnssec switch {

            DNSSECValidationResult.Secure         => new DaneResult(DaneStatus.Secure,   tlsaRecords),

            // Records claim to be signed but the chain of trust is broken or could not be
            // completed: fail closed and defer rather than deliver over an unauthenticated channel.
            DNSSECValidationResult.Bogus          => new DaneResult(DaneStatus.Bogus,    tlsaRecords, "DNSSEC validation bogus"),
            DNSSECValidationResult.Indeterminate  => new DaneResult(DaneStatus.Bogus,    tlsaRecords, "DNSSEC validation indeterminate"),

            // TLSA published in an unsigned zone: not authenticated, so not usable for DANE.
            _                                     => new DaneResult(DaneStatus.Insecure, tlsaRecords, "zone not DNSSEC-signed")

        };

    }

}

#endregion

#region DANE certificate authenticator

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
    /// <param name="Logger">A logger.</param>
    public static Boolean Matches(IReadOnlyList<TLSA>  Records,
                                  X509Certificate2     Leaf,
                                  X509Chain?           Chain,
                                  ILogger              Logger)
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
                // certificate chain presented by the server (RFC 7672 §3.1.1).
                case TLSA_CertificateUsage.DANE_TA:
                    if (Chain is not null)
                        foreach (var element in Chain.ChainElements)
                            if (SelectedDataMatches(record, element.Certificate))
                                return true;
                    if (SelectedDataMatches(record, Leaf))
                        return true;
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

#endregion
