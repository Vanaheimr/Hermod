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

using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for all DNS clients.
    /// </summary>
    public static class IDNSTCPClientExtensions
    {

        #region Data

        /// <summary>
        /// The Resolution Delay of RFC 8305 §3: how long the addresses of one family, having arrived first, wait
        /// for those of the other before they are used: 50 ms, as the RFC recommends.
        /// </summary>
        public static readonly TimeSpan DefaultResolutionDelay = TimeSpan.FromMilliseconds(50);

        private static readonly ConcurrentDictionary<Type, DNSResourceRecordTypes> typeIdCache = new();

        private static DNSResourceRecordTypes GetResourceRecordType<T>() where T : ADNSResourceRecord

            => typeIdCache.GetOrAdd(typeof(T), type => {

                   var typeIdField = type.GetField("TypeId")
                                        ?? throw new ArgumentException($"Constant field 'TypeId' of type '{type.Name}' was not found!");

                   var typeIdValue = typeIdField.GetValue(null);

                   if (typeIdValue is not DNSResourceRecordTypes dnsResourceRecordType)
                       throw new ArgumentException($"Constant field 'TypeId' of type '{type.Name}' was not of type '{nameof(DNSResourceRecordTypes)}'!");

                   return dnsResourceRecordType;

               });

        #endregion

        #region Query<T>            (DomainName,             Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<DNSInfo<T>>

            Query<T>(this IDNSClient    IDNSClient,
                     DomainName         DomainName,
                     TimeSpan?          Timeout             = null,
                     Boolean?           RecursionDesired    = null,
                     Boolean?           ForceUpdate         = false,
                     CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var dnsInfo = await IDNSClient.Query(
                                    DomainName,
                                    [ GetResourceRecordType<T>() ],
                                    Timeout,
                                    RecursionDesired,
                                    ForceUpdate,
                                    CancellationToken
                                ).ConfigureAwait(false);

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion

        #region Query<T>            (DNSServiceName,         Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<DNSInfo<T>>

            Query<T>(this IDNSClient    IDNSClient,
                     DNSServiceName     DNSServiceName,
                     TimeSpan?          Timeout             = null,
                     Boolean?           RecursionDesired    = null,
                     Boolean?           ForceUpdate         = false,
                     CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var dnsInfo = await IDNSClient.Query(
                                    DNSServiceName,
                                    [ GetResourceRecordType<T>() ],
                                    Timeout,
                                    RecursionDesired,
                                    ForceUpdate,
                                    CancellationToken
                                ).ConfigureAwait(false);

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion


        #region Query<T1, T2>       (DomainName,     Mapper, Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<T2>>

            Query<TRR, T2>(this IDNSClient    IDNSClient,
                           DomainName         DomainName,
                           Func<TRR, T2>      Mapper,
                           TimeSpan?          Timeout             = null,
                           Boolean?           RecursionDesired    = null,
                           Boolean?           ForceUpdate         = false,
                           CancellationToken  CancellationToken   = default)

            where TRR : ADNSResourceRecord

            => (await IDNSClient.Query<TRR>(
                          DomainName,
                          Timeout,
                          RecursionDesired,
                          ForceUpdate,
                          CancellationToken
                      ).ConfigureAwait(false)).FilteredAnswers.Select(v => Mapper(v));

        #endregion

        #region Query<T1, T2>       (DNSServiceName, Mapper, Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<T2>>

            Query<TRR, T2>(this IDNSClient    IDNSClient,
                           DNSServiceName     DNSServiceName,
                           Func<TRR, T2>      Mapper,
                           TimeSpan?          Timeout             = null,
                           Boolean?           RecursionDesired    = null,
                           Boolean?           ForceUpdate         = false,
                           CancellationToken  CancellationToken   = default)

            where TRR : ADNSResourceRecord

            => (await IDNSClient.Query<TRR>(
                          DNSServiceName,
                          Timeout,
                          RecursionDesired,
                          ForceUpdate,
                          CancellationToken
                      ).ConfigureAwait(false)).FilteredAnswers.Select(v => Mapper(v));

        #endregion


        #region Query_IPv4Addresses (DomainName,             Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                DomainName         DomainName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = null,
                                Boolean?           ForceUpdate         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<A>(
                              DNSServiceName.Parse(DomainName.FullName),
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(ARecord => ARecord.IPv4Address);

        #endregion

        #region Query_IPv4Addresses (DNSServiceName,         Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                DNSServiceName     DNSServiceName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = null,
                                Boolean?           ForceUpdate         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<A>(
                              DNSServiceName,
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(ARecord => ARecord.IPv4Address);

        #endregion

        #region Query_IPv4Addresses (RemoteURL,              Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                URL                RemoteURL,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = null,
                                Boolean?           ForceUpdate         = false,
                                CancellationToken  CancellationToken   = default)

        {

            // When the URL already carries an IP address there is nothing to resolve.
            // Sending it to a DNS server would be a pointless query for a name like
            // "192.168.1.1", as that happens to pass the domain name syntax check.
            if (RemoteURL.Host.IsIPAddress)
                return RemoteURL.Host.IPAddress is IPv4Address ipv4Address
                           ? [ ipv4Address ]
                           : [];

            if (RemoteURL.Host.DomainName is null)
                return [];

            return (await IDNSClient.Query<A>(
                              RemoteURL.Host.DomainName,
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(ARecord => ARecord.IPv4Address);

        }

        #endregion


        #region Query_IPv6Addresses (DomainName,             Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                DomainName         DomainName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = null,
                                Boolean?           ForceUpdate         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<AAAA>(
                              DNSServiceName.Parse(DomainName.FullName),
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address);

        #endregion

        #region Query_IPv6Addresses (DNSServiceName,         Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                DNSServiceName     DNSServiceName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = null,
                                Boolean?           ForceUpdate         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<AAAA>(
                              DNSServiceName,
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address);

        #endregion

        #region Query_IPv6Addresses (RemoteURL,              Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                URL                RemoteURL,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = null,
                                Boolean?           ForceUpdate         = false,
                                CancellationToken  CancellationToken   = default)

        {

            // When the URL already carries an IP address there is nothing to resolve.
            if (RemoteURL.Host.IsIPAddress)
                return RemoteURL.Host.IPAddress is IPv6Address ipv6Address
                           ? [ ipv6Address ]
                           : [];

            if (RemoteURL.Host.DomainName is null)
                return [];

            return (await IDNSClient.Query<AAAA>(
                              RemoteURL.Host.DomainName,
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address);

        }

        #endregion


        #region Query_IPAddresses   (DomainName,             Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              DomainName         DomainName,
                              TimeSpan?          Timeout             = null,
                              Boolean?           RecursionDesired    = null,
                              Boolean?           ForceUpdate         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DomainName, Timeout, RecursionDesired, ForceUpdate, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DomainName, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            var ipv4Addresses = await ipv4AddressLookupTask.ConfigureAwait(false);
            var ipv6Addresses = await ipv6AddressLookupTask.ConfigureAwait(false);

            return       ipv4Addresses.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6Addresses.Distinct().Select(v => v as IIPAddress));

        }

        #endregion

        #region Query_IPAddresses   (DNSServiceName,         Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              DNSServiceName     DNSServiceName,
                              TimeSpan?          Timeout             = null,
                              Boolean?           RecursionDesired    = null,
                              Boolean?           ForceUpdate         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DNSServiceName, Timeout, RecursionDesired, ForceUpdate, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DNSServiceName, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            var ipv4Addresses = await ipv4AddressLookupTask.ConfigureAwait(false);
            var ipv6Addresses = await ipv6AddressLookupTask.ConfigureAwait(false);

            return       ipv4Addresses.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6Addresses.Distinct().Select(v => v as IIPAddress));

        }

        #endregion

        #region Query_IPAddresses   (RemoteURL,              Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              URL                RemoteURL,
                              TimeSpan?          Timeout             = null,
                              Boolean?           RecursionDesired    = null,
                              Boolean?           ForceUpdate         = false,
                              CancellationToken  CancellationToken   = default)

        {

            // When the URL already carries an IP address there is nothing to resolve.
            if (RemoteURL.Host.IPAddress is IIPAddress ipAddress)
                return [ ipAddress ];

            if (RemoteURL.Host.DomainName is null)
                return [];

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(RemoteURL.Host.DomainName, Timeout, RecursionDesired, ForceUpdate, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(RemoteURL.Host.DomainName, Timeout, RecursionDesired, ForceUpdate, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            var ipv4Addresses = await ipv4AddressLookupTask.ConfigureAwait(false);
            var ipv6Addresses = await ipv6AddressLookupTask.ConfigureAwait(false);

            return       ipv4Addresses.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6Addresses.Distinct().Select(v => v as IIPAddress));

        }

        #endregion


        #region Query_IPAddressesToConnect (DomainName,      ResolutionDelay = null, Timeout = null, ...)

        /// <summary>
        /// The addresses to connect to the given name, IPv6 first, with the Resolution Delay of RFC 8305 §3
        /// ("Happy Eyeballs Version 2"): asked for AAAA and A at once, and not waiting for a family that does not
        /// answer once the other has.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Query_IPAddresses waits for both answers, so a name server that never answers for one family - an AAAA
        /// query dropped on the way, a resolver that only knows the A record - keeps the caller for the whole
        /// timeout, although the addresses of the other family are long known. Measured in Norn: a key exchange
        /// with a budget of 10 s waited for an AAAA answer that never came, the A record of the name in the cache,
        /// and failed.
        /// </para>
        /// <para>
        /// As RFC 8305 §3 says: once the first positive answer of one family has arrived, the other is waited for
        /// the Resolution Delay, 50 ms by default, and no longer. A negative answer - no addresses, a name error, a
        /// timeout, a failure - is no reason to stop waiting: the other family is then waited for as long as it
        /// takes. The addresses are returned IPv6 first, then IPv4, each in the order they were given, for a caller
        /// that tries them in turn (RFC 8305 §4 in short: the families are not interleaved).
        /// </para>
        /// <para>
        /// Unlike the RFC, which starts connecting over IPv6 at once when the AAAA answer is first and adds the IPv4
        /// addresses as they arrive, this returns once: an AAAA answer that is first waits the Resolution Delay for
        /// the A answer as well, so that a caller has the IPv4 addresses to fall back on when IPv6 does not get
        /// through - the IPv6 addresses still come first. Addresses arriving after the delay are not added; the
        /// query that is still out is cancelled.
        /// </para>
        /// </remarks>
        /// <param name="IDNSClient">A DNS client.</param>
        /// <param name="DomainName">The name to connect to.</param>
        /// <param name="ResolutionDelay">How long the family that has not answered is waited for once the other has; 50 ms by default.</param>
        /// <param name="Timeout">The timeout of each of the two queries.</param>
        /// <param name="RecursionDesired">Whether recursion is desired.</param>
        /// <param name="ForceUpdate">Whether to ask the name servers rather than the cache.</param>
        /// <param name="CancellationToken">A cancellation token.</param>
        public static Task<IEnumerable<IIPAddress>>

            Query_IPAddressesToConnect(this IDNSClient    IDNSClient,
                                       DomainName         DomainName,
                                       TimeSpan?          ResolutionDelay     = null,
                                       TimeSpan?          Timeout             = null,
                                       Boolean?           RecursionDesired    = null,
                                       Boolean?           ForceUpdate         = false,
                                       CancellationToken  CancellationToken   = default)

                => Query_IPAddressesToConnect(IDNSClient,
                                              DNSServiceName.Parse(DomainName.FullName),
                                              ResolutionDelay,
                                              Timeout,
                                              RecursionDesired,
                                              ForceUpdate,
                                              CancellationToken);

        #endregion

        #region Query_IPAddressesToConnect (DNSServiceName,  ResolutionDelay = null, Timeout = null, ...)

        /// <summary>
        /// The addresses to connect to the given name, IPv6 first, with the Resolution Delay of RFC 8305 §3:
        /// see <see cref="Query_IPAddressesToConnect(IDNSClient, DomainName, TimeSpan?, TimeSpan?, Boolean?, Boolean?, CancellationToken)"/>.
        /// </summary>
        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddressesToConnect(this IDNSClient    IDNSClient,
                                       DNSServiceName     DNSServiceName,
                                       TimeSpan?          ResolutionDelay     = null,
                                       TimeSpan?          Timeout             = null,
                                       Boolean?           RecursionDesired    = null,
                                       Boolean?           ForceUpdate         = false,
                                       CancellationToken  CancellationToken   = default)

        {

            CancellationToken.ThrowIfCancellationRequested();

            // Not disposed: the query still out when the addresses are returned is cancelled by it, and may look
            // at its token after that.
            var queries        = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);

            // RFC 8305 §3: the AAAA query first, the A query immediately after it.
            var ipv6Lookup     = IDNSClient.Query_IPv6Addresses(DNSServiceName, Timeout, RecursionDesired, ForceUpdate, queries.Token);
            var ipv4Lookup     = IDNSClient.Query_IPv4Addresses(DNSServiceName, Timeout, RecursionDesired, ForceUpdate, queries.Token);

            try
            {

                var first      = await Task.WhenAny(ipv6Lookup, ipv4Lookup).ConfigureAwait(false);
                var other      = first == ipv6Lookup ? (Task) ipv4Lookup : ipv6Lookup;
                var positive   = first == ipv6Lookup
                                     ? AddressesOf(ipv6Lookup).Any()
                                     : AddressesOf(ipv4Lookup).Any();

                // A positive answer first: the other family is waited for the Resolution Delay, and no longer.
                // A negative one, or none: the other is all there may be, and is waited for as long as it takes.
                if (positive)
                    await Task.WhenAny(other, Task.Delay(ResolutionDelay ?? DefaultResolutionDelay, CancellationToken)).ConfigureAwait(false);

                else
                    await Task.WhenAny(other).ConfigureAwait(false);

                CancellationToken.ThrowIfCancellationRequested();

                return [ .. AddressesOf(ipv6Lookup).Distinct(),
                         .. AddressesOf(ipv4Lookup).Distinct() ];

            }
            finally
            {

                // The query still out is not waited for, and its end is not left unobserved.
                queries.Cancel();

                foreach (var lookup in new Task[] { ipv6Lookup, ipv4Lookup })
                    if (!lookup.IsCompleted)
                        _ = lookup.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);

            }

        }

        #endregion

        #region Query_IPAddressesToConnect (RemoteURL,       ResolutionDelay = null, Timeout = null, ...)

        /// <summary>
        /// The addresses to connect to the host of the given URL, IPv6 first, with the Resolution Delay of
        /// RFC 8305 §3: see <see cref="Query_IPAddressesToConnect(IDNSClient, DomainName, TimeSpan?, TimeSpan?, Boolean?, Boolean?, CancellationToken)"/>.
        /// A host that is an IP address is that address, without a query.
        /// </summary>
        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddressesToConnect(this IDNSClient    IDNSClient,
                                       URL                RemoteURL,
                                       TimeSpan?          ResolutionDelay     = null,
                                       TimeSpan?          Timeout             = null,
                                       Boolean?           RecursionDesired    = null,
                                       Boolean?           ForceUpdate         = false,
                                       CancellationToken  CancellationToken   = default)

        {

            if (RemoteURL.Host.IPAddress is IIPAddress ipAddress)
                return [ ipAddress ];

            if (RemoteURL.Host.DomainName is null)
                return [];

            return await IDNSClient.Query_IPAddressesToConnect(RemoteURL.Host.DomainName,
                                                               ResolutionDelay,
                                                               Timeout,
                                                               RecursionDesired,
                                                               ForceUpdate,
                                                               CancellationToken).ConfigureAwait(false);

        }

        #endregion

        #region (private static) AddressesOf<T>(Lookup)

        /// <summary>
        /// The addresses the given lookup has ended with; none while it is running, or when it failed or was
        /// cancelled.
        /// </summary>
        private static IEnumerable<IIPAddress> AddressesOf<T>(Task<IEnumerable<T>> Lookup)

            where T : IIPAddress

            => Lookup.IsCompletedSuccessfully
                   ? [ .. Lookup.Result.Cast<IIPAddress>() ]
                   : [];

        #endregion


        #region Query_DNSService    (DomainName,             Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<SRV>>

            Query_DNSService(this IDNSClient    IDNSClient,
                             DomainName         DomainName,
                             SRV_Spec           DNSServiceSpec,
                             TimeSpan?          Timeout             = null,
                             Boolean?           RecursionDesired    = null,
                             Boolean?           ForceUpdate         = false,
                             CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<SRV>(
                              DNSServiceName.From(
                                  DomainName,
                                  DNSServiceSpec
                              ),
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers;

        #endregion

        #region Query_DNSService    (DNSServiceName,          Timeout = null, RecursionDesired = null, ForceUpdate = false, ...)

        public static async Task<IEnumerable<SRV>>

            Query_DNSService(this IDNSClient    IDNSClient,
                             DNSServiceName     DNSServiceName,
                             TimeSpan?          Timeout             = null,
                             Boolean?           RecursionDesired    = null,
                             Boolean?           ForceUpdate         = false,
                             CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<SRV>(
                              DNSServiceName,
                              Timeout,
                              RecursionDesired,
                              ForceUpdate,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers;

        #endregion

    }


    /// <summary>
    /// The interface for all DNS clients.
    /// </summary>
    public interface IDNSClient : IDisposable,
                                  IAsyncDisposable
    {

        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, ForceUpdate = false, RecursionDesired = null, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = null,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default);

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null,ForceUpdate = false, RecursionDesired = null, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = null,
                                   Boolean?                             ForceUpdate         = false,
                                   CancellationToken                    CancellationToken   = default);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String ToString();

        #endregion

    }

}
