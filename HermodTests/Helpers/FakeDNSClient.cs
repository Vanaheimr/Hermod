/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

namespace org.GraphDefined.Vanaheimr.Hermod.Tests
{

    /// <summary>
    /// An IDNSClient that never talks to the network, answers with a canned set of
    /// addresses and records every name it was asked to resolve.
    ///
    /// This makes it observable whether a lookup happened at all, which is what the
    /// "an URL already carrying an IP address must not be resolved" tests are about.
    /// </summary>
    public class FakeDNSClient : IDNSClient
    {

        #region Data

        private static readonly DNSServerConfig  origin  = new (IPv4Address.Localhost,
                                                                IPPort.Parse(53));

        private readonly List<DNSServiceName>  queriedNames  = [];

        #endregion

        #region Properties

        /// <summary>
        /// Every name this client was asked to resolve.
        ///
        /// Note: A DNSServiceName, because that is where every lookup ends up. It is the
        ///       permissive form allowing underscore labels such as "_dmarc" or "_25._tcp",
        ///       which a hostname never has, and thus a superset of a DomainName.
        /// </summary>
        public IReadOnlyList<DNSServiceName>  QueriedNames
            => queriedNames;

        /// <summary>
        /// The total number of lookups performed.
        /// </summary>
        public Int32                          QueryCount
            => queriedNames.Count;

        /// <summary>
        /// The IPv4 addresses to answer every query with.
        /// </summary>
        public List<IPv4Address>              IPv4Addresses    { get; } = [];

        /// <summary>
        /// The IPv6 addresses to answer every query with.
        /// </summary>
        public List<IPv6Address>              IPv6Addresses    { get; } = [];

        #endregion

        #region Query (DomainName,     ResourceRecordTypes, ...)

        /// <summary>
        /// Note: Widens to a DNSServiceName and delegates, exactly like the real DNSClient,
        ///       so that a lookup is recorded once regardless of the entry point.
        /// </summary>
        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)

            => Query(
                   DNSServiceName.Parse(DomainName.FullName),
                   ResourceRecordTypes,
                   Timeout,
                   RecursionDesired,
                   ForceUpdate,
                   CancellationToken
               );

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default)
        {

            queriedNames.Add(DNSServiceName);

            return Task.FromResult(Answer(DNSServiceName));

        }

        #endregion

        #region (private) Answer(DNSServiceName)

        private DNSInfo Answer(DNSServiceName DNSServiceName)
        {

            var answers = new List<IDNSResourceRecord>();

            foreach (var ipv4Address in IPv4Addresses)
                answers.Add(new A   (DNSServiceName, DNSQueryClasses.IN, TimeSpan.FromMinutes(1), ipv4Address));

            foreach (var ipv6Address in IPv6Addresses)
                answers.Add(new AAAA(DNSServiceName, DNSQueryClasses.IN, TimeSpan.FromMinutes(1), ipv6Address));

            return new DNSInfo(
                       origin,
                       QueryId:                0,
                       IsAuthoritativeAnswer:  true,
                       IsTruncated:            false,
                       RecursionDesired:       true,
                       RecursionAvailable:     true,
                       ResponseCode:           DNSResponseCodes.NoError,
                       Answers:                answers,
                       Authorities:            [],
                       AdditionalRecords:      [],
                       IsValid:                true,
                       IsTimeout:              false,
                       Timeout:                TimeSpan.FromSeconds(5),
                       Runtime:                TimeSpan.Zero
                   );

        }

        #endregion


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        public override String ToString()
            => nameof(FakeDNSClient);

    }

}
