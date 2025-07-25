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
    /// Extensions methods for DNS TXT resource records.
    /// </summary>
    public static class DNS_TXT_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, TXTRecord)

        /// <summary>
        /// Add a DNS TXT record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="TXTRecord">A DNS TXT record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      TXT             TXTRecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                TXTRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Text (TXT) resource record type identifier.
    /// </summary>
    public class TXT : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Text (TXT) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 16;

        #endregion

        #region Properties

        /// <summary>
        /// The text of this DNS Text (TXT) resource record.
        /// </summary>
        public String  Text    { get; }

        #endregion

        #region Constructor

        #region TXT(Stream)

        /// <summary>
        /// Create a new TXT resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the TXT resource record data.</param>
        public TXT(Stream Stream)

            : base(Stream,
                   TypeId)

        {
            this.Text = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region TXT(Name, Stream)

        /// <summary>
        /// Create a new TXT resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this TXT resource record.</param>
        /// <param name="Stream">A stream containing the TXT resource record data.</param>
        public TXT(String  Name,
                   Stream  Stream)

            : base(Name, TypeId, Stream)

        {
            this.Text = DNSTools.ExtractName(Stream);
        }

        #endregion

        #region TXT(Name, Class, TimeToLive, RText)

        /// <summary>
        /// Create a new DNS TXT resource record.
        /// </summary>
        /// <param name="Name">The DNS name of this TXT resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="RText">The text of this DNS TXT resource record.</param>
        public TXT(String           Name,
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
