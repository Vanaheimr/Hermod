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
    /// The abstract DNS Resource Record class for objects returned in
    /// answers, authorities and additional record DNS responses.
    /// </summary>
    public abstract class ADNSResourceRecord
    {

        #region Properties

        /// <summary>
        /// The DNS name of this resource record.
        /// </summary>
        public String           Name          { get; }

        /// <summary>
        /// The type of this resource record.
        /// </summary>
        public UInt16           Type          { get; }

        /// <summary>
        /// The class of this resource record.
        /// </summary>
        public DNSQueryClasses  Class         { get; }

        /// <summary>
        /// The time to live of this resource record.
        /// </summary>
        public TimeSpan         TimeToLive    { get; }

        /// <summary>
        /// The end of life of this resource record.
        /// </summary>
        [NoDNSPaketInformation]
        public DateTime         EndOfLife     { get; }

        /// <summary>
        /// The source IP address of this resource record, if available.
        /// </summary>
        [NoDNSPaketInformation]
        public IIPAddress?      Source        { get; }

        /// <summary>
        /// The text representation of this resource record, if available.
        /// </summary>
        public String?          RText         { get; }

        #endregion

        #region Constructor(s)

        #region (protected) ADNSResourceRecord(DNSStream, Type)

        /// <summary>
        /// Create a new DNS resource record from the given DNS stream and type.
        /// </summary>
        /// <param name="DNSStream">A stream containing the DNS resource record data.</param>
        /// <param name="Type">A valid DNS resource record type.</param>
        protected ADNSResourceRecord(Stream  DNSStream,
                                     UInt16  Type)
        {

            this.Name        = DNSTools.ExtractName(DNSStream);
            this.Type        = Type;

            var type         = (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue;
            if (type != Type)
                throw new ArgumentException($"Invalid DNS resource record type! Expected '{Type}', but got '{type}'!");

            this.Class       = (DNSQueryClasses)   ((DNSStream.ReadByte() & Byte.MaxValue) <<  8 |  DNSStream.ReadByte() & Byte.MaxValue);
            this.TimeToLive  = TimeSpan.FromSeconds((DNSStream.ReadByte() & Byte.MaxValue) << 24 | (DNSStream.ReadByte() & Byte.MaxValue) << 16 | (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);
            this.EndOfLife   = Illias.Timestamp.Now + TimeToLive;

            //var RDLength     = (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region (protected) ADNSResourceRecord(Name, Type, DNSStream)

        /// <summary>
        /// Create a new DNS resource record from the given name, type and DNS stream.
        /// </summary>
        /// <param name="Name">A DNS name of this resource record.</param>
        /// <param name="Type">A valid DNS resource record type.</param>
        /// <param name="DNSStream">A stream containing the DNS resource record data.</param>
        protected ADNSResourceRecord(String  Name,
                                     UInt16  Type,
                                     Stream  DNSStream)
        {

            this.Name        = Name;
            this.Type        = Type;
            this.Class       = (DNSQueryClasses)   ((DNSStream.ReadByte() & Byte.MaxValue) <<  8 |  DNSStream.ReadByte() & Byte.MaxValue);
            this.TimeToLive  = TimeSpan.FromSeconds((DNSStream.ReadByte() & Byte.MaxValue) << 24 | (DNSStream.ReadByte() & Byte.MaxValue) << 16 | (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue);
            this.EndOfLife   = Timestamp.Now + TimeToLive;

            //var RDLength     = (DNSStream.ReadByte() & Byte.MaxValue) << 8 | DNSStream.ReadByte() & Byte.MaxValue;

        }

        #endregion

        #region (protected) ADNSResourceRecord(Name, Type, Class, TimeToLive)

        /// <summary>
        /// Create a new DNS resource record with the given name, type, class and time to live.
        /// </summary>
        /// <param name="Name">A DNS name of this resource record.</param>
        /// <param name="Type">A valid DNS resource record type.</param>
        /// <param name="Class">A valid DNS query class.</param>
        /// <param name="TimeToLive">A time span representing the time to live of this resource record.</param>
        protected ADNSResourceRecord(String           Name,
                                     UInt16           Type,
                                     DNSQueryClasses  Class,
                                     TimeSpan         TimeToLive)
        {

            this.Name        = Name;
            this.Type        = Type;
            this.Class       = Class;
            this.TimeToLive  = TimeToLive;
            this.EndOfLife   = Timestamp.Now + TimeToLive;

        }

        #endregion

        #region (protected) ADNSResourceRecord(Name, Type, Class, TimeToLive, RText)

        protected ADNSResourceRecord(String           Name,
                                     UInt16           Type,
                                     DNSQueryClasses  Class,
                                     TimeSpan         TimeToLive,
                                     String           RText)
        {

            this.Name        = Name;
            this.Type        = Type;
            this.Class       = Class;
            this.TimeToLive  = TimeToLive;
            this.EndOfLife   = Timestamp.Now + TimeToLive;
            this.RText       = RText;

        }

        #endregion

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"Name={Name}, Type={Type}, Class={Class}, TTL={TimeToLive.TotalSeconds} seconds, EndOfLife='{EndOfLife}'",

                   Source is not null
                       ? $"Source = {Source}"
                       : String.Empty

               );

        #endregion

    }

}