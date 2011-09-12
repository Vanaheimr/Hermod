/*
 * Copyright (c) 2010-2011, Achim 'ahzf' Friedland <code@ahzf.de>
 * This file is part of Hermod
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

namespace de.ahzf.Hermod.HTTP
{

    /// <summary>
    /// A HTTP version identifier.
    /// </summary>
    public class HTTPVersion : IEquatable<HTTPVersion>, IComparable<HTTPVersion>, IComparable
    {

        #region Properties

        /// <summary>
        /// The major of this HTTP version
        /// </summary>
        public UInt16 Major { get; private set; }

        /// <summary>
        /// The minor of this HTTP version
        /// </summary>
        public UInt16 Minor { get; private set; }

        #endregion

        #region Constructor(s)

        #region HTTPVersion(Major, Minor)

        /// <summary>
        /// Create a new HTTP version identifier.
        /// </summary>
        /// <param name="Major">The major number.</param>
        /// <param name="Minor">The minor number.</param>
        public HTTPVersion(UInt16 Major, UInt16 Minor)
        {
            this.Major = Major;
            this.Minor = Minor;
        }

        #endregion

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


        #region TryParseVersionString(VersionString, out HTTPVersion)

        /// <summary>
        /// Tries to find the apropriate HTTPVersion for the given string, e.g. "HTTP/1.1".
        /// </summary>
        /// <param name="VersionString">A HTTP version as stirng, e.g. "HTTP/1.1"</param>
        /// <param name="HTTPVersion">The parsed HTTP version</param>
        /// <returns>true or false</returns>
        public static Boolean TryParseVersionString(String VersionString, out HTTPVersion HTTPVersion)
        {

            HTTPVersion = null;

            var _MajorMinor = VersionString.Split(new Char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            if (_MajorMinor.Length != 2)
                return false;

            UInt16 __Major;
            UInt16 __Minor;

            if (!UInt16.TryParse(_MajorMinor[0], out __Major))
                return false;

            if (!UInt16.TryParse(_MajorMinor[1], out __Minor))
                return false;

            // Use reflection to find a static representation of the given version string.
            HTTPVersion = (from _FieldInfo in typeof(HTTPVersion).GetFields()
                           let    _HTTPVersion = _FieldInfo.GetValue(null) as HTTPVersion
                           where  _HTTPVersion != null
                           where  _HTTPVersion.Major == __Major
                           where  _HTTPVersion.Minor == __Minor
                           select _HTTPVersion).FirstOrDefault();

            if (HTTPVersion != null)
                return true;

            return false;

        }

        #endregion


        #region Operator overloading

        #region Operator == (myHTTPVersion1, myHTTPVersion2)

        public static Boolean operator == (HTTPVersion myHTTPVersion1, HTTPVersion myHTTPVersion2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myHTTPVersion1, myHTTPVersion2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) myHTTPVersion1 == null) || ((Object) myHTTPVersion2 == null))
                return false;

            return myHTTPVersion1.Equals(myHTTPVersion2);

        }

        #endregion

        #region Operator != (myHTTPVersion1, myHTTPVersion2)

        public static Boolean operator != (HTTPVersion myHTTPVersion1, HTTPVersion myHTTPVersion2)
        {
            return !(myHTTPVersion1 == myHTTPVersion2);
        }

        #endregion

        #region Operator <  (myHTTPVersion1, myHTTPVersion2)

        public static Boolean operator < (HTTPVersion myHTTPVersion1, HTTPVersion myHTTPVersion2)
        {

            // Check if myHTTPVersion1 is null
            if ((Object) myHTTPVersion1 == null)
                throw new ArgumentNullException("Parameter myHTTPVersion1 must not be null!");

            // Check if myHTTPVersion2 is null
            if ((Object) myHTTPVersion2 == null)
                throw new ArgumentNullException("Parameter myHTTPVersion2 must not be null!");

            return myHTTPVersion1.CompareTo(myHTTPVersion2) < 0;

        }

        #endregion

        #region Operator >  (myHTTPVersion1, myHTTPVersion2)

        public static Boolean operator > (HTTPVersion myHTTPVersion1, HTTPVersion myHTTPVersion2)
        {

            // Check if myHTTPVersion1 is null
            if ((Object) myHTTPVersion1 == null)
                throw new ArgumentNullException("Parameter myHTTPVersion1 must not be null!");

            // Check if myHTTPVersion2 is null
            if ((Object) myHTTPVersion2 == null)
                throw new ArgumentNullException("Parameter myHTTPVersion2 must not be null!");

            return myHTTPVersion1.CompareTo(myHTTPVersion2) > 0;

        }

        #endregion

        #region Operator <= (myHTTPVersion1, myHTTPVersion2)

        public static Boolean operator <= (HTTPVersion myHTTPVersion1, HTTPVersion myHTTPVersion2)
        {
            return !(myHTTPVersion1 > myHTTPVersion2);
        }

        #endregion

        #region Operator >= (myHTTPVersion1, myHTTPVersion2)

        public static Boolean operator >= (HTTPVersion myHTTPVersion1, HTTPVersion myHTTPVersion2)
        {
            return !(myHTTPVersion1 < myHTTPVersion2);
        }

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

            // Check if the given object is a HTTPVersion.
            var HTTPVersion = Object as HTTPVersion;
            if ((Object) HTTPVersion == null)
                throw new ArgumentException("The given object is not a HTTPVersion!");

            return CompareTo(HTTPVersion);

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
                throw new ArgumentNullException("The given HTTPVersion must not be null!");

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

            // Check if the given object is a HTTPVersion.
            var HTTPVersion = Object as HTTPVersion;
            if ((Object) HTTPVersion == null)
                return false;

            return this.Equals(HTTPVersion);

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

            if (Major != HTTPVersion.Major)
                return false;

            if (Minor != HTTPVersion.Minor)
                return false;

            return true;

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
            return (Major.ToString() + Minor.ToString()).GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string represtentation of this object.
        /// </summary>
        public override String ToString()
        {
            return String.Format("{0}.{1}", Major, Minor);
        }

        #endregion

    }

}
