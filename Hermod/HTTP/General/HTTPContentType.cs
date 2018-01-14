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

        private static readonly Char[]                                Splitter        = new Char[1] { ';' };

        private static readonly Dictionary<String, HTTPContentType>   _Lookup         = new Dictionary<String, HTTPContentType>();
        private static readonly Dictionary<String, HTTPContentType[]> _ReverseLookup  = new Dictionary<String, HTTPContentType[]>();

        #region Properties

        /// <summary>
        /// The media type.
        /// </summary>
        public String MediaType { get; }

        /// <summary>
        /// The prefix of the media type.
        /// </summary>
        public String MediaTypePrefix
            => MediaType.Split(new[] { '/' })[0];

        /// <summary>
        /// The media subtype.
        /// </summary>
        public String MediaSubType
            => MediaType.Split(new[] { '/' })[1];

        /// <summary>
        /// The character set.
        /// </summary>
        public String CharSet { get; }


        private readonly String[] _FileExtentions;

        /// <summary>
        /// Well-known file extentions using this HTTP content type.
        /// </summary>
        public IEnumerable<String> FileExtentions
            => _FileExtentions;

        /// <summary>
        /// The (optional) MIME boundary.
        /// </summary>
        public String MIMEBoundary { get; }

        /// <summary>
        /// The (optional) (SOAP) action.
        /// </summary>
        public String Action { get; }


        #region DebugView

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public String DebugView
            => ToString() +
               (FileExtentions != null & FileExtentions.Any() ? ", file extentions: " + FileExtentions.Aggregate((a, b) => a + ", " + b) : "");

        #endregion

        #endregion

        #region Constructor(s)

        #region HTTPContentType(MediaType, params FileExtentions)

        ///// <summary>
        ///// Creates a new HTTP content type based on the given media type
        ///// and file extentions.
        ///// </summary>
        ///// <param name="MediaType">The media type for the HTTP content type.</param>
        ///// <param name="FileExtentions">Well-known file extentions using this HTTP content type.</param>
        //public HTTPContentType(String           MediaType,
        //                       String           MIMEBoundary,
        //                       params String[]  FileExtentions)

        //    : this(MediaType,
        //           "utf-8",
        //           MIMEBoundary,
        //           FileExtentions)

        //{ }

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
                               String           CharSet,
                               String           Action,
                               String           MIMEBoundary,
                               params String[]  FileExtentions)
        {

            this.MediaType        = MediaType;
            this.CharSet          = CharSet        ?? "utf-8";
            this.Action           = Action;
            this.MIMEBoundary     = MIMEBoundary;
            this._FileExtentions  = FileExtentions ?? new String[0];

            if (!_Lookup.ContainsKey(MediaType))
                _Lookup.Add(MediaType, this);

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

        #region HTTPContentType(MediaType, CharSet, MIMEBoundary)

        ///// <summary>
        ///// Creates a new HTTP content type based on the given media type,
        ///// character set and file extentions.
        ///// </summary>
        ///// <param name="MediaType">The media type for the HTTP content type.</param>
        ///// <param name="CharSet">The char set of the HTTP content type.</param>
        ///// <param name="MIMEBoundary">The MIME boundary.</param>
        //public HTTPContentType(String  MediaType,
        //                       String  CharSet,
        //                       String  MIMEBoundary)
        //{

        //    this.MediaType        = MediaType;
        //    this.CharSet          = CharSet ?? "utf-8";
        //    this._FileExtentions  = new String[0];
        //    this.MIMEBoundary     = MIMEBoundary;

        //}

        #endregion

        #endregion


        #region Static HTTP content types

        public static readonly HTTPContentType ALL                  = new HTTPContentType("*/*",                                    "utf-8", null, null);

        public static readonly HTTPContentType TEXT_UTF8            = new HTTPContentType("text/plain",                             "utf-8", null, null, "txt");
        public static readonly HTTPContentType HTML_UTF8            = new HTTPContentType("text/html",                              "utf-8", null, null, "htm", "html");
        public static readonly HTTPContentType CSS_UTF8             = new HTTPContentType("text/css",                               "utf-8", null, null, "css");
        public static readonly HTTPContentType JAVASCRIPT_UTF8      = new HTTPContentType("text/javascript",                        "utf-8", null, null, "js");
        public static readonly HTTPContentType XMLTEXT_UTF8         = new HTTPContentType("text/xml",                               "utf-8", null, null, "xml");
        public static readonly HTTPContentType MARKDOWN_UTF8        = new HTTPContentType("text/markdown",                          "utf-8", null, null, "md");

        public static readonly HTTPContentType JSON_UTF8            = new HTTPContentType("application/json",                       "utf-8", null, null, "json");
        public static readonly HTTPContentType JSONLD_UTF8          = new HTTPContentType("application/ld+json",                    "utf-8", null, null, "json-ld");
        public static readonly HTTPContentType XML_UTF8             = new HTTPContentType("application/xml",                        "utf-8", null, null, "xml");
        public static readonly HTTPContentType SOAPXML_UTF8         = new HTTPContentType("application/soap+xml",                   "utf-8", null, null, "soap");

        public static readonly HTTPContentType GEXF_UTF8            = new HTTPContentType("application/gexf+xml",                   "utf-8", null, null, "gexf");
        public static readonly HTTPContentType GRAPHML_UTF8         = new HTTPContentType("application/graphml+xml",                "utf-8", null, null, "graphml");
        public static readonly HTTPContentType SWF                  = new HTTPContentType("application/x-shockwave-flash",          null,    null, null, "swf");
        public static readonly HTTPContentType PDF                  = new HTTPContentType("application/pdf",                        "utf-8", null, null, "pdf");
        public static readonly HTTPContentType CSV                  = new HTTPContentType("application/csv",                        "utf-8", null, null, "csv");
        public static readonly HTTPContentType SIG                  = new HTTPContentType("application/pgp-signature",              "utf-8", null, null, "sig");

        public static readonly HTTPContentType GIF                  = new HTTPContentType("image/gif",                              null,    null, null, "gif");
        public static readonly HTTPContentType ICO                  = new HTTPContentType("image/ico",                              null,    null, null, "ico");
        public static readonly HTTPContentType PNG                  = new HTTPContentType("image/png",                              null,    null, null, "png");
        public static readonly HTTPContentType JPEG                 = new HTTPContentType("image/jpeg",                             null,    null, null, "jpg", "jpeg");
        public static readonly HTTPContentType SVG                  = new HTTPContentType("image/svg+xml",                          "utf-8", null, null, "svg");
        public static readonly HTTPContentType WOFF                 = new HTTPContentType("application/font-woff",                  "utf-8", null, null, "woff", "woff2");

        public static readonly HTTPContentType XWWWFormUrlEncoded   = new HTTPContentType("application/x-www-form-urlencoded",      "utf-8", null, null);
        public static readonly HTTPContentType OCTETSTREAM          = new HTTPContentType("application/octet-stream",               "utf-8", null, null);
        public static readonly HTTPContentType EVENTSTREAM          = new HTTPContentType("text/event-stream",                      "utf-8", null, null);

        public static readonly HTTPContentType MULTIPART_FORMDATA   = new HTTPContentType("multipart/form-data",                    "utf-8", null, null);

        #endregion


        #region TryParseString(Text, out HTTPContentType)

        public static Boolean TryParseString(String Text, out HTTPContentType HTTPContentType)
        {

            var Parts = Text.Split(Splitter, StringSplitOptions.RemoveEmptyEntries).
                             SafeSelect(part => part.Trim()).
                             ToArray();

            var MediaType = Parts[0];
            var Charset   = Parts.FirstOrDefault(part => part.StartsWith("charset",  StringComparison.OrdinalIgnoreCase));
            var Boundary  = Parts.FirstOrDefault(part => part.StartsWith("boundary", StringComparison.OrdinalIgnoreCase));

            Charset = Charset.IsNeitherNullNorEmpty() ? Charset.Substring(Charset.IndexOf("=") + 1).Trim() : "utf-8";

            if (Boundary != null)
            {

                HTTPContentType = new HTTPContentType(MediaType,
                                                      Charset,
                                                      null,
                                                      Boundary.Substring(Boundary.IndexOf("----")));
                return true;

            }

            else
            {

                HTTPContentType = (from _FieldInfo in typeof(HTTPContentType).GetFields()
                                   let __HTTPContentType = _FieldInfo.GetValue(null) as HTTPContentType
                                   where __HTTPContentType != null
                                   where __HTTPContentType.MediaType == MediaType
                                   select __HTTPContentType).FirstOrDefault();

                if (HTTPContentType != null)
                    return true;

                return false;

            }

        }

        #endregion

        public static HTTPContentType ForMediaType(String                 MediaType,
                                                   Func<HTTPContentType>  DefaultValueFactory = null)
        {

            if (_Lookup.TryGetValue(MediaType, out HTTPContentType HTTPContentType))
                return HTTPContentType;

            if (DefaultValueFactory != null)
                return DefaultValueFactory();

            return ALL;

        }

        public static IEnumerable<HTTPContentType> ForFileExtention(String                 FileExtention,
                                                                    Func<HTTPContentType>  DefaultValueFactory = null)
        {

            if (_ReverseLookup.TryGetValue(FileExtention, out HTTPContentType[] HTTPContentTypes))
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
            => !(myHTTPContentType1 == myHTTPContentType2);

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
            => !(myHTTPContentType1 > myHTTPContentType2);

        #endregion

        #region Operator >= (myHTTPContentType1, myHTTPContentType2)

        public static Boolean operator >= (HTTPContentType myHTTPContentType1, HTTPContentType myHTTPContentType2)
            => !(myHTTPContentType1 < myHTTPContentType2);

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

            var HTTPContentType = Object as HTTPContentType;
            if ((Object) HTTPContentType == null)
                return false;

            return Equals(HTTPContentType);

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

            return MediaType.Equals(HTTPContentType.MediaType);// &&
                   //CharSet  .Equals(HTTPContentType.CharSet)   &&

                   //((MIMEBoundary == null && HTTPContentType.MIMEBoundary == null) ||
                   // (MIMEBoundary != null && HTTPContentType.MIMEBoundary != null && MIMEBoundary.Equals(HTTPContentType.MIMEBoundary)));

        }

        #endregion

        #region LesserEquals(HTTPContentType)

        /// <summary>
        /// Compares two HTTPContentTypes for lesser equality.
        /// </summary>
        /// <param name="HTTPContentType">A HTTPContentType to compare with.</param>
        /// <returns>True if both match; False otherwise.</returns>
        public Boolean LesserEquals(HTTPContentType HTTPContentType)
        {

            if ((Object) HTTPContentType == null)
                return false;

            return MediaType.Equals(HTTPContentType.MediaType);// &&
                   //CharSet  .Equals(HTTPContentType.CharSet);

        }

        #endregion

        #endregion

        #region GetHashCode()

        /// <summary>
        /// Get the HashCode of this object.
        /// </summary>
        public override Int32 GetHashCode()
        {
            unchecked
            {

                return MediaType.GetHashCode();// * 5 ^
                       //CharSet.  GetHashCode() * 3 ^

                       //(MIMEBoundary.IsNeitherNullNorEmpty()
                       //     ? MIMEBoundary.GetHashCode()
                       //     : 0);

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a string representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(MediaType,
                             "; charset=", CharSet.ToString(),

                             MIMEBoundary.IsNotNullOrEmpty()
                                 ? "; boundary = " + MIMEBoundary
                                 : "",

                             Action.IsNotNullOrEmpty()
                                 ? "; action = " + Action
                                 : "");

        #endregion

    }

}
