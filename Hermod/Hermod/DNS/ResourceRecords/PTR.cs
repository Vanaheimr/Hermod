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

#region Usings

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// Extensions methods for DNS PTR resource records.
    /// </summary>
    public static class DNS_PTR_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, PTRRecord)

        /// <summary>
        /// Add a DNS PTR record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="PTRRecord">A DNS PTR record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      PTR             PTRRecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                PTRRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Pointer (PTR) resource record type.
    /// </summary>
    public class PTR : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Pointer (PTR) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 12;

        #endregion

        #region Properties

        /// <summary>
        /// The text of this DNS Pointer (PTR) resource record.
        /// </summary>
        public String  Text    { get; }

        #endregion

        #region Constructor

        #region PTR(Stream)

        /// <summary>
        /// Create a new PTR resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the PTR resource record data.</param>
        public PTR(Stream  Stream)

            : base(Stream,
                   TypeId)

        {
            this.Text = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region PTR(Name, Stream)

        /// <summary>
        /// Create a new PTR resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this PTR resource record.</param>
        /// <param name="Stream">A stream containing the PTR resource record data.</param>
        public PTR(String  Name,
                   Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {
            this.Text = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region PTR(Name, Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="Name">The DNS name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RText">The text of this DNS Pointer (PTR) resource record.</param>
        public PTR(String           Name,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   String           RText)

            : base(Name,
                   TypeId,
                   Class,
                   TimeToLive,
                   RText)

        {
            this.Text = RText;
        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"{Text}, {base.ToString()}";

        #endregion

    }

}
