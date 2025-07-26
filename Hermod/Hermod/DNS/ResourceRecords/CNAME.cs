/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
    /// Extensions methods for DNS CNAME resource records.
    /// </summary>
    public static class DNS_CNAME_Extensions
    {

        #region CacheCNAME(this DNSClient, DomainName, CNAMERecord)

        /// <summary>
        /// Add a DNS CNAME record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="CName">The target of this CNAME resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        public static void CacheCNAME(this DNSClient   DNSClient,
                                      DomainName       DomainName,
                                      DomainName       CName,
                                      DNSQueryClasses  Class        = DNSQueryClasses.IN,
                                      TimeSpan?        TimeToLive   = null)
        {

            var dnsRecord = new CNAME(
                                DomainName,
                                Class,
                                TimeToLive ?? TimeSpan.FromDays(365),
                                CName
                            );

            DNSClient.DNSCache.Add(
                dnsRecord.DomainName,
                dnsRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Canonical Name (CNAME) resource record.
    /// </summary>
    public class CNAME : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Canonical Name (CNAME) resource record type identifier.
        /// </summary>
        public const DNSResourceRecords TypeId = DNSResourceRecords.CNAME;

        #endregion

        #region Properties

        /// <summary>
        /// The DNS Canonical Name (CNAME).
        /// </summary>
        public DomainName  CName    { get; }

        #endregion

        #region Constructor

        #region CNAME(Stream)

        /// <summary>
        /// Create a new CNAME resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the CNAME resource record data.</param>
        public CNAME(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            this.CName = DNS.DomainName.Parse(
                             DNSTools.ExtractName(Stream)
                         );

        }

        #endregion

        #region CNAME(DomainName, Stream)

        /// <summary>
        /// Create a new CNAME resource record from the given name and stream.
        /// </summary>
        /// <param name="DomainName">The domain name of this CNAME resource record.</param>
        /// <param name="Stream">A stream containing the CNAME resource record data.</param>
        public CNAME(DomainName  DomainName,
                     Stream      Stream)

            : base(DomainName,
                   TypeId,
                   Stream)

        {

            this.CName = DomainName.Parse(
                             DNSTools.ExtractName(Stream)
                         );

        }

        #endregion

        #region CNAME(DomainName, Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS CNAME resource record.
        /// </summary>
        /// <param name="DomainName">The domain name of this CNAME resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="CName">The target of this CNAME resource record.</param>
        public CNAME(DomainName       DomainName,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     DomainName       CName)

            : base(DomainName,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.CName = CName;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{CName}, {base.ToString()}";

        #endregion

    }

}
