/*
 * Copyright (c) 2010-2019, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The unique identification of a HTTP Event Source.
    /// </summary>
    public struct HTTPEventSource_Id : IId,
                                       IEquatable<HTTPEventSource_Id>,
                                       IComparable<HTTPEventSource_Id>

    {

        #region Data

        /// <summary>
        /// The internal identification.
        /// </summary>
        private readonly String  InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// The length of the HTTP Event Source identification.
        /// </summary>
        public UInt64 Length
            => (UInt64) InternalId?.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP Event Source identification based on the given string.
        /// </summary>
        /// <param name="String">The string representation of the HTTP Event Source identification.</param>
        private HTTPEventSource_Id(String  String)
        {
            this.InternalId  = String;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a HTTP Event Source identification.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Event Source identification.</param>
        public static HTTPEventSource_Id Parse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a HTTP Event Source identification must not be null or empty!");

            #endregion

            return new HTTPEventSource_Id(Text);

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a HTTP Event Source identification.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Event Source identification.</param>
        public static HTTPEventSource_Id? TryParse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a HTTP Event Source identification must not be null or empty!");

            #endregion

            if (TryParse(Text, out HTTPEventSource_Id _HTTPEventSourceId))
                return _HTTPEventSourceId;

            return new HTTPEventSource_Id?();

        }

        #endregion

        #region (static) TryParse(Text, out HTTPEventSourceId)

        /// <summary>
        /// Try to parse the given string as a HTTP Event Source identification.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId">The parsed HTTP Event Source identification.</param>
        public static Boolean TryParse(String Text, out HTTPEventSource_Id HTTPEventSourceId)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a HTTP Event Source identification must not be null or empty!");

            #endregion

            try
            {
                HTTPEventSourceId = new HTTPEventSource_Id(Text);
                return true;
            }
            catch (Exception)
            {
                HTTPEventSourceId = default(HTTPEventSource_Id);
                return false;
            }

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this HTTP Event Source identification.
        /// </summary>
        public HTTPEventSource_Id Clone
            => new HTTPEventSource_Id(new String(InternalId.ToCharArray()));

        #endregion


        #region Operator overloading

        #region Operator == (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">A HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPEventSource_Id HTTPEventSourceId1, HTTPEventSource_Id HTTPEventSourceId2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPEventSourceId1, HTTPEventSourceId2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPEventSourceId1 == null) || ((Object) HTTPEventSourceId2 == null))
                return false;

            return HTTPEventSourceId1.Equals(HTTPEventSourceId2);

        }

        #endregion

        #region Operator != (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">A HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPEventSource_Id HTTPEventSourceId1, HTTPEventSource_Id HTTPEventSourceId2)
            => !(HTTPEventSourceId1 == HTTPEventSourceId2);

        #endregion

        #region Operator <  (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">A HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPEventSource_Id HTTPEventSourceId1, HTTPEventSource_Id HTTPEventSourceId2)
        {

            if ((Object) HTTPEventSourceId1 == null)
                throw new ArgumentNullException(nameof(HTTPEventSourceId1), "The given HTTPEventSourceId1 must not be null!");

            return HTTPEventSourceId1.CompareTo(HTTPEventSourceId2) < 0;

        }

        #endregion

        #region Operator <= (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">A HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPEventSource_Id HTTPEventSourceId1, HTTPEventSource_Id HTTPEventSourceId2)
            => !(HTTPEventSourceId1 > HTTPEventSourceId2);

        #endregion

        #region Operator >  (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">A HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPEventSource_Id HTTPEventSourceId1, HTTPEventSource_Id HTTPEventSourceId2)
        {

            if ((Object) HTTPEventSourceId1 == null)
                throw new ArgumentNullException(nameof(HTTPEventSourceId1), "The given HTTPEventSourceId1 must not be null!");

            return HTTPEventSourceId1.CompareTo(HTTPEventSourceId2) > 0;

        }

        #endregion

        #region Operator >= (HTTPEventSourceId1, HTTPEventSourceId2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId1">A HTTP Event Source identification.</param>
        /// <param name="HTTPEventSourceId2">Another HTTP Event Source identification.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPEventSource_Id HTTPEventSourceId1, HTTPEventSource_Id HTTPEventSourceId2)
            => !(HTTPEventSourceId1 < HTTPEventSourceId2);

        #endregion

        #endregion

        #region IComparable<HTTPEventSourceId> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is HTTPEventSource_Id))
                throw new ArgumentException("The given object is not a HTTP Event Source identification!",
                                            nameof(Object));

            return CompareTo((HTTPEventSource_Id) Object);

        }

        #endregion

        #region CompareTo(HTTPEventSourceId)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPEventSourceId">An object to compare with.</param>
        public Int32 CompareTo(HTTPEventSource_Id HTTPEventSourceId)
        {

            if ((Object) HTTPEventSourceId == null)
                throw new ArgumentNullException(nameof(HTTPEventSourceId),  "The given HTTP Event Source identification must not be null!");

            return String.Compare(InternalId, HTTPEventSourceId.InternalId, StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPEventSourceId> Members

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

            if (!(Object is HTTPEventSource_Id))
                return false;

            return Equals((HTTPEventSource_Id) Object);

        }

        #endregion

        #region Equals(HTTPEventSourceId)

        /// <summary>
        /// Compares two HTTP Event Source identifications for equality.
        /// </summary>
        /// <param name="HTTPEventSourceId">An HTTP Event Source identification to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPEventSource_Id HTTPEventSourceId)
        {

            if ((Object) HTTPEventSourceId == null)
                return false;

            return InternalId.Equals(HTTPEventSourceId.InternalId);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
            => InternalId.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => InternalId;

        #endregion

    }

}
