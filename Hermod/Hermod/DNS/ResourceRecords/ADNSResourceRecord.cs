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
    /// Base Resource Record class for objects returned in 
    /// answers, authorities and additional record DNS responses. 
    /// </summary>
    public abstract class ADNSResourceRecord
    {

        #region Properties

        public String           Name          { get; }

        public UInt16           Type          { get; }

        public DNSQueryClasses  Class         { get; }

        public TimeSpan         TimeToLive    { get; }


        [NoDNSPaketInformation]
        public DateTime         EndOfLife     { get; }


        [NoDNSPaketInformation]
        public IIPAddress?      Source        { get; }

        public String?          RText         { get; }

        #endregion

        #region Constructor(s)

        #region (protected) ADNSResourceRecord(DNSStream, Type)

        protected ADNSResourceRecord(Stream DNSStream, UInt16 Type)
        {

            this.Name          = DNSTools.ExtractName(DNSStream);

            this.Type = Type;
            //this._Type          = (DNSResourceRecordTypes) ((DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue);

            //if (_Type != Type)
            //    throw new ArgumentException("Invalid DNS RR Type!");

            this.Class         = (DNSQueryClasses) ((DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue);
            this.TimeToLive    = TimeSpan.FromSeconds((DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue);
            this.EndOfLife     = Illias.Timestamp.Now + TimeToLive;

            var RDLength        = (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;

        }

        #endregion

        #region (protected) ADNSResourceRecord(Name, Type, DNSStream)

        protected ADNSResourceRecord(String  Name,
                                     UInt16  Type,
                                     Stream  DNSStream)
        {

            this.Name          = Name;
            this.Type          = Type;
            this.Class         = (DNSQueryClasses) ((DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue);
            this.TimeToLive    = TimeSpan.FromSeconds((DNSStream.ReadByte() & byte.MaxValue) << 24 | (DNSStream.ReadByte() & byte.MaxValue) << 16 | (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue);
            this.EndOfLife     = Illias.Timestamp.Now + TimeToLive;

            var RDLength        = (DNSStream.ReadByte() & byte.MaxValue) << 8 | DNSStream.ReadByte() & byte.MaxValue;

        }

        #endregion

        #region (protected) ADNSResourceRecord(Name, Type, Class, TimeToLive)

        protected ADNSResourceRecord(String           Name,
                                     UInt16           Type,
                                     DNSQueryClasses  Class,
                                     TimeSpan         TimeToLive)
        {

            this.Name          = Name;
            this.Type          = Type;
            this.Class         = Class;
            this.TimeToLive    = TimeToLive;
            this.EndOfLife     = Illias.Timestamp.Now + TimeToLive;

        }

        #endregion

        #region (protected) ADNSResourceRecord(Name, Type, Class, TimeToLive, RText)

        protected ADNSResourceRecord(String           Name,
                                     UInt16           Type,
                                     DNSQueryClasses  Class,
                                     TimeSpan         TimeToLive,
                                     String           RText)
        {

            this.Name          = Name;
            this.Type          = Type;
            this.Class         = Class;
            this.TimeToLive    = TimeToLive;
            this.EndOfLife     = Illias.Timestamp.Now + TimeToLive;
            this.RText         = RText;

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
                       : ""

               );

        #endregion

    }

}