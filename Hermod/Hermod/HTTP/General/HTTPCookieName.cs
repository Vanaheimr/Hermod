/*
 * Copyright (c) 2010-2024 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// Extension methods for HTTP cookies name.
    /// </summary>
    public static class HTTPCookieNameExtensions
    {

        /// <summary>
        /// Indicates whether this HTTP cookie namee is null or empty.
        /// </summary>
        /// <param name="HTTPCookieName">A HTTP cookie name.</param>
        public static Boolean IsNullOrEmpty(this HTTPCookieName? HTTPCookieName)
            => !HTTPCookieName.HasValue || HTTPCookieName.Value.IsNullOrEmpty;

        /// <summary>
        /// Indicates whether this HTTP cookie name is null or empty.
        /// </summary>
        /// <param name="HTTPCookieName">A HTTP cookie name.</param>
        public static Boolean IsNotNullOrEmpty(this HTTPCookieName? HTTPCookieName)
            => HTTPCookieName.HasValue && HTTPCookieName.Value.IsNotNullOrEmpty;

    }


    /// <summary>
    /// The unique name of a HTTP cookie.
    /// </summary>
    public readonly struct HTTPCookieName : IId<HTTPCookieName>
    {

        #region Data

        /// <summary>
        /// The internal name.
        /// </summary>
        private readonly String InternalName;

        #endregion

        #region Properties

        /// <summary>
        /// Indicates whether this identification is null or empty.
        /// </summary>
        public Boolean IsNullOrEmpty
            => InternalName.IsNullOrEmpty();

        /// <summary>
        /// Indicates whether this identification is NOT null or empty.
        /// </summary>
        public Boolean IsNotNullOrEmpty
            => InternalName.IsNullOrEmpty();

        /// <summary>
        /// The length of the HTTP cookie name.
        /// </summary>
        public UInt64 Length
            => (UInt64) (InternalName?.Length ?? 0);

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP cookie name based on the given text.
        /// </summary>
        /// <param name="Name">The text representation of the HTTP cookie name.</param>
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

            if (TryParse(Text, out var httpCookieName))
                return httpCookieName;

            throw new ArgumentException($"Invalid text representation of a HTTP cookie name: '" + Text + "'!",
                                        nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text)

        /// <summary>
        /// Try to parse the given string as a HTTP cookie name.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP cookie name.</param>
        public static HTTPCookieName? TryParse(String Text)
        {

            if (TryParse(Text, out var httpCookieName))
                return httpCookieName;

            return null;

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

            if (Text.IsNotNullOrEmpty())
            {
                try
                {
                    HTTPCookieName = new HTTPCookieName(Text.Trim());
                    return true;
                }
                catch
                { }
            }

            HTTPCookieName = default;
            return false;

        }

        #endregion

        #region Clone

        /// <summary>
        /// Clone this HTTP cookie name.
        /// </summary>
        public HTTPCookieName Clone

            => new (
                   new String(InternalName?.ToCharArray())
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
        public static Boolean operator == (HTTPCookieName HTTPCookieName1,
                                           HTTPCookieName HTTPCookieName2)

            => HTTPCookieName1.Equals(HTTPCookieName2);

        #endregion

        #region Operator != (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPCookieName HTTPCookieName1,
                                           HTTPCookieName HTTPCookieName2)

            => !HTTPCookieName1.Equals(HTTPCookieName2);

        #endregion

        #region Operator <  (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPCookieName HTTPCookieName1,
                                          HTTPCookieName HTTPCookieName2)

            => HTTPCookieName1.CompareTo(HTTPCookieName2) < 0;

        #endregion

        #region Operator <= (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPCookieName HTTPCookieName1,
                                           HTTPCookieName HTTPCookieName2)

            => HTTPCookieName1.CompareTo(HTTPCookieName2) <= 0;

        #endregion

        #region Operator >  (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPCookieName HTTPCookieName1,
                                          HTTPCookieName HTTPCookieName2)

            => HTTPCookieName1.CompareTo(HTTPCookieName2) > 0;

        #endregion

        #region Operator >= (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPCookieName HTTPCookieName1,
                                           HTTPCookieName HTTPCookieName2)

            => HTTPCookieName1.CompareTo(HTTPCookieName2) >= 0;

        #endregion


        #region Operator +  (HTTPCookieName1, HTTPCookieName2)

        /// <summary>
        /// Combines two HTTP cookies names.
        /// </summary>
        /// <param name="HTTPCookieName1">A HTTP cookie name.</param>
        /// <param name="HTTPCookieName2">Another HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static HTTPCookieName operator + (HTTPCookieName HTTPCookieName1,
                                                 HTTPCookieName HTTPCookieName2)

            => Parse(HTTPCookieName1.InternalName + HTTPCookieName2.InternalName);

        #endregion

        #region Operator +  (HTTPCookieName1, Text)

        /// <summary>
        /// Combines a HTTP cookies name with a text.
        /// </summary>
        /// <param name="HTTPCookieName">A HTTP cookie name.</param>
        /// <param name="Text">A text.</param>
        /// <returns>true|false</returns>
        public static HTTPCookieName operator + (HTTPCookieName HTTPCookieName,
                                                 String         Text)

            => Parse(HTTPCookieName.InternalName + Text);

        #endregion

        #region Operator +  (HTTPCookieName1, Text)

        /// <summary>
        /// Combines a HTTP cookies name with a text.
        /// </summary>
        /// <param name="Text">A text.</param>
        /// <param name="HTTPCookieName">A HTTP cookie name.</param>
        /// <returns>true|false</returns>
        public static HTTPCookieName operator + (String         Text,
                                                 HTTPCookieName HTTPCookieName)

            => Parse(Text + HTTPCookieName.InternalName);

        #endregion

        #endregion

        #region IComparable<HTTPCookieName> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP cookie names.
        /// </summary>
        /// <param name="Object">A HTTP cookie name to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPCookieName httpCookieName
                   ? CompareTo(httpCookieName)
                   : throw new ArgumentException("The given object is not a name of a HTTP cookie!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPCookieName)

        /// <summary>
        /// Compares two HTTP cookie names.
        /// </summary>
        /// <param name="HTTPCookieName">A HTTP cookie name to compare with.</param>
        public Int32 CompareTo(HTTPCookieName HTTPCookieName)

            => String.Compare(InternalName,
                              HTTPCookieName.InternalName,
                              StringComparison.Ordinal);

        #endregion

        #endregion

        #region IEquatable<HTTPCookieName> Members

        #region Equals(Object)

        /// <summary>
        /// Compares two HTTP cookie names for equality.
        /// </summary>
        /// <param name="Object">A HTTP cookie name to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPCookieName httpCookieName &&
                   Equals(httpCookieName);

        #endregion

        #region Equals(HTTPCookieName)

        /// <summary>
        /// Compares two HTTP cookie names for equality.
        /// </summary>
        /// <param name="HTTPCookieName">A HTTP cookie name to compare with.</param>
        public Boolean Equals(HTTPCookieName HTTPCookieName)

            => String.Equals(InternalName,
                             HTTPCookieName.InternalName,
                             StringComparison.Ordinal);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
        /// <returns>The HashCode of this object.</returns>
        public override Int32 GetHashCode()

            => InternalName?.GetHashCode() ?? 0;

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => InternalName ?? "";

        #endregion

    }

}
