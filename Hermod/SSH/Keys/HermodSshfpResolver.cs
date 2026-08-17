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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SSH
{

    /// <summary>
    /// The <see cref="ISshfpResolver"/> bound to Hermod's DNS client: looks up the SSHFP (RFC 4255)
    /// records of a host and maps them onto <see cref="SshfpRecord"/>, so a host key can be verified
    /// against DNS (the OpenSSH <c>VerifyHostKeyDNS</c> equivalent, via <see cref="SshfpVerifier"/>).
    ///
    /// <para>
    /// <b>On the DNSSEC flag:</b> plain DNS is unauthenticated, so a record it returns is only ever
    /// advisory — an attacker who can answer the lookup can also choose the fingerprint. The result is
    /// therefore reported as DNSSEC-validated <b>only</b> when a <see cref="DNSSECValidator"/> was
    /// supplied and it validated this very answer as <c>Secure</c>. Without one the flag stays false,
    /// which means <see cref="SshfpTrust.RequireDnssec"/> will never auto-accept — deliberately, since
    /// the alternative is trusting an unsigned answer.
    /// </para>
    /// </summary>
    public sealed class HermodSshfpResolver : ISshfpResolver
    {

        #region Data

        private readonly IDNSClient       dnsClient;
        private readonly DNSSECValidator?  dnssecValidator;
        private readonly TimeSpan?         queryTimeout;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create an SSHFP resolver over a Hermod DNS client.
        /// </summary>
        /// <param name="DNSClient">The DNS client to query with.</param>
        /// <param name="DNSSECValidator">
        /// An optional DNSSEC validator. Supply one to make <see cref="SshfpTrust.RequireDnssec"/>
        /// usable; without it every answer is reported unvalidated.
        /// </param>
        /// <param name="QueryTimeout">An optional per-query timeout.</param>
        public HermodSshfpResolver(IDNSClient        DNSClient,
                                   DNSSECValidator?  DNSSECValidator   = null,
                                   TimeSpan?         QueryTimeout      = null)
        {
            this.dnsClient        = DNSClient;
            this.dnssecValidator  = DNSSECValidator;
            this.queryTimeout     = QueryTimeout;
        }

        #endregion


        #region QueryAsync(Host, CancellationToken = default)

        /// <summary>
        /// Look up the SSHFP records for a host.
        /// </summary>
        /// <param name="Host">The hostname to look up.</param>
        /// <param name="CancellationToken">An optional token to cancel this request.</param>
        public async ValueTask<SshfpLookupResult> QueryAsync(String             Host,
                                                             CancellationToken  CancellationToken   = default)
        {

            if (!DomainName.TryParse(Host, out var domainName, out _) || domainName is null)
                return SshfpLookupResult.Empty;

            DNSInfo response;

            try
            {
                response = await dnsClient.Query(
                                     domainName,
                                     [ DNSResourceRecordTypes.SSHFP ],
                                     queryTimeout,
                                     CancellationToken: CancellationToken
                                 ).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A failed lookup is indistinguishable from "no records" for our purposes: either way
                // there is nothing to verify against, and SSHFP must never be able to break a login.
                return SshfpLookupResult.Empty;
            }

            var records = new List<SshfpRecord>();

            foreach (var sshfp in response.Answers.OfType<SSHFP>())
                if (TryMap(sshfp, out var record))
                    records.Add(record!);

            if (records.Count == 0)
                return SshfpLookupResult.Empty;

            return new SshfpLookupResult(records, await IsDnssecSecureAsync(response, CancellationToken).ConfigureAwait(false));

        }

        #endregion


        #region (private) TryMap(SSHFP, out SshfpRecord)

        // Both sides number the algorithms and hash types straight from RFC 4255 and its updates, so the
        // mapping is the byte value itself — but an unassigned number must be dropped rather than
        // conjured into an enum value we would then compare against.
        private static Boolean TryMap(SSHFP SSHFP, out SshfpRecord? Record)
        {

            Record = null;

            var algorithm       = (SshfpAlgorithm)       (Byte) SSHFP.FingerprintAlgorithm;
            var fingerprintType = (SshfpFingerprintType) (Byte) SSHFP.FingerprintType;

            if (!Enum.IsDefined(algorithm)       || algorithm       == SshfpAlgorithm.Reserved ||
                !Enum.IsDefined(fingerprintType) || fingerprintType == SshfpFingerprintType.Reserved)
                return false;

            if (SSHFP.Fingerprint is null || SSHFP.Fingerprint.Length == 0)
                return false;

            Record = new SshfpRecord(algorithm, fingerprintType, SSHFP.Fingerprint);
            return true;

        }

        #endregion

        #region (private) IsDnssecSecureAsync(Response, CancellationToken)

        private async ValueTask<Boolean> IsDnssecSecureAsync(DNSInfo Response, CancellationToken CancellationToken)
        {

            if (dnssecValidator is null)
                return false;

            try
            {
                return await dnssecValidator.ValidateAsync(Response, CancellationToken).ConfigureAwait(false)
                           == DNSSECValidationResult.Secure;
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;   // could not validate ⇒ not validated
            }

        }

        #endregion

    }

}
