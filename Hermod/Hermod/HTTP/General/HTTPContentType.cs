/*
 * Copyright (c) 2010-2022 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

        private static readonly Dictionary<String, HTTPContentType>   _Lookup         = new();
        private static readonly Dictionary<String, HTTPContentType[]> _ReverseLookup  = new();

        #region Properties

        /// <summary>
        /// The media main type.
        /// </summary>
        public String  MediaMainType    { get; }

        /// <summary>
        /// The media sub type.
        /// </summary>
        public String  MediaSubType     { get; }

        /// <summary>
        /// The (optional) character set.
        /// </summary>
        public String  CharSet          { get; }


        private readonly String[] fileExtensions;

        /// <summary>
        /// Well-known file extentions using this HTTP content type.
        /// </summary>
        public IEnumerable<String> FileExtensions
            => fileExtensions;

        /// <summary>
        /// The (optional) MIME boundary.
        /// </summary>
        public String  MIMEBoundary     { get; }

        /// <summary>
        /// The (optional) (SOAP) action.
        /// </summary>
        public String  Action           { get; }


        #region DebugView

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String DebugView
            => ToString() +
               (FileExtensions != null & FileExtensions.Any() ? ", file extentions: " + FileExtensions.Aggregate((a, b) => a + ", " + b) : "");

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP content type based on the given media type,
        /// character set and file extentions.
        /// </summary>
        /// <param name="MediaMainType">The media main type for the HTTP content type.</param>
        /// <param name="MediaSubType">The media sub type for the HTTP content type.</param>
        /// <param name="CharSet">The char set of the HTTP content type.</param>
        /// <param name="FileExtensions">Well-known file extentions using this HTTP content type.</param>
        public HTTPContentType(String           MediaMainType,
                               String           MediaSubType,
                               String           CharSet,
                               String           Action,
                               String           MIMEBoundary,
                               params String[]  FileExtensions)
        {

            this.MediaMainType    = MediaMainType;
            this.MediaSubType     = MediaSubType;
            this.CharSet          = CharSet;
            this.Action           = Action;
            this.MIMEBoundary     = MIMEBoundary;
            this.fileExtensions  = FileExtensions ?? Array.Empty<String>();

            if (!_Lookup.ContainsKey(MediaMainType + "/" + MediaSubType))
                _Lookup.Add(MediaMainType + "/" + MediaSubType, this);

            if (fileExtensions.Any())
            {
                fileExtensions.ForEach(FileExtension => {
                    if (_ReverseLookup.ContainsKey(FileExtension)) {
                        var List = new List<HTTPContentType>(_ReverseLookup[FileExtension]) {
                                       this
                                   };
                        _ReverseLookup[FileExtension] = List.ToArray();
                    }
                    else
                        _ReverseLookup.Add(FileExtension, new HTTPContentType[] { this });
                });
            }

        }

        #endregion


        #region Static HTTP content types

        public static readonly HTTPContentType ALL                  = new HTTPContentType("*", "*",                                    "utf-8", null, null);

        public static readonly HTTPContentType TEXT_UTF8            = new HTTPContentType("text", "plain",                             "utf-8", null, null, "txt");
        public static readonly HTTPContentType HTML_UTF8            = new HTTPContentType("text", "html",                              "utf-8", null, null, "htm", "html");
        public static readonly HTTPContentType CSS_UTF8             = new HTTPContentType("text", "css",                               "utf-8", null, null, "css");
        public static readonly HTTPContentType CSV_Text_UTF8        = new HTTPContentType("text", "csv",                               "utf-8", null, null, "css");
        public static readonly HTTPContentType JAVASCRIPT_UTF8      = new HTTPContentType("text", "javascript",                        "utf-8", null, null, "js");
        public static readonly HTTPContentType XMLTEXT_UTF8         = new HTTPContentType("text", "xml",                               "utf-8", null, null, "xml");
        public static readonly HTTPContentType MARKDOWN_UTF8        = new HTTPContentType("text", "markdown",                          "utf-8", null, null, "md");

        public static readonly HTTPContentType JSON_UTF8            = new HTTPContentType("application", "json",                       "utf-8", null, null, "json");
        public static readonly HTTPContentType JSONLD_UTF8          = new HTTPContentType("application", "ld+json",                    "utf-8", null, null, "json-ld");
        public static readonly HTTPContentType GeoJSON_UTF8         = new HTTPContentType("application", "geo+json",                   "utf-8", null, null, "geojson");
        public static readonly HTTPContentType JSONMergePatch_UTF8  = new HTTPContentType("application", "merge-patch+json",           "utf-8", null, null);
        public static readonly HTTPContentType XML_UTF8             = new HTTPContentType("application", "xml",                        "utf-8", null, null, "xml");
        public static readonly HTTPContentType SOAPXML_UTF8         = new HTTPContentType("application", "soap+xml",                   "utf-8", null, null, "soap");

        public static readonly HTTPContentType GEXF_UTF8            = new HTTPContentType("application", "gexf+xml",                   "utf-8", null, null, "gexf");
        public static readonly HTTPContentType GRAPHML_UTF8         = new HTTPContentType("application", "graphml+xml",                "utf-8", null, null, "graphml");
        public static readonly HTTPContentType SWF                  = new HTTPContentType("application", "x-shockwave-flash",          null,    null, null, "swf");
        public static readonly HTTPContentType PDF                  = new HTTPContentType("application", "pdf",                        "utf-8", null, null, "pdf");
        public static readonly HTTPContentType CSV_App_UTF8         = new HTTPContentType("application", "csv",                        "utf-8", null, null, "csv");
        public static readonly HTTPContentType SIG                  = new HTTPContentType("application", "pgp-signature",              "utf-8", null, null, "sig");

        public static readonly HTTPContentType GIF                  = new HTTPContentType("image", "gif",                              null,    null, null, "gif");
        public static readonly HTTPContentType ICO                  = new HTTPContentType("image", "ico",                              null,    null, null, "ico");
        public static readonly HTTPContentType PNG                  = new HTTPContentType("image", "png",                              null,    null, null, "png");
        public static readonly HTTPContentType JPEG                 = new HTTPContentType("image", "jpeg",                             null,    null, null, "jpg", "jpeg");
        public static readonly HTTPContentType SVG                  = new HTTPContentType("image", "svg+xml",                          "utf-8", null, null, "svg");

        public static readonly HTTPContentType OGM                  = new HTTPContentType("video", "ogm",                              null,    null, null, "ogm");
        public static readonly HTTPContentType OGV                  = new HTTPContentType("video", "ogv",                              null,    null, null, "ogv");
        public static readonly HTTPContentType OGG                  = new HTTPContentType("video", "ogg",                              null,    null, null, "ogg");
        public static readonly HTTPContentType MP4                  = new HTTPContentType("video", "mp4",                              null,    null, null, "mp4");
        public static readonly HTTPContentType WEBM                 = new HTTPContentType("video", "webm",                             null,    null, null, "webm");

        public static readonly HTTPContentType WOFF                 = new HTTPContentType("application", "font-woff",                  "utf-8", null, null, "woff", "woff2");

        public static readonly HTTPContentType XWWWFormUrlEncoded   = new HTTPContentType("application", "x-www-form-urlencoded",      "utf-8", null, null);
        public static readonly HTTPContentType OCTETSTREAM          = new HTTPContentType("application", "octet-stream",               "utf-8", null, null);
        public static readonly HTTPContentType EVENTSTREAM          = new HTTPContentType("text", "event-stream",                      "utf-8", null, null);

        public static readonly HTTPContentType MULTIPART_FORMDATA   = new HTTPContentType("multipart", "form-data",                    "utf-8", null, null);

        #endregion


        #region TryParseString(Text, out HTTPContentType)

        public static Boolean TryParseString(String Text, out HTTPContentType HTTPContentType)
        {

            try
            {

                var Parts       = Text.Split     (Splitter, StringSplitOptions.RemoveEmptyEntries).
                                       SafeSelect(part => part.Trim()).
                                       ToArray();

                var MediaTypes  = Parts[0].Split('/');
                var Charset     = Array.Find(Parts, part => part.StartsWith("charset",  StringComparison.OrdinalIgnoreCase));
                var Boundary    = Array.Find(Parts, part => part.StartsWith("boundary", StringComparison.OrdinalIgnoreCase));

                    Charset     = Charset.IsNeitherNullNorEmpty()
                                      ? Charset.Substring(Charset.IndexOf("=") + 1).Trim()
                                      : null;

                if (Boundary != null)
                {

                    HTTPContentType = new HTTPContentType(MediaTypes[0],
                                                          MediaTypes[1],
                                                          Charset,
                                                          null,
                                                          Boundary.Substring(Boundary.IndexOf("----")));
                    return true;

                }

                else
                {

                    HTTPContentType  = (from    _FieldInfo in typeof(HTTPContentType).GetFields()
                                        let    __HTTPContentType  = _FieldInfo.GetValue(null) as HTTPContentType
                                        where  __HTTPContentType != null
                                        where  __HTTPContentType.MediaMainType == MediaTypes[0] && __HTTPContentType.MediaSubType == MediaTypes[1]
                                        select __HTTPContentType).FirstOrDefault()

                                    ??  new HTTPContentType(MediaTypes[0],
                                                            MediaTypes[1],
                                                            null,
                                                            null,
                                                            null);

                    return true;

                }

            }
            catch (Exception)
            {
                HTTPContentType = null;
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

        public static IEnumerable<HTTPContentType> ForFileExtension(String                 FileExtension,
                                                                    Func<HTTPContentType>  DefaultValueFactory = null)
        {

            if (_ReverseLookup.TryGetValue(FileExtension, out HTTPContentType[] HTTPContentTypes))
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

            var c = MediaMainType.CompareTo(HTTPContentType.MediaMainType);

            if (c == 0)
                return MediaSubType.CompareTo(HTTPContentType.MediaSubType);

            return c;

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

            return MediaMainType.Equals(HTTPContentType.MediaMainType) &&
                   MediaSubType. Equals(HTTPContentType.MediaSubType);

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

                return MediaMainType.GetHashCode() * 3 ^
                       MediaSubType. GetHashCode();

            }
        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(MediaMainType, "/", MediaSubType,
                             CharSet != null ? "; charset=" + CharSet : null,

                             MIMEBoundary.IsNotNullOrEmpty()
                                 ? "; boundary=\"" + MIMEBoundary + "\""
                                 : "",

                             Action.IsNotNullOrEmpty()
                                 ? "; action=\"" + Action + "\""
                                 : "");

        #endregion

    }

}
