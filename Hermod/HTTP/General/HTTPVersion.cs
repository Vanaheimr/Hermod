/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
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
using System.Linq;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// An HTTP version identifier.
    /// </summary>
    /// <param name="Major">The major number.</param>
    /// <param name="Minor">The minor number.</param>
    public readonly struct HTTPVersion(UInt16 Major,
                                       UInt16 Minor)

        : IEquatable<HTTPVersion>,
          IComparable<HTTPVersion>,
          IComparable

    {

        #region Data

        public static readonly Char[] splitter = ['.', '/'];

        #endregion

        #region Properties

        /// <summary>
        /// The major of this HTTP version
        /// </summary>
        public UInt16 Major { get; } = Major;

        /// <summary>
        /// The minor of this HTTP version
        /// </summary>
        public UInt16 Minor { get; } = Minor;

        #endregion


        #region Parse    (Text)

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

        #region TryParse (Text)

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

        #region TryParse (Text, out Version)

        /// <summary>
        /// Try to parse the given text representation of a HTTP version, e.g. "HTTP/1.1".
        /// </summary>
        /// <param name="Text">A text representation of a HTTP version, e.g. "HTTP/1.1".</param>
        /// <param name="Version">The parsed HTTP version</param>
        public static Boolean TryParse(String Text, out HTTPVersion Version)
        {

            Version = default;

            if (String.IsNullOrWhiteSpace(Text))
                return false;

            Text = Text.Trim();

            // Remove optional "HTTP/" prefix (case-insensitive)
            if (Text.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                Text = Text[5..].Trim();

            var majorMinor = Text.Split(splitter, StringSplitOptions.None);

            if (majorMinor.Length == 2 &&
                UInt16.TryParse(majorMinor[0], out var major) &&
                UInt16.TryParse(majorMinor[1], out var minor))
            {

                Version = new HTTPVersion(
                              major,
                              minor
                          );

                return true;

            }

            Version = default;
            return false;

        }

        #endregion


        #region Static HTTP versions

        /// <summary>
        /// HTTP/1.0
        /// </summary>
        public static readonly HTTPVersion HTTP_1_0 = new (1, 0);

        /// <summary>
        /// HTTP/1.1
        /// </summary>
        public static readonly HTTPVersion HTTP_1_1 = new (1, 1);

        /// <summary>
        /// HTTP/2.0
        /// </summary>
        public static readonly HTTPVersion HTTP_2_0 = new (2, 0);

        #endregion


        #region Operator overloading

        #region Operator == (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">An HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator == (HTTPVersion HTTPVersion1,
                                           HTTPVersion HTTPVersion2)

            => HTTPVersion1.Equals(HTTPVersion2);

        #endregion

        #region Operator != (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">An HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator != (HTTPVersion HTTPVersion1,
                                           HTTPVersion HTTPVersion2)

            => !HTTPVersion1.Equals(HTTPVersion2);

        #endregion

        #region Operator <  (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">An HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator < (HTTPVersion HTTPVersion1,
                                          HTTPVersion HTTPVersion2)

            => HTTPVersion1.CompareTo(HTTPVersion2) < 0;

        #endregion

        #region Operator <= (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">An HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator <= (HTTPVersion HTTPVersion1,
                                           HTTPVersion HTTPVersion2)

            => HTTPVersion1.CompareTo(HTTPVersion2) <= 0;

        #endregion

        #region Operator >  (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">An HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator > (HTTPVersion HTTPVersion1,
                                          HTTPVersion HTTPVersion2)

            => HTTPVersion1.CompareTo(HTTPVersion2) > 0;

        #endregion

        #region Operator >= (HTTPVersion1, HTTPVersion2)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion1">An HTTP version.</param>
        /// <param name="HTTPVersion2">Another HTTP version.</param>
        /// <returns>true|false</returns>
        public static Boolean operator >= (HTTPVersion HTTPVersion1,
                                           HTTPVersion HTTPVersion2)

            => HTTPVersion1.CompareTo(HTTPVersion2) >= 0;

        #endregion

        #endregion

        #region IComparable<HTTPVersion> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object? Object)
        {

            if (Object is not HTTPVersion httpVersion)
                throw new ArgumentException("The given object is not a HTTP version!", nameof(Object));

            return CompareTo(httpVersion);

        }

        #endregion

        #region CompareTo(HTTPVersion)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPVersion">An object to compare with.</param>
        public readonly Int32 CompareTo(HTTPVersion HTTPVersion)
        {

            var c = Major.CompareTo(HTTPVersion.Major);

            if (c == 0)
                c = Minor.CompareTo(HTTPVersion.Minor);

            return c;

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
        public override Boolean Equals(Object? Object)

            => Object is HTTPVersion httpVersion &&
                   Equals(httpVersion);

        #endregion

        #region Equals(HTTPVersion)

        /// <summary>
        /// Compares two HTTPVersions for equality.
        /// </summary>
        /// <param name="HTTPVersion">An HTTPVersion to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPVersion HTTPVersion)

            => Major.Equals(HTTPVersion.Major) &&
               Minor.Equals(HTTPVersion.Minor);

        #endregion

        #endregion

        #region (override) GetHashCode()

        /// <summary>
        /// Return the HashCode of this object.
        /// </summary>
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

            => $"{Major}.{Minor}";

        #endregion

    }

}
