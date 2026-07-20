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
