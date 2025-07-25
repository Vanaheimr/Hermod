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
    /// Extensions methods for DNS SRV resource records.
    /// </summary>
    public static class DNS_SRV_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, SRVRecord)

        /// <summary>
        /// Add a DNS SRV record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="SRVRecord">A DNS SRV record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      SRV             SRVRecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                SRVRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Service (SRV) resource record.
    /// https://www.rfc-editor.org/rfc/rfc2782
    /// </summary>
    public class SRV : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Service (SRV) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 33;

        #endregion

        #region Properties

        /// <summary>
        /// A 16-bit unsigned integer specifying the priority of this target host.
        /// Lower values indicate higher priority.
        /// </summary>
        public UInt16  Priority    { get; }

        /// <summary>
        /// A 16-bit unsigned integer specifying a relative weight for entries with the same priority.
        /// Higher weights should be given a proportionately higher probability of being selected.
        /// </summary>
        public UInt16  Weight      { get; }

        /// <summary>
        /// The port on this target host of this service.
        /// </summary>
        public UInt16  Port        { get; }

        /// <summary>
        /// The domain name of the target host.
        /// </summary>
        public String  Target      { get; }

        #endregion

        #region Constructors

        #region SRV(Stream)

        /// <summary>
        /// Create a new SRV resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the SRV resource record data.</param>
        public SRV(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Priority  = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Weight    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Port      = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Target    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region SRV(Name, Stream)

        /// <summary>
        /// Create a new SRV resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this SRV resource record.</param>
        /// <param name="Stream">A stream containing the SRV resource record data.</param>
        public SRV(String Name,
                   Stream Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {

            this.Priority  = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Weight    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Port      = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Target    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region SRV(Name, Class, TimeToLive, Priority, Weight, Port, Target)

        /// <summary>
        ///  Create a new DNS SRV record.
        /// </summary>
        /// <param name="Name">The DNS name of this SRV record.</param>
        /// <param name="Class">The DNS query class of this SRV record.</param>
        /// <param name="TimeToLive">The time to live of this SRV record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="Weight">The relative weight for entries with the same priority.</param>
        /// <param name="Port">The port on this target host of this service.</param>
        /// <param name="Target">The domain name of the target host.</param>
        public SRV(String           Name,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Priority,
                   UInt16           Weight,
                   UInt16           Port,
                   String           Target)

            : base(Name,
                   TypeId,
                   Class,
                   TimeToLive,
                   $"{Priority} {Weight} {Port} {Target}")

        {

            this.Priority  = Priority;
            this.Weight    = Weight;
            this.Port      = Port;
            this.Target    = Target;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Priority={Priority}, Weight={Weight}, Port={Port}, Target={Target}, {base.ToString()}";

        #endregion

    }

}
