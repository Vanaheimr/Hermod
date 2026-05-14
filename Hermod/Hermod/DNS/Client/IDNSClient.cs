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
using System.Net;

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

        #region Query<T>            (DomainName,             Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<DNSInfo<T>>

            Query<T>(this IDNSClient    IDNSClient,
                     DomainName         DomainName,
                     TimeSpan?          Timeout             = null,
                     Boolean?           RecursionDesired    = true,
                     Boolean?           BypassCache         = false,
                     CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var dnsInfo = await IDNSClient.Query(
                                    DomainName,
                                    [ GetResourceRecordType<T>() ],
                                    Timeout,
                                    RecursionDesired,
                                    BypassCache,
                                    CancellationToken
                                ).ConfigureAwait(false);

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion

        #region Query<T>            (DNSServiceName,         Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<DNSInfo<T>>

            Query<T>(this IDNSClient    IDNSClient,
                     DNSServiceName     DNSServiceName,
                     TimeSpan?          Timeout             = null,
                     Boolean?           RecursionDesired    = true,
                     Boolean?           BypassCache         = false,
                     CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var dnsInfo = await IDNSClient.Query(
                                    DNSServiceName,
                                    [ GetResourceRecordType<T>() ],
                                    Timeout,
                                    RecursionDesired,
                                    BypassCache,
                                    CancellationToken
                                ).ConfigureAwait(false);

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion


        #region Query<T1, T2>       (DomainName,     Mapper, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<T2>>

            Query<TRR, T2>(this IDNSClient    IDNSClient,
                           DomainName         DomainName,
                           Func<TRR, T2>      Mapper,
                           TimeSpan?          Timeout             = null,
                           Boolean?           RecursionDesired    = true,
                           Boolean?           BypassCache         = false,
                           CancellationToken  CancellationToken   = default)

            where TRR : ADNSResourceRecord

            => (await IDNSClient.Query<TRR>(
                          DomainName,
                          Timeout,
                          RecursionDesired,
                          BypassCache,
                          CancellationToken
                      )).FilteredAnswers.Select(v => Mapper(v));

        #endregion

        #region Query<T1, T2>       (DNSServiceName, Mapper, Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<T2>>

            Query<TRR, T2>(this IDNSClient    IDNSClient,
                           DNSServiceName     DNSServiceName,
                           Func<TRR, T2>      Mapper,
                           TimeSpan?          Timeout             = null,
                           Boolean?           RecursionDesired    = true,
                           Boolean?           BypassCache         = false,
                           CancellationToken  CancellationToken   = default)

            where TRR : ADNSResourceRecord

            => (await IDNSClient.Query<TRR>(
                          DNSServiceName,
                          Timeout,
                          RecursionDesired,
                          BypassCache,
                          CancellationToken
                      )).FilteredAnswers.Select(v => Mapper(v));

        #endregion


        #region Query_IPv4Addresses (DomainName,             Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                DomainName         DomainName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<A>(
                              DNSServiceName.Parse(DomainName.FullName),
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(ARecord => ARecord.IPv4Address);

        #endregion

        #region Query_IPv4Addresses (DNSServiceName,         Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                DNSServiceName     DNSServiceName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<A>(
                              DNSServiceName,
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(ARecord => ARecord.IPv4Address);

        #endregion

        #region Query_IPv4Addresses (RemoteURL,              Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                URL                RemoteURL,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<A>(
                              DomainName.Parse(RemoteURL.Hostname.Name),
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(ARecord => ARecord.IPv4Address);

        #endregion


        #region Query_IPv6Addresses (DomainName,             Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                DomainName         DomainName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<AAAA>(
                              DNSServiceName.Parse(DomainName.FullName),
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address);

        #endregion

        #region Query_IPv6Addresses (DNSServiceName,         Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                DNSServiceName     DNSServiceName,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<AAAA>(
                              DNSServiceName,
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address);

        #endregion

        #region Query_IPv6Addresses (RemoteURL,              Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                URL                RemoteURL,
                                TimeSpan?          Timeout             = null,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<AAAA>(
                              DomainName.Parse(RemoteURL.Hostname.Name),
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address);

        #endregion


        #region Query_IPAddresses   (DomainName,             Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              DomainName         DomainName,
                              TimeSpan?          Timeout             = null,
                              Boolean?           RecursionDesired    = true,
                              Boolean?           BypassCache         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DomainName, Timeout, RecursionDesired, BypassCache, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DomainName, Timeout, RecursionDesired, BypassCache, CancellationToken);

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

        #region Query_IPAddresses   (DNSServiceName,         Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              DNSServiceName     DNSServiceName,
                              TimeSpan?          Timeout             = null,
                              Boolean?           RecursionDesired    = true,
                              Boolean?           BypassCache         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DNSServiceName, Timeout, RecursionDesired, BypassCache, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DNSServiceName, Timeout, RecursionDesired, BypassCache, CancellationToken);

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

        #region Query_IPAddresses   (RemoteURL,              Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              URL                RemoteURL,
                              TimeSpan?          Timeout             = null,
                              Boolean?           RecursionDesired    = true,
                              Boolean?           BypassCache         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DomainName.Parse(RemoteURL.Hostname.Name), Timeout, RecursionDesired, BypassCache, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DomainName.Parse(RemoteURL.Hostname.Name), Timeout, RecursionDesired, BypassCache, CancellationToken);

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


        #region Query_DNSService    (DomainName,             Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<SRV>>

            Query_DNSService(this IDNSClient    IDNSClient,
                             DomainName         DomainName,
                             SRV_Spec           DNSServiceSpec,
                             TimeSpan?          Timeout             = null,
                             Boolean?           RecursionDesired    = true,
                             Boolean?           BypassCache         = false,
                             CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<SRV>(
                              DNSServiceName.From(
                                  DomainName,
                                  DNSServiceSpec
                              ),
                              Timeout,
                              RecursionDesired,
                              BypassCache,
                              CancellationToken
                          ).ConfigureAwait(false)).FilteredAnswers;

        #endregion

        #region Query_DNSService    (DNSServiceName,          Timeout = null, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<SRV>>

            Query_DNSService(this IDNSClient    IDNSClient,
                             DNSServiceName     DNSServiceName,
                             TimeSpan?          Timeout             = null,
                             Boolean?           RecursionDesired    = true,
                             Boolean?           BypassCache         = false,
                             CancellationToken  CancellationToken   = default)

                => (await IDNSClient.Query<SRV>(
                              DNSServiceName,
                              Timeout,
                              RecursionDesired,
                              BypassCache,
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

        #region Query (DomainName,     ResourceRecordTypes, Timeout = null, BypassCache = false, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default);

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, Timeout = null,BypassCache = false, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   TimeSpan?                            Timeout             = null,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String ToString();

        #endregion

    }

    /// <summary>
    /// The interface for all DNS clients.
    /// </summary>
    public interface IDNSClient2 : IDNSClient
    {

        /// <summary>
        /// The local IP end point.
        /// </summary>
        IPEndPoint?  CurrentLocalEndPoint      { get; }

        /// <summary>
        /// The local port.
        /// </summary>
        UInt16?      CurrentLocalPort          { get; }

        /// <summary>
        /// The local IP address.
        /// </summary>
        IIPAddress?  CurrentLocalIPAddress     { get; }


        /// <summary>
        /// The remote IP end point.
        /// </summary>
        IPEndPoint?  CurrentRemoteEndPoint     { get; }

        /// <summary>
        /// The remote port.
        /// </summary>
        UInt16?      CurrentRemotePort         { get; }

        /// <summary>
        /// The remote IP address.
        /// </summary>
        IIPAddress?  CurrentRemoteIPAddress    { get; }


        URL          RemoteURL                 { get; }
        IIPAddress?  RemoteIPAddress           { get; }
        IPPort?      RemotePort                { get; }


        TimeSpan     ConnectTimeout            { get; }
        TimeSpan     ReceiveTimeout            { get; }
        TimeSpan     SendTimeout               { get; }
        UInt32       BufferSize                { get; }

    }

}
