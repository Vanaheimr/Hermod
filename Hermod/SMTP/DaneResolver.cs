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

    private readonly IDNSClient       dnsClient;
    private readonly DNSSECValidator  dnssecValidator;
    private readonly ILogger          logger;

    /// <summary>
    /// Create a new DANE resolver.
    /// </summary>
    /// <param name="DNSClient">A DNS client. Its DNSSEC-OK (DO) bit is enabled so RRSIG records are returned.</param>
    /// <param name="Logger">A logger.</param>
    /// <param name="DNSSECValidator">An optional DNSSEC validator; if omitted, one is created with the IANA root trust anchors.</param>
    public DaneResolver(IDNSClient       DNSClient,
                        ILogger          Logger,
                        DNSSECValidator? DNSSECValidator   = null)
    {

        ArgumentNullException.ThrowIfNull(DNSClient);

        if (DNSClient is not IDNSClientWithDNSSEC dnssecClient)
            throw new ArgumentException("DANE requires a DNS client that supports DNSSEC query configuration!", nameof(DNSClient));

        this.dnsClient        = DNSClient;
        this.logger           = Logger;
        this.dnssecValidator  = DNSSECValidator ?? DNS.DNSSECValidator.WithRootTrustAnchor(DNSClient);

        // DANE is meaningless without DNSSEC: make sure every query requests the
        // RRSIG/DNSKEY/DS records the validator needs (RFC 4035 §3.2.1).
        dnssecClient.DnssecOK = true;

    }


    /// <summary>
    /// Look up and DNSSEC-validate the TLSA records for the given MX host and port
    /// (owner name "_&lt;port&gt;._tcp.&lt;mxHost&gt;", RFC 7672 §2.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 7672 §2.2.2: "the SMTP client MUST perform any A and/or AAAA queries for the destination
    /// before attempting to locate the associated TLSA records ... If address records are found but
    /// the DNSSEC validation status of the first query response is 'insecure', the SMTP client
    /// SHOULD NOT proceed to search for any associated TLSA records" - nameservers of unsigned
    /// zones that answer TLSA queries with SERVFAIL would otherwise hold mail for good.
    /// </para>
    /// <para>
    /// Once the host is known to be in a signed zone, every failure counts (§2.1.2): an error,
    /// a timeout, records that do not validate - and "no TLSA records" without a validated denial
    /// of existence, which is what an attacker who strips the records produces (RFC 4035 §5.4).
    /// </para>
    /// </remarks>
    /// <param name="MxHost">The MX host name.</param>
    /// <param name="Port">The SMTP port (25 for MX delivery).</param>
    /// <param name="CancellationToken">An optional cancellation token.</param>
    public async Task<DaneResult> ResolveTlsaAsync(String             MxHost,
                                                   UInt16             Port                = 25,
                                                   CancellationToken  CancellationToken   = default)
    {

        var host  = MxHost.TrimEnd('.');
        var owner = $"_{Port}._tcp.{host}";

        #region The address records first (§2.2.2)

        DNSSECValidationResult addressStatus = DNSSECValidationResult.Insecure;
        var                    addressFound  = false;

        foreach (var type in new[] { DNSResourceRecordTypes.A, DNSResourceRecordTypes.AAAA })
        {

            DNSInfo addresses;

            try
            {
                addresses = await dnsClient.Query(DomainName.Parse(host), [ type ], CancellationToken: CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Warning, $"DANE: {type} lookup for '{host}' failed: {ex.Message}");
                return DaneResult.Failed($"{type} lookup error: {ex.Message}");
            }

            if (Failed(addresses))
                return DaneResult.Failed($"{type} lookup for {host}: {addresses.ResponseCode}");

            if (!addresses.Answers.Any(record => record.Type == type))
                continue;

            addressFound  = true;
            addressStatus = await dnssecValidator.ValidateAsync(addresses, CancellationToken: CancellationToken).ConfigureAwait(false);
            break;

        }

        if (!addressFound)
            return DaneResult.Failed($"no address records for {host}");

        if (addressStatus is DNSSECValidationResult.Bogus or DNSSECValidationResult.Indeterminate)
            return new DaneResult(DaneStatus.Bogus, [], $"the address records of {host} did not validate ({addressStatus})");

        if (addressStatus != DNSSECValidationResult.Secure)
            return DaneResult.None($"the address records of {host} are not DNSSEC-signed; no TLSA lookup (RFC 7672 §2.2.2)");

        #endregion

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
            return DaneResult.Failed($"lookup error: {ex.Message}");
        }

        if (Failed(response))
            return DaneResult.Failed($"TLSA lookup for {owner}: {response.ResponseCode}");

        var tlsaRecords = response.Answers.OfType<TLSA>().ToList();

        // No records in a signed zone: true only with a validated denial of existence.
        if (tlsaRecords.Count == 0)
        {

            var denial = await dnssecValidator.ValidateAsync(response,
                                                             (DomainName.ParseLenient(owner), DNSResourceRecordTypes.TLSA),
                                                             CancellationToken: CancellationToken).ConfigureAwait(false);

            logger.Log(LogLevel.Debug, $"DANE: '{owner}' has no TLSA records, denial {denial}");

            return denial is DNSSECValidationResult.Bogus or DNSSECValidationResult.Indeterminate
                       ? new DaneResult(DaneStatus.Bogus, [], $"no TLSA records for {owner}, and no valid proof that there are none")
                       : DaneResult.None("no TLSA records");

        }

        // The records only count if the zone is DNSSEC-signed and validates.
        var dnssec = await dnssecValidator.ValidateAsync(response, CancellationToken: CancellationToken).ConfigureAwait(false);

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

    // RFC 7672 §2.1.1: an answer is NOERROR or NXDOMAIN; SERVFAIL, a timeout, anything else is a
    // lookup error.
    private static Boolean Failed(DNSInfo Response)
        => Response.IsTimeout ||
           Response.ResponseCode is not (DNSResponseCodes.NoError or DNSResponseCodes.NameError);

}
