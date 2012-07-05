/*
 * Copyright (c) 2010-2012, Achim 'ahzf' Friedland <achim@graph-database.org>
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
    /// HTTP content type.
    /// </summary>
    public sealed class HTTPContentType : IComparable, IComparable<HTTPContentType>, IEquatable<HTTPContentType>
    {

        #region Properties

        /// <summary>
        /// The code of this HTTP media type
        /// </summary>
        public String MediaType   { get; private set; }
        
        /// <summary>
        /// The name of this HTTP content type
        /// </summary>
        public String CharSet     { get; private set; }

        /// <summary>
        /// The description of this HTTP content type.
        /// </summary>
        public String Description { get; private set; }

        #endregion

        #region Constructor(s)

        #region HTTPContentType(MediaType, CharSet = "UTF-8", Description = null)

        /// <summary>
        /// Creates a new HTTP content type based on the given media type
        /// and optional a char set or description.
        /// </summary>
        /// <param name="MediaType">The media type for the HTTP content type.</param>
        /// <param name="CharSet">The char set of the HTTP content type.</param>
        /// <param name="Description">A description of the HTTP content type.</param>
        public HTTPContentType(String MediaType, String CharSet = "UTF-8", String Description = null)
        {
            this.MediaType   = MediaType;
            this.CharSet     = CharSet;
            this.Description = Description;
        }

        #endregion

        #endregion


        #region Static HTTP content types

        public static readonly HTTPContentType ALL             = new HTTPContentType("*/*",                     "UTF-8");

        public static readonly HTTPContentType TEXT_UTF8       = new HTTPContentType("text/plain",              "UTF-8");
        public static readonly HTTPContentType HTML_UTF8       = new HTTPContentType("text/html",               "UTF-8");
        public static readonly HTTPContentType CSS_UTF8        = new HTTPContentType("text/css",                "UTF-8");
        public static readonly HTTPContentType JAVASCRIPT_UTF8 = new HTTPContentType("text/javascript",         "UTF-8");

        public static readonly HTTPContentType JSON_UTF8       = new HTTPContentType("application/json",        "UTF-8");
        public static readonly HTTPContentType XML_UTF8        = new HTTPContentType("application/xml",         "UTF-8");
        public static readonly HTTPContentType GEXF_UTF8       = new HTTPContentType("application/gexf+xml",    "UTF-8");
        public static readonly HTTPContentType GRAPHML_UTF8    = new HTTPContentType("application/graphml+xml", "UTF-8");
        public static readonly HTTPContentType FORM            = new HTTPContentType("application/x-www-form-urlencoded");
        public static readonly HTTPContentType SWF             = new HTTPContentType("application/x-shockwave-flash");
        public static readonly HTTPContentType PDF             = new HTTPContentType("application/pdf");
        public static readonly HTTPContentType OCTETSTREAM     = new HTTPContentType("application/octet-stream");
        public static readonly HTTPContentType EVENTSTREAM     = new HTTPContentType("text/event-stream",       "UTF-8");

        public static readonly HTTPContentType GIF             = new HTTPContentType("image/gif");
        public static readonly HTTPContentType ICO             = new HTTPContentType("image/ico");
        public static readonly HTTPContentType PNG             = new HTTPContentType("image/png");
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
            return MediaType.Split(new[] { '/' })[0];
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
            return MediaType.Split(new[] { '/' })[1];
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

        #region IComparable<HTTPContentType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="Object">An object to compare with.</param>
        public Int32 CompareTo(Object Object)
        {

            if (Object == null)
                throw new ArgumentNullException("The given object must not be null!");

            // Check if the given object is an HTTPContentType.
            var HTTPContentType = Object as HTTPContentType;
            if ((Object) HTTPContentType == null)
                throw new ArgumentException("The given object is not a HTTPContentType!");

            return CompareTo(HTTPContentType);

        }

        #endregion

        #region CompareTo(HTTPContentType)

        /// <summary>
        /// Compares two instances of this object.
        /// </summary>
        /// <param name="HTTPContentType">An object to compare with.</param>
        public Int32 CompareTo(HTTPContentType HTTPContentType)
        {

            if ((Object) HTTPContentType == null)
                throw new ArgumentNullException("The given HTTPContentType must not be null!");

            return MediaType.CompareTo(HTTPContentType.MediaType);

        }

        #endregion

        #endregion

        #region IEquatable<HTTPContentType> Members

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

            // Check if the given object is an HTTPContentType.
            var HTTPContentType = Object as HTTPContentType;
            if ((Object) HTTPContentType == null)
                return false;

            return this.Equals(HTTPContentType);

        }

        #endregion

        #region Equals(HTTPContentType)

        /// <summary>
        /// Compares two HTTPContentTypes for equality.
        /// </summary>
        /// <param name="HTTPContentType">A HTTPContentType to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean Equals(HTTPContentType HTTPContentType)
        {

            if ((Object) HTTPContentType == null)
                return false;

            return MediaType == HTTPContentType.MediaType;

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            return MediaType.GetHashCode();// ^ CharSet.GetHashCode();
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
