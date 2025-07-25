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
    /// Extensions methods for DNS MX resource records.
    /// </summary>
    public static class DNS_MX_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, MXRecord)

        /// <summary>
        /// Add a DNS MX record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="MXRecord">A DNS MX record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      MX              MXRecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                MXRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Mail Exchange (MX) resource record.
    /// </summary>
    public class MX : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Mail Exchange (MX) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 15;

        #endregion

        #region Properties

        /// <summary>
        /// The preference of this mail exchange.
        /// </summary>
        public Int32   Preference    { get; }

        /// <summary>
        /// The domain name of the mail exchange server.
        /// </summary>
        public String  Exchange      { get; }

        #endregion

        #region Constructor

        #region MX(Stream)

        /// <summary>
        /// Create a new MX resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the MX resource record data.</param>
        public MX(Stream  Stream)

            : base(Stream,
                   TypeId)

        {

            this.Preference  = (Stream.ReadByte() << 8) | (Stream.ReadByte() & Byte.MaxValue);
            this.Exchange    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region MX(Name, Stream)

        /// <summary>
        /// Create a new MX resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this MX resource record.</param>
        /// <param name="Stream">A stream containing the MX resource record data.</param>
        public MX(String  Name,
                  Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {

            this.Preference  = (Stream.ReadByte() << 8) | (Stream.ReadByte() & Byte.MaxValue);
            this.Exchange    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region MX(Name, Class, TimeToLive, Preference, Exchange)

        /// <summary>
        /// Create a new DNS A resource record.
        /// </summary>
        /// <param name="Name">The DNS name of this A resource record.</param>
        /// <param name="Class">The DNS query class of this resource record.</param>
        /// <param name="TimeToLive">The time to live of this resource record.</param>
        /// <param name="Preference">The preference of this mail exchange.</param>
        /// <param name="Exchange">The domain name of the mail exchange server.</param>
        public MX(String           Name,
                  DNSQueryClasses  Class,
                  TimeSpan         TimeToLive,
                  Int32            Preference,
                  String           Exchange)

            : base(Name,
                   TypeId,
                   Class,
                   TimeToLive)

        {

            this.Preference  = Preference;
            this.Exchange    = Exchange;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Preference: {Preference}, Exchange: {Exchange}, {base.ToString()}";

        #endregion

    }

}
