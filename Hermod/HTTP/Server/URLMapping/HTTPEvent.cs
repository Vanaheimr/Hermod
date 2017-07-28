/*
 * Copyright (c) 2010-2017, GraphDefined GmbH
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
        public UInt64               Id          { get; }

        /// <summary>
        /// The subevent identification of the event.
        /// </summary>
        public String               Subevent    { get; }

        /// <summary>
        /// The timestamp of the event.
        /// </summary>
        public DateTime             Timestamp   { get; }

        /// <summary>
        /// The attached data of the event.
        /// </summary>
        public IEnumerable<String>  Data        { get; }

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
                   DateTime.Now,
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
                   DateTime.Now,
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
            this.Subevent   = Subevent;
            this.Data       = Data;

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

            // Check if the given object is a HTTPEvent.
            var HTTPEvent = Object as HTTPEvent;
            if ((Object) HTTPEvent == null)
                return false;

            return this.Equals(HTTPEvent);

        }

        #endregion

        #region Equals(VertexId)

        /// <summary>
        /// Compares two HTTPEvents for equality.
        /// </summary>
        /// <param name="HTTPEvent">A HTTPEvent to compare with.</param>
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

            // Check if the given object is a HTTPEvent.
            var HTTPEvent = Object as HTTPEvent;
            if ((Object) HTTPEvent == null)
                throw new ArgumentException("The given object is not a HTTPEvent!");

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
                throw new ArgumentNullException("HTTPEvent", "The given HTTPEvent must not be null!");

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
            return Subevent.GetHashCode() ^ Id.GetHashCode();
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        public override String ToString()
        {

            if (Subevent == null || Subevent.Trim() == "")
                return String.Concat("id: ",     Id, Environment.NewLine,
                                     "data: ",   Data.Aggregate((a, b) => { return a + Environment.NewLine + "data: " + b; }), Environment.NewLine);

            else
                return String.Concat("event: ",  Subevent, Environment.NewLine,
                                     "id: ",     Id,       Environment.NewLine, 
                                     "data: ",   Data.Aggregate((a, b) => { return a + Environment.NewLine + "data: " + b; }), Environment.NewLine);

        }

        #endregion

    }

}
