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
    /// Extensions methods for DNS URI resource records.
    /// </summary>
    public static class DNS_URI_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, URIRecord)

        /// <summary>
        /// Add a DNS URI record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="URIRecord">A DNS URI record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      URI             URIRecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                URIRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Uniform Resource Identifier (URI) resource record.
    /// https://www.rfc-editor.org/rfc/rfc7553
    /// </summary>
    public class URI : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Uniform Resource Identifier (URI) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 256;

        #endregion

        #region Properties

        /// <summary>
        /// A 16-bit unsigned integer specifying the priority of this target URI.
        /// Lower values indicate higher priority.
        /// </summary>
        public UInt16  Priority    { get; }

        /// <summary>
        /// A 16-bit unsigned integer specifying a relative weight for entries with the same priority.
        /// Higher weights should be given a proportionately higher probability of being selected.
        /// </summary>
        public UInt16  Weight      { get; }

        /// <summary>
        /// The URI of the target, enclosed in double-quote characters in presentation format.
        /// </summary>
        public String  Target      { get; }

        #endregion

        #region Constructors

        #region URI(Stream)

        /// <summary>
        /// Create a new URI resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the URI resource record data.</param>
        public URI(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Priority  = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Weight    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Target    = DNSTools.ExtractNameUTF8(Stream);

        }

        #endregion

        #region URI(Name, Stream)

        /// <summary>
        /// Create a new URI resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this URI resource record.</param>
        /// <param name="Stream">A stream containing the URI resource record data.</param>
        public URI(String  Name,
                   Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {

            this.Priority  = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Weight    = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Target    = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region URI(Name, Class, TimeToLive, Priority, Weight, Port, Target)

        /// <summary>
        ///  Create a new DNS URI record.
        /// </summary>
        /// <param name="Name">The DNS name of this URI record.</param>
        /// <param name="Class">The DNS query class of this URI record.</param>
        /// <param name="TimeToLive">The time to live of this URI record.</param>
        /// <param name="Priority">The priority of this target host.</param>
        /// <param name="Weight">The relative weight for entries with the same priority.</param>
        /// <param name="Target">The domain name of the target host.</param>
        public URI(String           Name,
                   DNSQueryClasses  Class,
                   TimeSpan         TimeToLive,
                   UInt16           Priority,
                   UInt16           Weight,
                   String           Target)

            : base(Name,
                   TypeId,
                   Class,
                   TimeToLive,
                   $"{Priority} {Weight} {Target}")

        {

            this.Priority  = Priority;
            this.Weight    = Weight;
            this.Target    = Target;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Priority={Priority}, Weight={Weight}, Target={Target}, {base.ToString()}";

        #endregion

    }

}
