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

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// A DNS client routing link-local names (".local", the link-local reverse zones,
    /// RFC 6762 §3 and §12) to a Multicast DNS client and every other name to a
    /// conventional (unicast) DNS client. Hand it to HTTP, WebSocket or TCP clients
    /// that must reach "hostname.local" URLs as well as Internet hosts.
    /// </summary>
    public sealed class HybridDNSClient : IDNSClientWithDNSSEC
    {

        #region Data

        private readonly Func<IDomainName, Boolean>  isMulticastName;
        private          Boolean                     isDisposed;

        #endregion

        #region Properties

        /// <summary>
        /// The Multicast DNS client for link-local names.
        /// </summary>
        public MulticastDNSClient  MulticastClient    { get; }

        /// <summary>
        /// The unicast DNS client for every other name.
        /// </summary>
        public IDNSClient          UnicastClient      { get; }

        /// <summary>
        /// Whether disposing this client also disposes both inner clients.
        /// </summary>
        public Boolean             OwnsClients        { get; }

        /// <summary>
        /// Whether the unicast client requests DNSSEC records via the EDNS0 DNSSEC-OK bit.
        /// </summary>
        public Boolean             DnssecOK
        {

            get
                => UnicastClient is IDNSClientWithDNSSEC dnssecClient &&
                   dnssecClient.DnssecOK;

            set
            {

                if (UnicastClient is not IDNSClientWithDNSSEC dnssecClient)
                    throw new NotSupportedException($"The unicast DNS client '{UnicastClient.GetType().Name}' does not support DNSSEC query configuration!");

                dnssecClient.DnssecOK = value;

            }

        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new hybrid DNS client.
        /// </summary>
        /// <param name="MulticastClient">The Multicast DNS client for link-local names.</param>
        /// <param name="UnicastClient">The unicast DNS client for every other name (default: a new DNSClient using the operating system's resolvers).</param>
        /// <param name="OwnsClients">Whether disposing this client also disposes both inner clients (default: false).</param>
        /// <param name="IsMulticastName">An optional predicate deciding which names are resolved via Multicast DNS (default: MulticastDNS.IsLocalName).</param>
        public HybridDNSClient(MulticastDNSClient           MulticastClient,
                               IDNSClient?                  UnicastClient     = null,
                               Boolean                      OwnsClients       = false,
                               Func<IDomainName, Boolean>?  IsMulticastName   = null)
        {

            ArgumentNullException.ThrowIfNull(MulticastClient);

            this.MulticastClient  = MulticastClient;
            this.UnicastClient    = UnicastClient   ?? new DNSClient();
            this.OwnsClients      = OwnsClients;
            this.isMulticastName  = IsMulticastName ?? MulticastDNS.IsLocalName;

        }

        #endregion


        #region Query(DomainName,     ResourceRecordTypes, Timeout = null, RecursionDesired = true, ForceUpdate = false, ...)

        /// <summary>
        /// Resolve the given domain name via Multicast DNS (link-local names) or unicast DNS.
        /// </summary>
        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => isMulticastName(DomainName)
                   ? MulticastClient.Query(DomainName, ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken)
                   : UnicastClient.  Query(DomainName, ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

        #endregion

        #region Query(DNSServiceName, ResourceRecordTypes, Timeout = null, RecursionDesired = true, ForceUpdate = false, ...)

        /// <summary>
        /// Resolve the given DNS service name via Multicast DNS (link-local names) or unicast DNS.
        /// </summary>
        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => isMulticastName(DNSServiceName)
                   ? MulticastClient.Query(DNSServiceName, ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken)
                   : UnicastClient.  Query(DNSServiceName, ResourceRecordTypes, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

        #endregion


        #region Dispose() / DisposeAsync()

        /// <summary>
        /// Dispose the inner clients when this client owns them.
        /// </summary>
        public void Dispose()
        {

            if (isDisposed)
                return;

            isDisposed = true;

            if (OwnsClients)
            {
                MulticastClient.Dispose();
                UnicastClient.  Dispose();
            }

        }

        /// <summary>
        /// Dispose the inner clients when this client owns them.
        /// </summary>
        public async ValueTask DisposeAsync()
        {

            if (isDisposed)
                return;

            isDisposed = true;

            if (OwnsClients)
            {
                await MulticastClient.DisposeAsync().ConfigureAwait(false);
                await UnicastClient.  DisposeAsync().ConfigureAwait(false);
            }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this client.
        /// </summary>
        public override String ToString()

            => $"hybrid DNS client: .local via {MulticastClient}, others via {UnicastClient}";

        #endregion

    }

}
