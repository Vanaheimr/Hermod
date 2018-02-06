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

using System;
using System.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// A HTTP version identifier.
    /// </summary>
    public struct HTTPVersion : IEquatable<HTTPVersion>,
                                IComparable<HTTPVersion>,
                                IComparable
    {

        #region Properties

        /// <summary>
        /// The major of this HTTP version
        /// </summary>
        public UInt16  Major   { get; }

        /// <summary>
        /// The minor of this HTTP version
        /// </summary>
        public UInt16  Minor   { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HTTP version identifier.
        /// </summary>
        /// <param name="Major">The major number.</param>
        /// <param name="Minor">The minor number.</param>
        public HTTPVersion(UInt16 Major, UInt16 Minor)
        {
            this.Major  = Major;
            this.Minor  = Minor;
        }

        #endregion


        #region Parse   (Text)

        /// <summary>
        /// Parse the given text representation of a HTTP version, e.g. "HTTP/1.1".
        /// </summary>
        /// <param name="Text">A text representation of a HTTP version, e.g. "HTTP/1.1".</param>
        public static HTTPVersion Parse(String Text)
        {

            if (TryParse(Text, out HTTPVersion Version))
                return Version;

            throw new ArgumentException("The given string could not be parsed as a HTTP version!", nameof(Text));

        }

        #endregion

        #region TryParse(Text)

        /// <summary>
        /// Try to parse the given text representation of a HTTP version, e.g. "HTTP/1.1".
        /// </summary>
        /// <param name="Text">A text representation of a HTTP version, e.g. "HTTP/1.1".</param>
        public static HTTPVersion? TryParse(String Text)
        {

            if (TryParse(Text, out HTTPVersion Version))
                return Version;

            return new HTTPVersion?();

        }

        #endregion

        #region TryParse(Text, out Version)

        /// <summary>
        /// Try to parse the given text representation of a HTTP version, e.g. "HTTP/1.1".
        /// </summary>
        /// <param name="Text">A text representation of a HTTP version, e.g. "HTTP/1.1".</param>
        /// <param name="Version">The parsed HTTP version</param>
        public static Boolean TryParse(String Text, out HTTPVersion Version)
        {

            var MajorMinor = Text.Split(new Char[] { '.' }, StringSplitOptions.None);

            if (MajorMinor.Length != 2 ||
                !UInt16.TryParse(MajorMinor[0], out UInt16 Major) ||
                !UInt16.TryParse(MajorMinor[1], out UInt16 Minor))
            {
                Version = default(HTTPVersion);
                return false;
            }

            Version = new HTTPVersion(Major, Minor);
            return true;

        }

        #endregion


        #region Static HTTP versions

        /// <summary>
        /// HTTP 1.0
        /// </summary>
        public static readonly HTTPVersion HTTP_1_0 = new HTTPVersion(1, 0);

        /// <summary>
        /// HTTP 1.1
        /// </summary>
        public static readonly HTTPVersion HTTP_1_1 = new HTTPVersion(1, 1);

        #endregion


        #region Operator overloading

        #region Operator == (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">A HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPVersion HTTPVersion1, HTTPVersion HTTPVersion2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPVersion1, HTTPVersion2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) HTTPVersion1 == null) || ((Object) HTTPVersion2 == null))
                return false;

            return HTTPVersion1.Equals(HTTPVersion2);

        }

        #endregion

        #region Operator != (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">A HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPVersion HTTPVersion1, HTTPVersion HTTPVersion2)
            => !(HTTPVersion1 == HTTPVersion2);

        #endregion

        #region Operator <  (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">A HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPVersion HTTPVersion1, HTTPVersion HTTPVersion2)
        {

            if ((Object) HTTPVersion1 == null)
                throw new ArgumentNullException(nameof(HTTPVersion1), "The given HTTPVersion1 must not be null!");

            return HTTPVersion1.CompareTo(HTTPVersion2) < 0;

        }

        #endregion

        #region Operator <= (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">A HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPVersion HTTPVersion1, HTTPVersion HTTPVersion2)
            => !(HTTPVersion1 > HTTPVersion2);

        #endregion

        #region Operator >  (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">A HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPVersion HTTPVersion1, HTTPVersion HTTPVersion2)
        {

            if ((Object) HTTPVersion1 == null)
                throw new ArgumentNullException(nameof(HTTPVersion1), "The given HTTPVersion1 must not be null!");

            return HTTPVersion1.CompareTo(HTTPVersion2) > 0;

        }

        #endregion

        #region Operator >= (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">A HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPVersion HTTPVersion1, HTTPVersion HTTPVersion2)
            => !(HTTPVersion1 < HTTPVersion2);

        #endregion

        #endregion

        #region IComparable<HTTPVersion> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            if (!(Object is HTTPVersion))
                throw new ArgumentException("The given object is not a HTTP version!");

            return CompareTo((HTTPVersion) Object);

        }

        #endregion

        #region CompareTo(HTTPVersion)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion">An object to compare with.</param>
        public Int32 CompareTo(HTTPVersion HTTPVersion)
        {

            if ((Object) HTTPVersion == null)
                throw new ArgumentNullException("The given HTTP version must not be null!");

            var _MajorCompared = Major.CompareTo(HTTPVersion.Major);

            if (_MajorCompared != 0)
                return _MajorCompared;

            return Minor.CompareTo(HTTPVersion.Minor);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPVersion> Members

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

            if (!(Object is HTTPVersion))
                return false;

            return Equals((HTTPVersion) Object);

        }

        #endregion

        #region Equals(HTTPVersion)

        /// <summary>
        /// Compares two HTTPVersions for equality.
        /// </summary>
        /// <param name="HTTPVersion">A HTTPVersion to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPVersion HTTPVersion)
        {

            if ((Object) HTTPVersion == null)
                return false;

            return Major == HTTPVersion.Major &&
                   Minor == HTTPVersion.Minor;

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

                return Major.GetHashCode() * 3 ^
                       Minor.GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()
            => String.Concat(Major, ".", Minor);

        #endregion

    }

}
