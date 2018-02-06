/*
 * Copyright (c) 2010-2018, Achim 'ahzf' Friedland <achim.friedland@graphdefined.com>
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

using org.GraphDefined.Vanaheimr.Illias;
using System;
using System.Globalization;
using System.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP uniform resource identifier.
    /// </summary>
    public struct HTTPURI : IEquatable<HTTPURI>,
                            IComparable<HTTPURI>,
                            IComparable
    {

        #region Data

        /// <summary>
        /// The internal identifier.
        /// </summary>
        private readonly String  InternalId;

        #endregion

        #region Properties

        /// <summary>
        /// The length of the HTTP uniform resource identifier.
        /// </summary>
        public UInt32 Length
            => (UInt32) InternalId.Length;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP uniform resource identifier
        /// </summary>
        /// <param name="URI">The uniform resource identifier.</param>
        private HTTPURI(String URI)
        {
            this.InternalId = URI;
        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text representation of a HTTP uniform resource identifier.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP uniform resource identifier.</param>
        public static HTTPURI Parse(String Text)
        {

            if (TryParse(Text, out HTTPURI HTTPURI))
                return HTTPURI;

            throw new ArgumentException("The given string could not be parsed as a HTTP uniform resource identifier!", nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text representation of a HTTP uniform resource identifier.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP uniform resource identifier.</param>
        public static HTTPURI? TryParse(String Text)
        {

            if (TryParse(Text, out HTTPURI URI))
                return URI;

            return new HTTPURI?();

        }

        #endregion

        #region TryParse(VersionString, out URI)

        /// <summary>
        /// Try to parse the given text representation of a HTTP uniform resource identifier.
        /// </summary>
        /// <param name="Text">A text representation of a HTTP uniform resource identifier.</param>
        /// <param name="URI">The parsed HTTP uniform resource identifier.</param>
        public static Boolean TryParse(String Text, out HTTPURI URI)
        {

            if (Text != null)
                Text = Text.Trim();

            if (!Text.StartsWith("/"))
                Text = "/" + Text;

            URI = new HTTPURI(Text);
            return true;

        }

        #endregion


        public Boolean Contains(String Text)
            => InternalId.Contains(Text);


        public HTTPURI Substring(Int32 StartIndex)
            => Parse(InternalId.Substring(StartIndex));

        public HTTPURI Substring(Int32 StartIndex, Int32 EndIndex)
            => Parse(InternalId.Substring(StartIndex, EndIndex));


        public int IndexOf(char value, int startIndex, int count)
            => InternalId.IndexOf(value, startIndex, count);

        public int IndexOf(char value, int startIndex)
            => InternalId.IndexOf(value, startIndex);

        public int IndexOf(String value)
            => InternalId.IndexOf(value);

        public int IndexOf(String value, int startIndex)
            => InternalId.IndexOf(value, startIndex);

        public int IndexOf(String value, int startIndex, int count)
            => InternalId.IndexOf(value, startIndex, count);

        public int IndexOf(String value, StringComparison comparisonType)
            => InternalId.IndexOf(value, comparisonType);

        public int IndexOf(String value, int startIndex, StringComparison comparisonType)
            => InternalId.IndexOf(value, startIndex, comparisonType);

        public int IndexOf(char value)
            => InternalId.IndexOf(value);


        public bool StartsWith(String value, StringComparison comparisonType)
            => InternalId.StartsWith(value, comparisonType);

        public bool StartsWith(String value, bool ignoreCase, CultureInfo culture)
            => InternalId.StartsWith(value, ignoreCase, culture);

        public bool StartsWith(String value)
            => InternalId.StartsWith(value);


        public bool EndsWith(String value)
            => InternalId.EndsWith(value);

        public bool EndsWith(String value, StringComparison comparisonType)
            => InternalId.EndsWith(value, comparisonType);

        public bool EndsWith(String value, bool ignoreCase, CultureInfo culture)
            => InternalId.EndsWith(value, ignoreCase, culture);


        public Boolean IsNullOrEmpty()
        {

            if (String.IsNullOrEmpty(InternalId))
                return true;

            return String.IsNullOrEmpty(InternalId.Trim());

        }

        public Boolean IsNotNullOrEmpty()
            => !IsNullOrEmpty();


        #region Operator overloading

        #region Operator == (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPURI1, HTTPURI2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPURI1 == null) || ((Object) HTTPURI2 == null))
                return false;

            return HTTPURI1.Equals(HTTPURI2);

        }

        #endregion

        #region Operator == (HTTPURI1, Text)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="Text">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPURI HTTPURI1, String Text)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPURI1, Text))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPURI1 == null) || ((Object) Text == null))
                return false;

            return HTTPURI1.Equals(Text);

        }

        #endregion

        #region Operator != (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
            => !(HTTPURI1 == HTTPURI2);

        #endregion

        #region Operator != (HTTPURI1, Text)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="Text">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPURI HTTPURI1, String Text)
            => !(HTTPURI1 == Text);

        #endregion

        #region Operator <  (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
        {

            if ((Object) HTTPURI1 == null)
                throw new ArgumentNullException(nameof(HTTPURI1), "The given HTTPURI1 must not be null!");

            return HTTPURI1.CompareTo(HTTPURI2) < 0;

        }

        #endregion

        #region Operator <= (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
            => !(HTTPURI1 > HTTPURI2);

        #endregion

        #region Operator >  (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
        {

            if ((Object) HTTPURI1 == null)
                throw new ArgumentNullException(nameof(HTTPURI1), "The given HTTPURI1 must not be null!");

            return HTTPURI1.CompareTo(HTTPURI2) > 0;

        }

        #endregion

        #region Operator >= (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
            => !(HTTPURI1 < HTTPURI2);

        #endregion


        #region Operator + (HTTPURI1, Text)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="Text">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static HTTPURI operator + (HTTPURI HTTPURI1, String Text)
            => HTTPURI1.EndsWith("/") && Text.StartsWith("/")
                   ? Parse(HTTPURI1.ToString() + Text.Substring(1))
                   : Parse(HTTPURI1.ToString() + Text);

        #endregion

        #region Operator + (HTTPURI1, HTTPURI2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI1">A HTTP uniform resource identifier.</param>
        /// <param name="HTTPURI2">Another HTTP uniform resource identifier.</param>
        /// <returns>true|false</returns>
        public static HTTPURI operator + (HTTPURI HTTPURI1, HTTPURI HTTPURI2)
            => HTTPURI1.EndsWith("/") && HTTPURI2.StartsWith("/")
                   ? Parse(HTTPURI1.ToString() + HTTPURI2.Substring(1))
                   : Parse(HTTPURI1.ToString() + HTTPURI2);

        #endregion

        #endregion

        #region IComparable<HTTPURI> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is HTTPURI))
                throw new ArgumentException("The given object is not a HTTP uniform resource identifier!");

            return CompareTo((HTTPURI) Object);

        }

        #endregion

        #region CompareTo(HTTPURI)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPURI">An object to compare with.</param>
        public Int32 CompareTo(HTTPURI HTTPURI)
        {

            if ((Object) HTTPURI == null)
                throw new ArgumentNullException("The given HTTP uniform resource identifier must not be null!");

            return InternalId.CompareTo(HTTPURI.InternalId);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPURI> Members

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

            if (!(Object is HTTPURI))
                return false;

            return Equals((HTTPURI) Object);

        }

        #endregion

        #region Equals(HTTPURI)

        /// <summary>
        /// Compares two HTTPURIs for equality.
        /// </summary>
        /// <param name="HTTPURI">A HTTPURI to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPURI HTTPURI)
        {

            if ((Object) HTTPURI == null)
                return false;

            return InternalId.Equals(HTTPURI.InternalId);

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
