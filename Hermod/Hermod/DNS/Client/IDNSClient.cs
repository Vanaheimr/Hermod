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

        #region Query<T>            (DomainName,             RecursionDesired = true, BypassCache = false, ...)

        public static async Task<DNSInfo<T>>

            Query<T>(this IDNSClient    IDNSClient,
                     DomainName         DomainName,
                     Boolean?           RecursionDesired    = true,
                     Boolean?           BypassCache         = false,
                     CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var typeIdField  = typeof(T).GetField("TypeId")
                                   ?? throw new ArgumentException($"Constant field 'TypeId' of type '{typeof(T).Name}' was not found!");

            var typeIdValue  = typeIdField.GetValue(typeof(T));

            if (typeIdValue is not DNSResourceRecordTypes dnsResourceRecordType)
                throw new ArgumentException($"Constant field 'TypeId' of type '{typeof(T).Name}' was not of type '{typeof(DNSResourceRecordTypes).Name}'!");

            var dnsInfo      = await IDNSClient.Query(
                                         DomainName,
                                         [ dnsResourceRecordType ],
                                         RecursionDesired,
                                         BypassCache,
                                         CancellationToken
                                     ).ConfigureAwait(false);

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion

        #region Query<T>            (DNSServiceName,         RecursionDesired = true, BypassCache = false, ...)

        public static async Task<DNSInfo<T>>

            Query<T>(this IDNSClient    IDNSClient,
                     DNSServiceName     DNSServiceName,
                     Boolean?           RecursionDesired    = true,
                     Boolean?           BypassCache         = false,
                     CancellationToken  CancellationToken   = default)

            where T : ADNSResourceRecord

        {

            var typeIdField  = typeof(T).GetField("TypeId")
                                   ?? throw new ArgumentException($"Constant field 'TypeId' of type '{typeof(T).Name}' was not found!");

            var typeIdValue  = typeIdField.GetValue(typeof(T));

            if (typeIdValue is not DNSResourceRecordTypes dnsResourceRecordType)
                throw new ArgumentException($"Constant field 'TypeId' of type '{typeof(T).Name}' was not of type '{typeof(DNSResourceRecordTypes).Name}'!");

            var dnsInfo      = await IDNSClient.Query(
                                         DNSServiceName,
                                         [ dnsResourceRecordType ],
                                         RecursionDesired,
                                         BypassCache,
                                         CancellationToken
                                     ).ConfigureAwait(false);

            return new DNSInfo<T>(dnsInfo);

        }

        #endregion


        #region Query<T1, T2>       (DomainName,     Mapper, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<T2>>

            Query<TRR, T2>(this IDNSClient    IDNSClient,
                           DomainName         DomainName,
                           Func<TRR, T2>      Mapper,
                           Boolean?           RecursionDesired    = true,
                           Boolean?           BypassCache         = false,
                           CancellationToken  CancellationToken   = default)

            where TRR : ADNSResourceRecord

            => (await IDNSClient.Query<TRR>(
                          DomainName,
                          RecursionDesired,
                          BypassCache,
                          CancellationToken
                      )).FilteredAnswers.Select(v => Mapper(v));

        #endregion

        #region Query<T1, T2>       (DNSServiceName, Mapper, RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<T2>>

            Query<TRR, T2>(this IDNSClient    IDNSClient,
                           DNSServiceName     DNSServiceName,
                           Func<TRR, T2>      Mapper,
                           Boolean?           RecursionDesired    = true,
                           Boolean?           BypassCache         = false,
                           CancellationToken  CancellationToken   = default)

            where TRR : ADNSResourceRecord

            => (await IDNSClient.Query<TRR>(
                          DNSServiceName,
                          RecursionDesired,
                          BypassCache,
                          CancellationToken
                      )).FilteredAnswers.Select(v => Mapper(v));

        #endregion


        #region Query_IPv4Addresses (DomainName,             RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                DomainName         DomainName,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<A>(
                             DNSServiceName.Parse(DomainName.FullName),
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(ARecord => ARecord.IPv4Address)).
                           ConfigureAwait(false);

        #endregion

        #region Query_IPv4Addresses (DNSServiceName,         RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                DNSServiceName     DNSServiceName,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<A>(
                             DNSServiceName,
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(ARecord => ARecord.IPv4Address)).
                           ConfigureAwait(false);

        #endregion

        #region Query_IPv4Addresses (RemoteURL,              RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv4Address>>

            Query_IPv4Addresses(this IDNSClient    IDNSClient,
                                URL                RemoteURL,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<A>(
                             DomainName.Parse(RemoteURL.Hostname.Name),
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(AAAARecord => AAAARecord.IPv4Address)).
                           ConfigureAwait(false);

        #endregion


        #region Query_IPv6Addresses (DomainName,             RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                DomainName         DomainName,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<AAAA>(
                             DNSServiceName.Parse(DomainName.FullName),
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address)).
                           ConfigureAwait(false);

        #endregion

        #region Query_IPv6Addresses (DNSServiceName,         RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                DNSServiceName     DNSServiceName,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<AAAA>(
                             DNSServiceName,
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address)).
                           ConfigureAwait(false);

        #endregion

        #region Query_IPv6Addresses (RemoteURL,              RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IPv6Address>>

            Query_IPv6Addresses(this IDNSClient    IDNSClient,
                                URL                RemoteURL,
                                Boolean?           RecursionDesired    = true,
                                Boolean?           BypassCache         = false,
                                CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<AAAA>(
                             DomainName.Parse(RemoteURL.Hostname.Name),
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers.Select(AAAARecord => AAAARecord.IPv6Address)).
                           ConfigureAwait(false);

        #endregion


        #region Query_IPAddresses   (DomainName,             RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              DomainName         DomainName,
                              Boolean?           RecursionDesired    = true,
                              Boolean?           BypassCache         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DomainName, RecursionDesired, BypassCache, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DomainName, RecursionDesired, BypassCache, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            return       ipv4AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress));

        }

        #endregion

        #region Query_IPAddresses   (DNSServiceName,         RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              DNSServiceName     DNSServiceName,
                              Boolean?           RecursionDesired    = true,
                              Boolean?           BypassCache         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DNSServiceName, RecursionDesired, BypassCache, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DNSServiceName, RecursionDesired, BypassCache, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            return       ipv4AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress));

        }

        #endregion

        #region Query_IPAddresses   (RemoteURL,              RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<IIPAddress>>

            Query_IPAddresses(this IDNSClient    IDNSClient,
                              URL                RemoteURL,
                              Boolean?           RecursionDesired    = true,
                              Boolean?           BypassCache         = false,
                              CancellationToken  CancellationToken   = default)

        {

            var ipv4AddressLookupTask = IDNSClient.Query_IPv4Addresses(DomainName.Parse(RemoteURL.Hostname.Name), RecursionDesired, BypassCache, CancellationToken);
            var ipv6AddressLookupTask = IDNSClient.Query_IPv6Addresses(DomainName.Parse(RemoteURL.Hostname.Name), RecursionDesired, BypassCache, CancellationToken);

            await Task.WhenAll(
                      ipv4AddressLookupTask,
                      ipv6AddressLookupTask
                  ).ConfigureAwait(false);

            return       ipv4AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress).
                   Union(ipv6AddressLookupTask.Result.Distinct().Select(v => v as IIPAddress));

        }

        #endregion


        #region Query_DNSService    (DomainName,             RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<SRV>>

            Query_DNSService(this IDNSClient    IDNSClient,
                             DomainName         DomainName,
                             SRV_Spec           DNSServiceSpec,
                             Boolean?           RecursionDesired    = true,
                             Boolean?           BypassCache         = false,
                             CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<SRV>(
                             DNSServiceName.From(
                                 DomainName,
                                 DNSServiceSpec
                             ),
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers).
                           ConfigureAwait(false);

        #endregion

        #region Query_DNSService    (DNSServiceName,         RecursionDesired = true, BypassCache = false, ...)

        public static async Task<IEnumerable<SRV>>

            Query_DNSService(this IDNSClient    IDNSClient,
                             DNSServiceName     DNSServiceName,
                             Boolean?           RecursionDesired    = true,
                             Boolean?           BypassCache         = false,
                             CancellationToken  CancellationToken   = default)

                => await IDNSClient.Query<SRV>(
                             DNSServiceName,
                             RecursionDesired,
                             BypassCache,
                             CancellationToken
                         ).ContinueWith  (query => query.Result.FilteredAnswers).
                           ConfigureAwait(false);

        #endregion

    }


    /// <summary>
    /// The interface for all DNS clients.
    /// </summary>
    public interface IDNSClient : IDisposable,
                                  IAsyncDisposable
    {

        #region Query (DomainName,     ResourceRecordTypes, BypassCache = false, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DomainName                           DomainName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
                                   Boolean?                             RecursionDesired    = true,
                                   Boolean?                             BypassCache         = false,
                                   CancellationToken                    CancellationToken   = default);

        #endregion

        #region Query (DNSServiceName, ResourceRecordTypes, BypassCache = false, RecursionDesired = true, ...)

        public Task<DNSInfo> Query(DNSServiceName                       DNSServiceName,
                                   IEnumerable<DNSResourceRecordTypes>  ResourceRecordTypes,
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
