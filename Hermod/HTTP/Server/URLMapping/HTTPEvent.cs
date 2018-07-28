/*
 * Copyright (c) 2010-2018, GraphDefined GmbH
 * Author: Achim Friedland <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <http://www.github.com/Vanaheimr/Hermod>
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

using System;
using System.Linq;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP event.
    /// </summary>
    public class HTTPEvent : IEquatable<HTTPEvent>,
                             IComparable<HTTPEvent>,
                             IComparable
    {

        #region Properties

        /// <summary>
        /// The identification of the event.
        /// </summary>
        public UInt64               Id           { get; }

        /// <summary>
        /// The subevent identification of the event.
        /// </summary>
        public String               Subevent     { get; }

        /// <summary>
        /// The timestamp of the event.
        /// </summary>
        public DateTime             Timestamp    { get; }

        /// <summary>
        /// The attached data of the event.
        /// </summary>
        public IEnumerable<String>  Data         { get; }

        #endregion

        #region Constructor(s)

        #region HTTPEvent(Id, Data)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Data">The attached data of the event.</param>
        public HTTPEvent(UInt64           Id,
                         params String[]  Data)

            : this(Id,
                   DateTime.UtcNow,
                   String.Empty,
                   Data)

        { }

        #endregion

        #region HTTPEvent(Id, Timestamp, Data)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached data of the event.</param>
        public HTTPEvent(UInt64           Id,
                         DateTime         Timestamp,
                         params String[]  Data)

            : this(Id,
                   Timestamp,
                   String.Empty,
                   Data)

        { }

        #endregion

        #region HTTPEvent(Id, Subevent, Data)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Subevent">The subevent.</param>
        /// <param name="Data">The attached data of the event.</param>
        public HTTPEvent(UInt64           Id,
                         String           Subevent,
                         params String[]  Data)

            : this(Id,
                   DateTime.UtcNow,
                   Subevent,
                   Data)

        { }

        #endregion

        #region HTTPEvent(Id, Timestamp, Subevent, Data)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Subevent">The subevent.</param>
        /// <param name="Data">The attached data of the event.</param>
        public HTTPEvent(UInt64           Id,
                         DateTime         Timestamp,
                         String           Subevent,
                         params String[]  Data)
        {

            this.Id         = Id;
            this.Timestamp  = Timestamp;
            this.Subevent   = Subevent?.Trim() ?? "";
            this.Data       = Data             ?? new String[0];

        }

        #endregion

        #endregion


        #region IEquatable<HTTPEvent> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        /// <returns>true|false</returns>
        public override Boolean Equals(Object Object)
        {

            if (Object == null)
                return false;

            if (!(Object is HTTPEvent HTTPEvent))
                return false;

            return Equals(HTTPEvent);

        }

        #endregion

        #region Equals(HTTPEvent)

        /// <summary>
        /// Compares two HTTP events for equality.
        /// </summary>
        /// <param name="OtherHTTPEvent">A HTTP event to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPEvent OtherHTTPEvent)
        {

            if ((Object) OtherHTTPEvent == null)
                return false;

            return Id.Equals(OtherHTTPEvent.Id);

        }

        #endregion

        #endregion

        #region IComparable<HTTPEvent> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is HTTPEvent HTTPEvent))
                throw new ArgumentException("The given object is not a HTTP event!");

            return CompareTo(HTTPEvent);

        }

        #endregion

        #region CompareTo(OtherHTTPEvent)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="OtherHTTPEvent">Another HTTP event.</param>
        public Int32 CompareTo(HTTPEvent OtherHTTPEvent)
        {

            if ((Object) OtherHTTPEvent == null)
                throw new ArgumentNullException(nameof(OtherHTTPEvent), "The given HTTP event must not be null!");

            return Id.CompareTo(OtherHTTPEvent.Id);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return Id.      GetHashCode() * 3 ^
                       Subevent.GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(Subevent.IsNotNullOrEmpty()
                                 ? "event: " + Subevent + Environment.NewLine
                                 : "",
                             "id: ",    Id,                                                                Environment.NewLine,
                             "data: ",  Data.Aggregate((a, b) => a + Environment.NewLine + "data: " + b),  Environment.NewLine);

        #endregion

    }

}
