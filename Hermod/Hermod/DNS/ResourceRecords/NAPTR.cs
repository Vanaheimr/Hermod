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
    /// Extensions methods for DNS NAPTR resource records.
    /// </summary>
    public static class DNS_NAPTR_Extensions
    {

        #region AddToCache(this DNSClient, DomainName, NAPTRRecord)

        /// <summary>
        /// Add a DNS NAPTR record cache entry.
        /// </summary>
        /// <param name="DNSClient">A DNS client.</param>
        /// <param name="DomainName">A domain name.</param>
        /// <param name="NAPTRRecord">A DNS NAPTR record</param>
        public static void AddToCache(this DNSClient  DNSClient,
                                      String          DomainName,
                                      NAPTR           NAPTRRecord)
        {

            if (DomainName.IsNullOrEmpty())
                return;

            DNSClient.DNSCache.Add(
                DomainName,
                IPSocket.LocalhostV4(IPPort.DNS),
                NAPTRRecord
            );

        }

        #endregion

    }


    /// <summary>
    /// The DNS Naming Authority Pointer (NAPTR) resource record.
    /// https://www.rfc-editor.org/rfc/rfc3403
    /// </summary>
    public class NAPTR : ADNSResourceRecord
    {

        #region Data

        /// <summary>
        /// The DNS Naming Authority Pointer (NAPTR) resource record type identifier.
        /// </summary>
        public const UInt16 TypeId = 35;

        #endregion

        #region Properties

        /// <summary>
        /// A 16-bit unsigned integer specifying the order in which the NAPTR records MUST be processed.
        /// Low numbers are processed before high numbers.
        /// </summary>
        public UInt16  Order          { get; }

        /// <summary>
        /// A 16-bit unsigned integer that specifies the order in which NAPTR records with the same Order value should be processed.
        /// Low numbers are processed before high numbers.
        /// </summary>
        public UInt16  Preference     { get; }

        /// <summary>
        /// A character-string containing flags to control aspects of the rewriting and interpretation of the fields.
        /// Flags are single characters from the set [A-Z0-9], case preserved.
        /// </summary>
        public String  Flags          { get; }

        /// <summary>
        /// A character-string that specifies the Service Parameters applicable to this delegation path.
        /// </summary>
        public String  Services       { get; }

        /// <summary>
        /// A character-string containing a substitution expression (regular expression) applied to the original string to construct the next domain name.
        /// </summary>
        public String  RegExpr        { get; }

        /// <summary>
        /// A domain-name which specifies the new value in the case where the regular expression is a simple replacement.
        /// Can be "." if terminal.
        /// </summary>
        public String  Replacement    { get; }

        #endregion

        #region Constructors

        #region NAPTR(Stream)

        /// <summary>
        /// Create a new NAPTR resource record from the given stream.
        /// </summary>
        /// <param name="Stream">A stream containing the NAPTR resource record data.</param>
        public NAPTR(Stream Stream)

            : base(Stream,
                   TypeId)

        {

            this.Order        = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Preference   = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Flags        = DNSTools.ExtractCharacterString(Stream);
            this.Services     = DNSTools.ExtractCharacterString(Stream);
            this.RegExpr      = DNSTools.ExtractCharacterString(Stream);
            this.Replacement  = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region NAPTR(Name, Stream)

        /// <summary>
        /// Create a new NAPTR resource record from the given name and stream.
        /// </summary>
        /// <param name="Name">The DNS name of this NAPTR resource record.</param>
        /// <param name="Stream">A stream containing the NAPTR resource record data.</param>
        public NAPTR(String  Name,
                     Stream  Stream)

            : base(Name,
                   TypeId,
                   Stream)

        {

            this.Order        = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Preference   = (UInt16) ((Stream.ReadByte() & byte.MaxValue) << 8 | Stream.ReadByte() & byte.MaxValue);
            this.Flags        = DNSTools.ExtractCharacterString(Stream);
            this.Services     = DNSTools.ExtractCharacterString(Stream);
            this.RegExpr      = DNSTools.ExtractCharacterString(Stream);
            this.Replacement  = DNSTools.ExtractName(Stream);

        }

        #endregion

        #region NAPTR(Name, Class, TimeToLive, Order, Preference, Flags, Services, RegExpr, Replacement)

        /// <summary>
        /// Create a new DNS NAPTR record.
        /// </summary>
        /// <param name="Name">The DNS name of this NAPTR record.</param>
        /// <param name="Class">The DNS query class of this NAPTR record.</param>
        /// <param name="TimeToLive">The time to live of this NAPTR record.</param>
        /// <param name="Order">The order in which the NAPTR records MUST be processed.</param>
        /// <param name="Preference">The order in which NAPTR records with the same Order value should be processed.</param>
        /// <param name="Flags">The flags to control aspects of the rewriting and interpretation of the fields.</param>
        /// <param name="Services">The service parameters applicable to this delegation path.</param>
        /// <param name="RegExpr">The substitution expression (regular expression) applied to the original string to construct the next domain name.</param>
        /// <param name="Replacement">The new value in the case where the regular expression is a simple replacement.</param>
        public NAPTR(String           Name,
                     DNSQueryClasses  Class,
                     TimeSpan         TimeToLive,
                     UInt16           Order,
                     UInt16           Preference,
                     String           Flags,
                     String           Services,
                     String           RegExpr,
                     String           Replacement)

            : base(Name,
                   TypeId,
                   Class,
                   TimeToLive,
                   $"{Order} {Preference} '{Flags}' '{Services}' '{RegExpr}' {Replacement}")

        {

            this.Order        = Order;
            this.Preference   = Preference;
            this.Flags        = Flags;
            this.Services     = Services;
            this.RegExpr      = RegExpr;
            this.Replacement  = Replacement;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this DNS record.
        /// </summary>
        public override String ToString()

            => $"Order={Order}, Preference={Preference}, Flags='{Flags}', Services='{Services}', RegExpr='{RegExpr}', Replacement={Replacement}, {base.ToString()}";

        #endregion

    }

}
