/*
 * Copyright (c) 2010-2022, Achim Friedland <achim.friedland@graphdefined.com>
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
using System.Collections;
using System.Collections.Generic;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A single HTTP cookie.
    /// </summary>
    public class HTTPCookie : IEquatable <HTTPCookie>,
                              IComparable<HTTPCookie>,
                              IEnumerable<KeyValuePair<String, String>>
    {

        #region Data

        private static readonly Char[] MultipleCookiesSplitter = new Char[] { ';' };

        /// <summary>
        /// The data stored within the cookie.
        /// </summary>
        private readonly Dictionary<String, String> crumbs;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the cookie.
        /// </summary>
        public HTTPCookieName  Name   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP cookie.
        /// </summary>
        private HTTPCookie(HTTPCookieName                             Name,
                           IEnumerable<KeyValuePair<String, String>>  Crumbs)
        {

            this.Name    = Name;
            this.crumbs  = new Dictionary<String, String>();

            if (Crumbs != null)
            {
                foreach (var crumb in Crumbs)
                {
                    if (!this.crumbs.ContainsKey(crumb.Key))
                        this.crumbs.Add(crumb.Key, crumb.Value);
                }
            }

        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text as a single HTTP cookie.
        /// </summary>
        /// <param name="Text">A text representation of a single HTTP cookie.</param>
        public static HTTPCookie Parse(String Text)
        {

            if (TryParse(Text, out HTTPCookie httpCookie))
                return httpCookie;

            return null;

        }

        #endregion

        #region TryParse(Text, out HTTPCookie)

        /// <summary>
        /// Parse the given string as a single HTTP cookie.
        /// </summary>
        /// <param name="Text">A text representation of a single HTTP cookie.</param>
        /// <param name="HTTPCookie">The parsed HTTP cookie.</param>
        public static Boolean TryParse(String Text, out HTTPCookie HTTPCookie)
        {

            if (Text.IsNotNullOrEmpty())
                Text = Text.Trim();

            if (Text.IsNullOrEmpty())
            {
                HTTPCookie = null;
                return false;
            }

            if (!HTTPCookieName.TryParse(Text.Substring(0, Text.IndexOf('=')),
                                         out HTTPCookieName cookieName))
            {
                HTTPCookie = null;
                return false;
            }

            var split2 = new Char[] { '=' };
            var crumbs = new Dictionary<String, String>();

            Text.Substring (Text.IndexOf('=') + 1).
                 Split     (':').
                 SafeSelect(command => command.Split(split2, 2)).
                 ForEach   (tuple   => {
                               if (tuple[0].IsNotNullOrEmpty())
                               {
                                   if (tuple.Length == 1)
                                   {
                                       if (!crumbs.ContainsKey(tuple[0].Trim()))
                                           crumbs.Add(tuple[0].Trim(), "");
                                       else
                                           crumbs[tuple[0].Trim()] = "";
                                   }
                                   else if (tuple.Length == 2)
                                   {
                                       if (!crumbs.ContainsKey(tuple[0].Trim()))
                                           crumbs.Add(tuple[0].Trim(), tuple[1]);
                                       else
                                           crumbs[tuple[0].Trim()] = tuple[1];
                                   }
                               }
                          });

            HTTPCookie = new HTTPCookie(cookieName,
                                        crumbs);

            return true;

        }

        #endregion


        #region this[Crumb]

        /// <summary>
        /// Return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        public String this[String Crumb]
        {
            get
            {

                if (crumbs.TryGetValue(Crumb, out String Value))
                    return Value;

                return null;

            }
        }

        #endregion

        #region Get(Crumb)

        /// <summary>
        /// Return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        public String Get(String Crumb)
        {

            if (crumbs.TryGetValue(Crumb, out String Value))
                return Value;

            return null;

        }

        #endregion

        #region TryGet(Crumb, out Value)

        /// <summary>
        /// Try to return the value of the given crumb.
        /// </summary>
        /// <param name="Crumb">The key/name of the crumb.</param>
        /// <param name="Value">The value of the crumb.</param>
        public Boolean TryGet(String Crumb, out String Value)

            => crumbs.TryGetValue(Crumb, out Value);

        #endregion


        public IEnumerator<KeyValuePair<String, String>> GetEnumerator()
            => crumbs.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => crumbs.GetEnumerator();


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
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => Name.ToString();

        #endregion

    }

}
