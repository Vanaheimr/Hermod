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

namespace de.ahzf.Hermod.HTTP.Common
{

    public class HTTPVersion : IComparable, IComparable<HTTPVersion>, IEquatable<HTTPVersion>
    {

        #region Properties

        #region Major

        private readonly UInt16 _Major;

        /// <summary>
        /// The major of this HTTP version
        /// </summary>
        public UInt16 Major
        {
            get
            {
                return _Major;
            }
        }
        
        #endregion

        #region Minor

        private readonly UInt16 _Minor;
        
        /// <summary>
        /// The minor of this HTTP version
        /// </summary>
        public UInt16 Minor
        {
            get
            {
                return _Minor;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPVersion(myMajor, myMinor)

        public HTTPVersion(UInt16 myMajor, UInt16 myMinor)
        {
            _Major = myMajor;
            _Minor = myMinor;
        }

        #endregion

        #endregion


        #region Static HTTP versions

        public static readonly HTTPVersion HTTPVersion10 = new HTTPVersion(1, 0);
        public static readonly HTTPVersion HTTPVersion11 = new HTTPVersion(1, 1);

        #endregion


        #region Tools

        #region TryParseString(myVersionString, out myHTTPVersion)

        /// <summary>
        /// Tries to find the apropriate HTTPVersion for the given string, e.g. "HTTP/1.1".
        /// </summary>
        /// <param name="myVersionString">A HTTP version as stirng, e.g. HTTP/1.1</param>
        /// <param name="myHTTPStatusCode">The parsed HTTP version</param>
        /// <returns>true or false</returns>
        public static Boolean TryParseString(String myVersionString, out HTTPVersion myHTTPVersion)
        {

            myHTTPVersion = null;

            if (!myVersionString.StartsWith("HTTP/"))
                return false;

            var _MajorMinor = myVersionString.Substring(5).Split(new Char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            if (_MajorMinor.Length != 2)
                return false;

            UInt16 __Major;
            UInt16 __Minor;

            if (!UInt16.TryParse(_MajorMinor[0], out __Major))
                return false;

            if (!UInt16.TryParse(_MajorMinor[1], out __Minor))
                return false;

            myHTTPVersion = (from _FieldInfo in typeof(HTTPVersion).GetFields()
                             let    _HTTPVersion = _FieldInfo.GetValue(null) as HTTPVersion
                             where  _HTTPVersion != null
                             where  _HTTPVersion.Major == __Major
                             where  _HTTPVersion.Minor == __Minor
                             select _HTTPVersion).FirstOrDefault();

            if (myHTTPVersion != null)
                return true;

            return false;

        }

        #endregion

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

        #region IComparable Members

        public Int32 CompareTo(Object myObject)
        {

            // Check if myObject is null
            if (myObject == null)
                throw new ArgumentNullException("myObject must not be null!");

            // Check if myObject can be casted to an HTTPVersion object
            var myHTTPVersion = myObject as HTTPVersion;
            if ((Object) myHTTPVersion == null)
                throw new ArgumentException("myObject is not of type HTTPVersion!");

            return CompareTo(myHTTPVersion);

        }

        #endregion

        #region IComparable<HTTPVersion> Members

        public Int32 CompareTo(HTTPVersion myHTTPVersion)
        {

            // Check if myHTTPVersion is null
            if (myHTTPVersion == null)
                throw new ArgumentNullException("myHTTPVersion must not be null!");

            var _MajorCompared = Major.CompareTo(myHTTPVersion.Major);

            if (_MajorCompared != 0)
                return _MajorCompared;

            return Minor.CompareTo(myHTTPVersion.Minor);

        }

        #endregion

        #region IEquatable<HTTPVersion> Members

        #region Equals(myObject)

        public override Boolean Equals(Object myObject)
        {

            // Check if myObject is null
            if (myObject == null)
                throw new ArgumentNullException("Parameter myObject must not be null!");

            // Check if myObject can be cast to HTTPVersion
            var myHTTPVersion = myObject as HTTPVersion;
            if ((Object) myHTTPVersion == null)
                throw new ArgumentException("Parameter myObject could not be casted to type HTTPVersion!");

            return this.Equals(myHTTPVersion);

        }

        #endregion

        #region Equals(myHTTPVersion)

        public Boolean Equals(HTTPVersion myHTTPVersion)
        {

            // Check if myHTTPVersion is null
            if (myHTTPVersion == null)
                throw new ArgumentNullException("Parameter myHTTPVersion must not be null!");

            if (Major != myHTTPVersion.Major)
                return false;

            if (Minor != myHTTPVersion.Minor)
                return false;

            return true;

        }

        #endregion

        #endregion

        #region GetHashCode()

        public override Int32 GetHashCode()
        {
            return Major + Minor;
        }

        #endregion

        #region ToString()

        public override String ToString()
        {
            return String.Format("HTTP/{0}.{1}", Major, Minor);
        }

        #endregion

    }

}
