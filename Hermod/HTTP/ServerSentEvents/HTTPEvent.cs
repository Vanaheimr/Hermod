/*
 * Copyright (c) 2010-2021, GraphDefined GmbH
 * Author: Achim Friedland <achim.friedland@graphdefined.com>
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

using System;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP event.
    /// </summary>
    public class HTTPEvent<T> : IEquatable<HTTPEvent<T>>,
                                IComparable<HTTPEvent<T>>,
                                IComparable
    {

        #region Properties

        /// <summary>
        /// The identification of the event.
        /// </summary>
        public UInt64    Id                  { get; }

        /// <summary>
        /// The subevent identification of the event.
        /// </summary>
        public String    Subevent            { get; }

        /// <summary>
        /// The timestamp of the event.
        /// </summary>
        public DateTime  Timestamp           { get; }

        /// <summary>
        /// The attached data of the event.
        /// </summary>
        public T         Data                { get; }

        /// <summary>
        /// A text-representation of the attached data of the event.
        /// </summary>
        public String    SerializedHeader    { get; }

        /// <summary>
        /// A text-representation of the attached data of the event.
        /// </summary>
        public String    SerializedData      { get; }

        #endregion

        #region Constructor(s)

        #region HTTPEvent(Id,                      Data, SerializedData)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Data">The attached data of the event.</param>
        /// <param name="SerializedHeader">The HTTP SSE header of the attached data of the event.</param>
        /// <param name="SerializedData">A text-representation of the attached data of the event.</param>
        public HTTPEvent(UInt64  Id,
                         T       Data,
                         String  SerializedHeader,
                         String  SerializedData)

            : this(Id,
                   DateTime.UtcNow,
                   String.Empty,
                   Data,
                   SerializedHeader,
                   SerializedData)

        { }

        #endregion

        #region HTTPEvent(Id, Timestamp,           Data, SerializedData)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Data">The attached data of the event.</param>
        /// <param name="SerializedHeader">The HTTP SSE header of the attached data of the event.</param>
        /// <param name="SerializedData">A text-representation of the attached data of the event.</param>
        public HTTPEvent(UInt64    Id,
                         DateTime  Timestamp,
                         T         Data,
                         String    SerializedHeader,
                         String    SerializedData)

            : this(Id,
                   Timestamp,
                   String.Empty,
                   Data,
                   SerializedHeader,
                   SerializedData)

        { }

        #endregion

        #region HTTPEvent(Id,            Subevent, Data, SerializedData)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Subevent">The subevent.</param>
        /// <param name="Data">The attached data of the event.</param>
        /// <param name="SerializedHeader">The HTTP SSE header of the attached data of the event.</param>
        /// <param name="SerializedData">A text-representation of the attached data of the event.</param>
        public HTTPEvent(UInt64  Id,
                         String  Subevent,
                         T       Data,
                         String  SerializedHeader,
                         String  SerializedData)

            : this(Id,
                   DateTime.UtcNow,
                   Subevent,
                   Data,
                   SerializedHeader,
                   SerializedData)

        { }

        #endregion

        #region HTTPEvent(Id, Timestamp, Subevent, Data, SerializedData)

        /// <summary>
        /// Create a new HTTP event based on the given parameters.
        /// </summary>
        /// <param name="Id">The id of the event.</param>
        /// <param name="Timestamp">The timestamp of the event.</param>
        /// <param name="Subevent">The subevent.</param>
        /// <param name="Data">The attached data of the event.</param>
        /// <param name="SerializedHeader">The HTTP SSE header of the attached data of the event.</param>
        /// <param name="SerializedData">A text-representation of the attached data of the event.</param>
        public HTTPEvent(UInt64    Id,
                         DateTime  Timestamp,
                         String    Subevent,
                         T         Data,
                         String    SerializedHeader,
                         String    SerializedData)
        {

            this.Id                = Id;
            this.Timestamp         = Timestamp;
            this.Subevent          = Subevent?.Trim() ?? "";
            this.Data              = Data;
            this.SerializedHeader  = SerializedHeader;
            this.SerializedData    = SerializedData;

        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (HTTPEvent1, HTTPEvent2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent1">A HTTP event.</param>
        /// <param name="HTTPEvent2">Another HTTP event.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPEvent<T> HTTPEvent1, HTTPEvent<T> HTTPEvent2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPEvent1, HTTPEvent2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPEvent1 == null) || ((Object) HTTPEvent2 == null))
                return false;

            return HTTPEvent1.Equals(HTTPEvent2);

        }

        #endregion

        #region Operator != (HTTPEvent1, HTTPEvent2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent1">A HTTP event.</param>
        /// <param name="HTTPEvent2">Another HTTP event.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPEvent<T> HTTPEvent1, HTTPEvent<T> HTTPEvent2)
            => !(HTTPEvent1 == HTTPEvent2);

        #endregion

        #region Operator <  (HTTPEvent1, HTTPEvent2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent1">A HTTP event.</param>
        /// <param name="HTTPEvent2">Another HTTP event.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPEvent<T> HTTPEvent1, HTTPEvent<T> HTTPEvent2)
        {

            if ((Object) HTTPEvent1 == null)
                throw new ArgumentNullException(nameof(HTTPEvent1), "The given HTTPEvent1 must not be null!");

            return HTTPEvent1.CompareTo(HTTPEvent2) < 0;

        }

        #endregion

        #region Operator <= (HTTPEvent1, HTTPEvent2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent1">A HTTP event.</param>
        /// <param name="HTTPEvent2">Another HTTP event.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPEvent<T> HTTPEvent1, HTTPEvent<T> HTTPEvent2)
            => !(HTTPEvent1 > HTTPEvent2);

        #endregion

        #region Operator >  (HTTPEvent1, HTTPEvent2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent1">A HTTP event.</param>
        /// <param name="HTTPEvent2">Another HTTP event.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPEvent<T> HTTPEvent1, HTTPEvent<T> HTTPEvent2)
        {

            if ((Object) HTTPEvent1 == null)
                throw new ArgumentNullException(nameof(HTTPEvent1), "The given HTTPEvent1 must not be null!");

            return HTTPEvent1.CompareTo(HTTPEvent2) > 0;

        }

        #endregion

        #region Operator >= (HTTPEvent1, HTTPEvent2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent1">A HTTP event.</param>
        /// <param name="HTTPEvent2">Another HTTP event.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPEvent<T> HTTPEvent1, HTTPEvent<T> HTTPEvent2)
            => !(HTTPEvent1 < HTTPEvent2);

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

            if (Object is null)
                return false;

            if (!(Object is HTTPEvent<T> HTTPEvent))
                return false;

            return Equals(HTTPEvent);

        }

        #endregion

        #region Equals(HTTPEvent)

        /// <summary>
        /// Compares two HTTP events for equality.
        /// </summary>
        /// <param name="HTTPEvent">A HTTP event to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPEvent<T> HTTPEvent)
        {

            if (HTTPEvent is null)
                return false;

            return Id.Equals(HTTPEvent.Id);

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

            if (Object is null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is HTTPEvent<T> HTTPEvent))
                throw new ArgumentException("The given object is not a HTTP event!");

            return CompareTo(HTTPEvent);

        }

        #endregion

        #region CompareTo(OtherHTTPEvent)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEvent">Another HTTP event.</param>
        public Int32 CompareTo(HTTPEvent<T> HTTPEvent)
        {

            if (HTTPEvent is null)
                throw new ArgumentNullException(nameof(HTTPEvent), "The given HTTP event must not be null!");

            return Id.CompareTo(HTTPEvent.Id);

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
            => SerializedData;

        #endregion

    }

}
