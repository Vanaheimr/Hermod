/*
 * Copyright (c) 2010-2016, Achim 'ahzf' Friedland <achim@graphdefined.org>
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
using System.Collections.Generic;
using System.Linq;

using org.GraphDefined.Vanaheimr.Illias;
using System.Diagnostics;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    public enum CharSetType
    {
        UTF8
    }

    /// <summary>
    /// HTTP content type.
    /// </summary>
    [DebuggerDisplay("{DebugView}")]
    public sealed class HTTPContentType : IEquatable<HTTPContentType>,
                                          IComparable<HTTPContentType>,
                                          IComparable
    {

        private static readonly Dictionary<String, HTTPContentType>   _Lookup         = new Dictionary<String, HTTPContentType>();
        private static readonly Dictionary<String, HTTPContentType[]> _ReverseLookup  = new Dictionary<String, HTTPContentType[]>();

        #region Properties

        #region MediaType

        private readonly String _MediaType;

        /// <summary>
        /// The media type.
        /// </summary>
        public String MediaType
        {
            get
            {
                return _MediaType;
            }
        }

        #endregion

        #region MediaTypePrefix

        /// <summary>
        /// The prefix of the media type.
        /// </summary>
        public String MediaTypePrefix
        {
            get
            {
                return MediaType.Split(new[] { '/' })[0];
            }
        }

        #endregion

        #region MediaSubType

        /// <summary>
        /// The media subtype.
        /// </summary>
        public String MediaSubType
        {
            get
            {
                return MediaType.Split(new[] { '/' })[1];
            }
        }

        #endregion

        #region CharSet

        private readonly CharSetType _CharSet;

        /// <summary>
        /// The character set.
        /// </summary>
        public CharSetType CharSet
        {
            get
            {
                return _CharSet;
            }
        }

        #endregion

        #region FileExtentions

        private readonly String[] _FileExtentions;

        /// <summary>
        /// Well-known file extentions using this HTTP content type.
        /// </summary>
        public String[] FileExtentions
        {
            get
            {
                return _FileExtentions;
            }
        }

        #endregion

        #region DebugView

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public String DebugView
        {
            get
            {
                return String.Concat(MediaType, ", charset=", CharSet, (FileExtentions != null & FileExtentions.Any()) ? ", file extentions: " + FileExtentions.Aggregate((a, b) => a + ", " + b) : "");
            }
        }

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPContentType(MediaType, params FileExtentions)

        /// <summary>
        /// Creates a new HTTP content type based on the given media type
        /// and file extentions.
        /// </summary>
        /// <param name="MediaType">The media type for the HTTP content type.</param>
        /// <param name="FileExtentions">Well-known file extentions using this HTTP content type.</param>
        public HTTPContentType(String           MediaType,
                               params String[]  FileExtentions)

            : this(MediaType, CharSetType.UTF8, FileExtentions)

        { }

        #endregion

        #region HTTPContentType(MediaType, CharSet = CharSetType.UTF8, params FileExtentions)

        /// <summary>
        /// Creates a new HTTP content type based on the given media type,
        /// character set and file extentions.
        /// </summary>
        /// <param name="MediaType">The media type for the HTTP content type.</param>
        /// <param name="CharSet">The char set of the HTTP content type.</param>
        /// <param name="FileExtentions">Well-known file extentions using this HTTP content type.</param>
        public HTTPContentType(String           MediaType,
                               CharSetType      CharSet  = CharSetType.UTF8,
                               params String[]  FileExtentions)
        {

            this._MediaType       = MediaType;
            this._CharSet         = CharSet;
            this._FileExtentions  = FileExtentions;

            if (!_Lookup.ContainsKey(MediaType))
                _Lookup.Add(MediaType, this);
            else
                throw new ArgumentException("");

            if (_FileExtentions != null && _FileExtentions.Any())
            {
                _FileExtentions.ForEach(FileExtention => {
                    if (_ReverseLookup.ContainsKey(FileExtention)) {
                        var List = new List<HTTPContentType>(_ReverseLookup[FileExtention]);
                        List.Add(this);
                        _ReverseLookup[FileExtention] = List.ToArray();
                    }
                    else
                        _ReverseLookup.Add(FileExtention, new HTTPContentType[] { this });
                });
            }
        }

        #endregion

        #endregion


        #region Static HTTP content types

        public static readonly HTTPContentType ALL                  = new HTTPContentType("*/*");

        public static readonly HTTPContentType TEXT_UTF8            = new HTTPContentType("text/plain",                             "txt");
        public static readonly HTTPContentType HTML_UTF8            = new HTTPContentType("text/html",                              "htm", "html");
        public static readonly HTTPContentType CSS_UTF8             = new HTTPContentType("text/css",                               "css");
        public static readonly HTTPContentType JAVASCRIPT_UTF8      = new HTTPContentType("text/javascript",                        "js");
        public static readonly HTTPContentType XMLTEXT_UTF8         = new HTTPContentType("text/xml",                               "xml");

        public static readonly HTTPContentType JSON_UTF8            = new HTTPContentType("application/json",                       "json");
        public static readonly HTTPContentType JSONLD_UTF8          = new HTTPContentType("application/ld+json",                    "json-ld");
        public static readonly HTTPContentType XML_UTF8             = new HTTPContentType("application/xml",                        "xml");
        public static readonly HTTPContentType GEXF_UTF8            = new HTTPContentType("application/gexf+xml",                   "gexf");
        public static readonly HTTPContentType GRAPHML_UTF8         = new HTTPContentType("application/graphml+xml",                "graphml");
        public static readonly HTTPContentType SWF                  = new HTTPContentType("application/x-shockwave-flash",          "swf");
        public static readonly HTTPContentType PDF                  = new HTTPContentType("application/pdf",                        "pdf");

        public static readonly HTTPContentType GIF                  = new HTTPContentType("image/gif",                              "gif");
        public static readonly HTTPContentType ICO                  = new HTTPContentType("image/ico",                              "ico");
        public static readonly HTTPContentType PNG                  = new HTTPContentType("image/png",                              "png");
        public static readonly HTTPContentType JPEG                 = new HTTPContentType("image/jpeg",                             "jpg", "jpeg");
        public static readonly HTTPContentType SVG                  = new HTTPContentType("image/svg+xml",                          "svg");

        public static readonly HTTPContentType XWWWFormUrlEncoded   = new HTTPContentType("application/x-www-form-urlencoded");
        public static readonly HTTPContentType OCTETSTREAM          = new HTTPContentType("application/octet-stream");
        public static readonly HTTPContentType EVENTSTREAM          = new HTTPContentType("text/event-stream");

        #endregion


        #region TryParseString(Text, out HTTPContentType)

        public static Boolean TryParseString(String Text, out HTTPContentType HTTPContentType)
        {

            var Split = Text.Split(new String[1] { ";" }, StringSplitOptions.None);

            var MediaType = Split[0].Trim();
            var CharSet   = (Split.Length > 1) ? Split[1].Trim() : "UTF-8";

            HTTPContentType = (from   _FieldInfo in typeof(HTTPContentType).GetFields()
                               let    __HTTPContentType = _FieldInfo.GetValue(null) as HTTPContentType
                               where  __HTTPContentType != null
                               where  __HTTPContentType.MediaType == MediaType
                               select __HTTPContentType).FirstOrDefault();

            if (HTTPContentType != null)
                return true;

            return false;

        }

        #endregion

        public static HTTPContentType ForMediaType(String                 MediaType,
                                                   Func<HTTPContentType>  DefaultValueFactory = null)
        {

            HTTPContentType HTTPContentType = null;

            if (_Lookup.TryGetValue(MediaType, out HTTPContentType))
                return HTTPContentType;

            if (DefaultValueFactory != null)
                return DefaultValueFactory();

            return HTTPContentType.ALL;

        }

        public static IEnumerable<HTTPContentType> ForFileExtention(String                 FileExtention,
                                                                    Func<HTTPContentType>  DefaultValueFactory = null)
        {

            HTTPContentType[] HTTPContentTypes = null;

            if (_ReverseLookup.TryGetValue(FileExtention, out HTTPContentTypes))
                return HTTPContentTypes;

            if (DefaultValueFactory != null)
                return new HTTPContentType[] { DefaultValueFactory() };

            return new HTTPContentType[0];

        }


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

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()
        {
            return String.Concat(MediaType, "; charset=", CharSet.ToString().Replace("UTF8", "utf-8"));
        }

        #endregion

    }

}
