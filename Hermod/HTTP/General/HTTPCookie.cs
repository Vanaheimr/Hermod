/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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
    /// The unique identification of a HTTP cookie.
    /// </summary>
    public class HTTPCookie : IEquatable <HTTPCookie>,
                              IComparable<HTTPCookie>

    {

        #region Properties

        /// <summary>
        /// The name of the cookie.
        /// </summary>
        public String                                     Name     { get; }

        /// <summary>
        /// The stored data within the cookie.
        /// </summary>
        public IEnumerable<KeyValuePair<String, String>>  Crumbs   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP cookie.
        /// </summary>
        private HTTPCookie(String                                     Name,
                           IEnumerable<KeyValuePair<String, String>>  Crumbs)
        {

            this.Name    = Name;
            this.Crumbs  = Crumbs;

        }

        #endregion


        #region Parse(Text)

        /// <summary>
        /// Parse the given text as HTTP cookie.
        /// </summary>
        /// <param name="Text"></param>
        public static HTTPCookie Parse(String Text)
        {

            HTTPCookie _HTTPCookie;

            if (TryParse(Text, out _HTTPCookie))
                return _HTTPCookie;

            return null;

        }

        #endregion

        #region TryParse(Text, out HTTPCookie)

        /// <summary>
        /// Parse the given string as a HTTP cookie.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP cookie.</param>
        /// <param name="HTTPCookie">The parsed HTTP cookie.</param>
        public static Boolean TryParse(String Text, out HTTPCookie HTTPCookie)
        {

            if (Text.IsNullOrEmpty())
            {
                HTTPCookie = null;
                return false;
            }

            var CookieName = Text.Trim().Split('=').FirstOrDefault();

            if (CookieName.IsNullOrEmpty())
            {
                HTTPCookie = null;
                return false;
            }

            HTTPCookie = new HTTPCookie(CookieName,
                                        Text.Trim().DoubleSplit(':', '='));

            return true;

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPCookie HTTPCookie1, HTTPCookie HTTPCookie2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPCookie1, HTTPCookie2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPCookie1 == null) || ((Object) HTTPCookie2 == null))
                return false;

            return HTTPCookie1.Equals(HTTPCookie2);

        }

        #endregion

        #region Operator != (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPCookie HTTPCookie1, HTTPCookie HTTPCookie2)
            => !(HTTPCookie1 == HTTPCookie2);

        #endregion

        #region Operator <  (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPCookie HTTPCookie1, HTTPCookie HTTPCookie2)
        {

            if ((Object) HTTPCookie1 == null)
                throw new ArgumentNullException("The given HTTPCookie1 must not be null!");

            return HTTPCookie1.CompareTo(HTTPCookie2) < 0;

        }

        #endregion

        #region Operator <= (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPCookie HTTPCookie1, HTTPCookie HTTPCookie2)
            => !(HTTPCookie1 > HTTPCookie2);

        #endregion

        #region Operator >  (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPCookie HTTPCookie1, HTTPCookie HTTPCookie2)
        {

            if ((Object) HTTPCookie1 == null)
                throw new ArgumentNullException("The given HTTPCookie1 must not be null!");

            return HTTPCookie1.CompareTo(HTTPCookie2) > 0;

        }

        #endregion

        #region Operator >= (HTTPCookie1, HTTPCookie2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie1">A HTTP cookie.</param>
        /// <param name="HTTPCookie2">Another HTTP cookie.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPCookie HTTPCookie1, HTTPCookie HTTPCookie2)
            => !(HTTPCookie1 < HTTPCookie2);

        #endregion

        #endregion

        #region IComparable<HTTPCookie> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException(nameof(Object), "The given object must not be null!");

            var HTTPCookie = Object as HTTPCookie;
            if ((Object) HTTPCookie == null)
                throw new ArgumentException("The given object is not a HTTP cookie!", nameof(Object));

            return CompareTo(HTTPCookie);

        }

        #endregion

        #region CompareTo(HTTPCookie)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookie">An object to compare with.</param>
        public Int32 CompareTo(HTTPCookie HTTPCookie)
        {

            if ((Object) HTTPCookie == null)
                throw new ArgumentNullException(nameof(HTTPCookie), "The given HTTPCookie must not be null!");

            return String.Compare(ToString(), HTTPCookie.ToString(), StringComparison.Ordinal);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPCookie> Members

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

            var HTTPCookie = Object as HTTPCookie;
            if ((Object) HTTPCookie == null)
                return false;

            return Equals(HTTPCookie);

        }

        #endregion

        #region Equals(HTTPCookie)

        /// <summary>
        /// Compares two HTTPCookies for equality.
        /// </summary>
        /// <param name="HTTPCookie">A HTTPCookie to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPCookie HTTPCookie)
        {

            if ((Object) HTTPCookie == null)
                return false;

            return ToString().Equals(HTTPCookie.ToString());

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
                return Name.GetHashCode();
            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
            => Name;

        #endregion

    }

}
