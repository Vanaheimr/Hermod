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
    /// The unique name of a HTTP cookie.
    /// </summary>
    public struct HTTPCookieName : IId,
                                   IEquatable<HTTPCookieName>,
                                   IComparable<HTTPCookieName>
    {

        #region Data

        /// <summary>
        /// The internal name.
        /// </summary>
        private readonly String  InternalName;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalName.IsNullOrEmpty();

        /// <summary>
        /// The length of the HTTP cookie name.
        /// </summary>
        public UInt64 Length
            => (UInt64) InternalName.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP cookie name based on the given string.
        /// </summary>
        /// <param name="Name">The string representation of the HTTP cookie name.</param>
        private HTTPCookieName(String Name)
        {
            this.InternalName = Name;
        }

        #endregion


        #region (static) Parse   (Text)

        /// <summary>
        /// Parse the given string as a HTTP cookie name.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP cookie name.</param>
        public static HTTPCookieName Parse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a HTTP cookie name must not be null or empty!");

            #endregion

            if (TryParse(Text, out HTTPCookieName _HTTPCookieName))
                return _HTTPCookieName;

            throw new ArgumentException("The given text '" + Text + "' is not a valid text representation of a HTTP cookie!");

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a HTTP cookie name.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP cookie name.</param>
        public static HTTPCookieName? TryParse(String Text)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a HTTP cookie name must not be null or empty!");

            #endregion

            if (TryParse(Text, out HTTPCookieName _HTTPCookieName))
                return _HTTPCookieName;

            return new HTTPCookieName?();

        }

        #endregion

        #region (static) TryParse(Text, out HTTPCookieName)

        /// <summary>
        /// Try to parse the given string as a HTTP cookie name.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP cookie name.</param>
        /// <param name="HTTPCookieName">The parsed HTTP cookie name.</param>
        public static Boolean TryParse(String Text, out HTTPCookieName HTTPCookieName)
        {

            #region Initial checks

            if (Text != null)
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Text), "The given text representation of a HTTP cookie name must not be null or empty!");

            #endregion

            try
            {
                HTTPCookieName = new HTTPCookieName(Text);
                return true;
            }
            catch (Exception)
            {
                HTTPCookieName = default(HTTPCookieName);
                return false;
            }

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this HTTP cookie name.
        /// </summary>

        public HTTPCookieName Clone
            => new HTTPCookieName(
                   new String(InternalName.ToCharArray())
               );

        #endregion


        #region Operator overloading

        #region Operator == (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPCookieName HTTPCookieName1, HTTPCookieName HTTPCookieName2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPCookieName1, HTTPCookieName2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPCookieName1 == null) || ((Object) HTTPCookieName2 == null))
                return false;

            return HTTPCookieName1.Equals(HTTPCookieName2);

        }

        #endregion

        #region Operator != (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPCookieName HTTPCookieName1, HTTPCookieName HTTPCookieName2)
            => !(HTTPCookieName1 == HTTPCookieName2);

        #endregion

        #region Operator <  (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPCookieName HTTPCookieName1, HTTPCookieName HTTPCookieName2)
        {

            if ((Object) HTTPCookieName1 == null)
                throw new ArgumentNullException(nameof(HTTPCookieName1), "The given HTTPCookieName1 must not be null!");

            return HTTPCookieName1.CompareTo(HTTPCookieName2) < 0;

        }

        #endregion

        #region Operator <= (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPCookieName HTTPCookieName1, HTTPCookieName HTTPCookieName2)
            => !(HTTPCookieName1 > HTTPCookieName2);

        #endregion

        #region Operator >  (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPCookieName HTTPCookieName1, HTTPCookieName HTTPCookieName2)
        {

            if ((Object) HTTPCookieName1 == null)
                throw new ArgumentNullException(nameof(HTTPCookieName1), "The given HTTPCookieName1 must not be null!");

            return HTTPCookieName1.CompareTo(HTTPCookieName2) > 0;

        }

        #endregion

        #region Operator >= (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPCookieName HTTPCookieName1, HTTPCookieName HTTPCookieName2)
            => !(HTTPCookieName1 < HTTPCookieName2);

        #endregion

        #endregion

        #region IComparable<HTTPCookieName> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            if (!(Object is HTTPCookieName))
                throw new ArgumentException("The given object is not a HTTP cookie name!",
                                            nameof(Object));

            return CompareTo((HTTPCookieName) Object);

        }

        #endregion

        #region CompareTo(HTTPCookieName)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName">An object to compare with.</param>
        public Int32 CompareTo(HTTPCookieName HTTPCookieName)
        {

            if ((Object) HTTPCookieName == null)
                throw new ArgumentNullException(nameof(HTTPCookieName),  "The given HTTP cookie name must not be null!");

            return String.Compare(InternalName, HTTPCookieName.InternalName, StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPCookieName> Members

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

            if (!(Object is HTTPCookieName))
                return false;

            return Equals((HTTPCookieName) Object);

        }

        #endregion

        #region Equals(HTTPCookieName)

        /// <summary>
        /// Compares two HTTP cookie names for equality.
        /// </summary>
        /// <param name="HTTPCookieName">An HTTP cookie name to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPCookieName HTTPCookieName)
        {

            if ((Object) HTTPCookieName == null)
                return false;

            return InternalName.Equals(HTTPCookieName.InternalName);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()
            => InternalName.GetHashCode();

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => InternalName;

        #endregion

    }

}
