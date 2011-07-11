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

    /// <summary>
    /// HTTP status codes as defined within RFC 2616 and other resources.
    /// </summary>
    public sealed class HTTPContentType : IComparable, IComparable<HTTPContentType>, IEquatable<HTTPContentType>
    {

        #region Properties

        #region MediaType

        private readonly String _MediaType;

        /// <summary>
        /// The code of this HTTP media type
        /// </summary>
        public String MediaType
        {
            get
            {
                return _MediaType;
            }
        }
        
        #endregion

        #region CharSet

        private readonly String _CharSet;
        
        /// <summary>
        /// The name of this HTTP content type
        /// </summary>
        public String CharSet
        {
            get
            {
                return _CharSet;
            }
        }

        #endregion

        #region Description

        private readonly String _Description;
        
        /// <summary>
        /// The description of this HTTP content type.
        /// </summary>
        public String Description
        {
            get
            {
                return _Description;
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPContentType(myMediaType, CharSet = "UTF-8", Description = null)

        /// <summary>
        /// Creates a new HTTP content type based on the given media type
        /// and optional a char set or description.
        /// </summary>
        /// <param name="myMediaType">The media type for the HTTP content type.</param>
        /// <param name="CharSet">The char set of the HTTP content type.</param>
        /// <param name="Description">A description of the HTTP content type.</param>
        public HTTPContentType(String myMediaType, String CharSet = "UTF-8", String Description = null)
        {
            _MediaType   = myMediaType;
            _CharSet     = CharSet;
            _Description = Description;
        }

        #endregion

        #endregion


        #region 1xx Informational

        //private static ContentType _JSON = new ContentType("application/json");
        //private static ContentType _XML = new ContentType("application/xml");
        //private static ContentType _XHTML = new ContentType("application/xhtml+xml");
        //private static ContentType _GEXF = new ContentType("application/gexf+xml");

        public static readonly HTTPContentType TEXT_UTF8       = new HTTPContentType("text/plain",             "UTF-8");
        public static readonly HTTPContentType HTML_UTF8       = new HTTPContentType("text/html",              "UTF-8");
        public static readonly HTTPContentType CSS_UTF8        = new HTTPContentType("text/css",               "UTF-8");
        public static readonly HTTPContentType JAVASCRIPT_UTF8 = new HTTPContentType("text/javascript",        "UTF-8");

        public static readonly HTTPContentType JSON_UTF8       = new HTTPContentType("application/json",       "UTF-8");
        public static readonly HTTPContentType XML_UTF8        = new HTTPContentType("application/xml",        "UTF-8");
        public static readonly HTTPContentType XHTML_UTF8      = new HTTPContentType("application/xhtml+xml",  "UTF-8");
        public static readonly HTTPContentType GEXF_UTF8       = new HTTPContentType("application/gexf+xml",   "UTF-8");
        public static readonly HTTPContentType FORM            = new HTTPContentType("application/x-www-form-urlencoded");
        public static readonly HTTPContentType SWF             = new HTTPContentType("application/x-shockwave-flash");
        public static readonly HTTPContentType PDF             = new HTTPContentType("application/pdf");
        public static readonly HTTPContentType OCTETSTREAM     = new HTTPContentType("application/octet-stream");

        public static readonly HTTPContentType GIF             = new HTTPContentType("image/gif");
        public static readonly HTTPContentType ICO             = new HTTPContentType("image/ico");
        public static readonly HTTPContentType PNG             = new HTTPContentType("image/png");
        public static readonly HTTPContentType JPG             = new HTTPContentType("image/jpg");
        public static readonly HTTPContentType JPEG            = new HTTPContentType("image/jpeg");

        #endregion


        #region Tools

        #region GetMediaType(this myContentType)

        /// <summary>
        /// Returns the mediatype without the subtype
        /// </summary>
        /// <param name="myContentType"></param>
        /// <returns></returns>
        public String GetMediaType()
        {
            return _MediaType.Split(new[] { '/' })[0];
        }

        #endregion

        #region GetMediaSubType(this myContentType)

        /// <summary>
        /// Returns the media subtype
        /// </summary>
        /// <param name="myContentType"></param>
        /// <returns></returns>
        public String GetMediaSubType()
        {
            return _MediaType.Split(new[] { '/' })[1];
        }

        #endregion

        #region TryParseString(myString, out myHTTPContentType)

        public static Boolean TryParseString(String myString, out HTTPContentType myHTTPContentType)
        {

            myHTTPContentType = (from   _FieldInfo in typeof(HTTPContentType).GetFields()
                                 let    _HTTPContentType = _FieldInfo.GetValue(null) as HTTPContentType
                                 where  _HTTPContentType != null
                                 where  _HTTPContentType.MediaType == myString
                                 select _HTTPContentType).FirstOrDefault();

            if (myHTTPContentType != null)
                return true;

            return false;

        }

        #endregion

        #endregion


        #region Operator overloading

        #region Operator == (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator == (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(myHTTPContentType1, myHTTPContentType2))
                return true;

            // If one is null, but not both, return false.
            if (((Object) myHTTPContentType1 == null) || ((Object) myHTTPContentType2 == null))
                return false;

            return myHTTPContentType1.Equals(myHTTPContentType2);

        }

        #endregion

        #region Operator != (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator != (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
        {
            return !(myHTTPContentType1 == myHTTPContentType2);
        }

        #endregion

        #region Operator <  (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator < (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
        {

            // Check if myHTTPContentType1 is null
            if ((Object) myHTTPContentType1 == null)
                throw new ArgumentNullException("Parameter myHTTPContentType1 must not be null!");

            // Check if myHTTPContentType2 is null
            if ((Object) myHTTPContentType2 == null)
                throw new ArgumentNullException("Parameter myHTTPContentType2 must not be null!");

            return myHTTPContentType1.CompareTo(myHTTPContentType2) < 0;

        }

        #endregion

        #region Operator >  (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator > (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
        {

            // Check if myHTTPContentType1 is null
            if ((Object) myHTTPContentType1 == null)
                throw new ArgumentNullException("Parameter myHTTPContentType1 must not be null!");

            // Check if myHTTPContentType2 is null
            if ((Object) myHTTPContentType2 == null)
                throw new ArgumentNullException("Parameter myHTTPContentType2 must not be null!");

            return myHTTPContentType1.CompareTo(myHTTPContentType2) > 0;

        }

        #endregion

        #region Operator <= (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator <= (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
        {
            return !(myHTTPContentType1 > myHTTPContentType2);
        }

        #endregion

        #region Operator >= (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator >= (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
        {
            return !(myHTTPContentType1 < myHTTPContentType2);
        }

        #endregion

        #endregion

        #region IComparable Members

        public Int32 CompareTo(Object myObject)
        {

            // Check if myObject is null
            if (myObject == null)
                throw new ArgumentNullException("myObject must not be null!");

            // Check if myObject can be casted to an HTTPContentType object
            var myHTTPContentType = myObject as HTTPContentType;
            if ((Object) myHTTPContentType == null)
                throw new ArgumentException("myObject is not of type HTTPContentType!");

            return CompareTo(myHTTPContentType);

        }

        #endregion

        #region IComparable<HTTPContentType> Members

        public Int32 CompareTo(HTTPContentType myHTTPContentType)
        {

            // Check if myHTTPContentType is null
            if (myHTTPContentType == null)
                throw new ArgumentNullException("myHTTPContentType must not be null!");

            return MediaType.CompareTo(myHTTPContentType.MediaType);

        }

        #endregion

        #region IEquatable<HTTPContentType> Members

        #region Equals(myObject)

        public override Boolean Equals(Object myObject)
        {

            // Check if myObject is null
            if (myObject == null)
                throw new ArgumentNullException("Parameter myObject must not be null!");

            // Check if myObject can be cast to HTTPContentType
            var myHTTPContentType = myObject as HTTPContentType;
            if ((Object) myHTTPContentType == null)
                throw new ArgumentException("Parameter myObject could not be casted to type HTTPContentType!");

            return this.Equals(myHTTPContentType);

        }

        #endregion

        #region Equals(myHTTPContentType)

        public Boolean Equals(HTTPContentType myHTTPContentType)
        {

            // Check if myHTTPContentType is null
            if (myHTTPContentType == null)
                throw new ArgumentNullException("Parameter myHTTPContentType must not be null!");

            return MediaType == myHTTPContentType.MediaType;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            return MediaType.GetHashCode() ^ CharSet.GetHashCode();
        }

        #endregion

        #region ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return String.Format("{0}; charset={1}", MediaType, CharSet);
        }

        #endregion

    }

}
