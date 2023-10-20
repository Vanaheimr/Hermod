/*
 * Copyright (c) 2010-2023 GraphDefined GmbH
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

using System.Diagnostics;

using org.GraphDefined.Vanaheimr.Illias;

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

        #region Data

        private static readonly  Char[]                                 splitter        = new[] { ';' };

        private static readonly  Dictionary<String, HTTPContentType>    lookup          = new ();
        private static readonly  Dictionary<String, HTTPContentType[]>  reverseLookup   = new ();

        private        readonly  String[] fileExtensions;

        #endregion

        #region Properties

        /// <summary>
        /// The media main type.
        /// </summary>
        public String               MediaMainType    { get; }

        /// <summary>
        /// The media sub type.
        /// </summary>
        public String               MediaSubType     { get; }

        /// <summary>
        /// The optional character set.
        /// </summary>
        public String?              CharSet          { get; }

        /// <summary>
        /// Well-known file extensions using this HTTP content type.
        /// </summary>
        public IEnumerable<String>  FileExtensions
            => fileExtensions;

        /// <summary>
        /// The (optional) MIME boundary.
        /// </summary>
        public String?              MIMEBoundary     { get; }

        /// <summary>
        /// The (optional) (SOAP) action.
        /// </summary>
        public String?              Action           { get; }


        #region DebugView

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String DebugView
            => ToString() +
               (FileExtensions != null & FileExtensions.Any() ? ", file extensions: " + FileExtensions.Aggregate((a, b) => a + ", " + b) : "");

        #endregion

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new HTTP content type based on the given media type,
        /// character set and file extensions.
        /// </summary>
        /// <param name="MediaMainType">The media main type for the HTTP content type.</param>
        /// <param name="MediaSubType">The media sub type for the HTTP content type.</param>
        /// <param name="CharSet">The char set of the HTTP content type.</param>
        /// <param name="FileExtensions">Well-known file extensions using this HTTP content type.</param>
        public HTTPContentType(String           MediaMainType,
                               String           MediaSubType,
                               String?          CharSet,
                               String?          Action,
                               String?          MIMEBoundary,
                               params String[]  FileExtensions)
        {

            this.MediaMainType    = MediaMainType;
            this.MediaSubType     = MediaSubType;
            this.CharSet          = CharSet;
            this.Action           = Action;
            this.MIMEBoundary     = MIMEBoundary;
            this.fileExtensions   = FileExtensions ?? Array.Empty<String>();

            if (!lookup.ContainsKey(MediaMainType + "/" + MediaSubType))
                lookup.Add(MediaMainType + "/" + MediaSubType, this);

            if (fileExtensions.Any())
            {
                fileExtensions.ForEach(FileExtension => {
                    if (reverseLookup.ContainsKey(FileExtension)) {
                        var List = new List<HTTPContentType>(reverseLookup[FileExtension]) {
                                       this
                                   };
                        reverseLookup[FileExtension] = List.ToArray();
                    }
                    else
                        reverseLookup.Add(FileExtension, new HTTPContentType[] { this });
                });
            }

        }

        #endregion


        #region Static HTTP content types

        public static readonly HTTPContentType ALL                  = new ("*", "*",                                    "utf-8", null, null);

        public static readonly HTTPContentType TEXT_UTF8            = new ("text", "plain",                             "utf-8", null, null, "txt");
        public static readonly HTTPContentType HTML_UTF8            = new ("text", "html",                              "utf-8", null, null, "htm", "html");
        public static readonly HTTPContentType CSS_UTF8             = new ("text", "css",                               "utf-8", null, null, "css");
        public static readonly HTTPContentType CSV_Text_UTF8        = new ("text", "csv",                               "utf-8", null, null, "css");
        public static readonly HTTPContentType JAVASCRIPT_UTF8      = new ("text", "javascript",                        "utf-8", null, null, "js");
        public static readonly HTTPContentType XMLTEXT_UTF8         = new ("text", "xml",                               "utf-8", null, null, "xml");
        public static readonly HTTPContentType MARKDOWN_UTF8        = new ("text", "markdown",                          "utf-8", null, null, "md");

        public static readonly HTTPContentType JSON_UTF8            = new ("application", "json",                       "utf-8", null, null, "json");
        public static readonly HTTPContentType JSONLD_UTF8          = new ("application", "ld+json",                    "utf-8", null, null, "json-ld");
        public static readonly HTTPContentType GeoJSON_UTF8         = new ("application", "geo+json",                   "utf-8", null, null, "geojson");
        public static readonly HTTPContentType JSONMergePatch_UTF8  = new ("application", "merge-patch+json",           "utf-8", null, null);
        public static readonly HTTPContentType XML_UTF8             = new ("application", "xml",                        "utf-8", null, null, "xml");
        public static readonly HTTPContentType SOAPXML_UTF8         = new ("application", "soap+xml",                   "utf-8", null, null, "soap");

        public static readonly HTTPContentType GEXF_UTF8            = new ("application", "gexf+xml",                   "utf-8", null, null, "gexf");
        public static readonly HTTPContentType GRAPHML_UTF8         = new ("application", "graphml+xml",                "utf-8", null, null, "graphml");
        public static readonly HTTPContentType SWF                  = new ("application", "x-shockwave-flash",          null,    null, null, "swf");
        public static readonly HTTPContentType PDF                  = new ("application", "pdf",                        "utf-8", null, null, "pdf");
        public static readonly HTTPContentType CSV_App_UTF8         = new ("application", "csv",                        "utf-8", null, null, "csv");
        public static readonly HTTPContentType SIG                  = new ("application", "pgp-signature",              "utf-8", null, null, "sig");

        public static readonly HTTPContentType GIF                  = new ("image", "gif",                              null,    null, null, "gif");
        public static readonly HTTPContentType ICO                  = new ("image", "ico",                              null,    null, null, "ico");
        public static readonly HTTPContentType PNG                  = new ("image", "png",                              null,    null, null, "png");
        public static readonly HTTPContentType JPEG                 = new ("image", "jpeg",                             null,    null, null, "jpg", "jpeg");
        public static readonly HTTPContentType SVG                  = new ("image", "svg+xml",                          "utf-8", null, null, "svg");

        public static readonly HTTPContentType OGM                  = new ("video", "ogm",                              null,    null, null, "ogm");
        public static readonly HTTPContentType OGV                  = new ("video", "ogv",                              null,    null, null, "ogv");
        public static readonly HTTPContentType OGG                  = new ("video", "ogg",                              null,    null, null, "ogg");
        public static readonly HTTPContentType MP4                  = new ("video", "mp4",                              null,    null, null, "mp4");
        public static readonly HTTPContentType WEBM                 = new ("video", "webm",                             null,    null, null, "webm");

        public static readonly HTTPContentType WOFF                 = new ("application", "font-woff",                  "utf-8", null, null, "woff", "woff2");

        public static readonly HTTPContentType XWWWFormUrlEncoded   = new ("application", "x-www-form-urlencoded",      "utf-8", null, null);
        public static readonly HTTPContentType OCTETSTREAM          = new ("application", "octet-stream",               "utf-8", null, null);
        public static readonly HTTPContentType EVENTSTREAM          = new ("text", "event-stream",                      "utf-8", null, null);

        public static readonly HTTPContentType MULTIPART_FORMDATA   = new ("multipart", "form-data",                    "utf-8", null, null);

        #endregion


        #region TryParse(Text, out HTTPContentType)

        public static Boolean TryParse(String Text, out HTTPContentType? HTTPContentType)
        {

            try
            {

                var parts       = Text.Split     (splitter, StringSplitOptions.RemoveEmptyEntries).
                                       SafeSelect(part => part.Trim()).
                                       ToArray();

                var mediaTypes  = parts[0].Split('/');
                var charset     = Array.Find(parts, part => part.StartsWith("charset",  StringComparison.OrdinalIgnoreCase));
                var boundary    = Array.Find(parts, part => part.StartsWith("boundary", StringComparison.OrdinalIgnoreCase));

                    charset     = charset is not null && charset.IsNeitherNullNorEmpty()
                                      ? charset.Substring(charset.IndexOf("=") + 1).Trim()
                                      : null;

                if (boundary is not null)
                {

                    HTTPContentType = new HTTPContentType(mediaTypes[0],
                                                          mediaTypes[1],
                                                          charset,
                                                          null,
                                                          boundary[boundary.IndexOf("----")..]);
                    return true;

                }

                else
                {

                    HTTPContentType  = (from    fieldInfo in typeof(HTTPContentType).GetFields()
                                        let    httpContentType  = fieldInfo.GetValue(null) as HTTPContentType
                                        where  httpContentType is not null
                                        where  httpContentType.MediaMainType == mediaTypes[0] && httpContentType.MediaSubType == mediaTypes[1]
                                        select httpContentType).FirstOrDefault()

                                    ??  new HTTPContentType(mediaTypes[0],
                                                            mediaTypes[1],
                                                            null,
                                                            null,
                                                            null);

                    return true;

                }

            }
            catch
            {
                HTTPContentType = null;
                return false;
            }

        }

        #endregion

        #region ForMediaType(MediaType, DefaultValueFactory = null)

        public static HTTPContentType? ForMediaType(String                  MediaType,
                                                    Func<HTTPContentType>?  DefaultValueFactory   = null)
        {

            if (lookup.TryGetValue(MediaType, out var HTTPContentType))
                return HTTPContentType;

            if (DefaultValueFactory != null)
                return DefaultValueFactory();

            return ALL;

        }

        #endregion

        #region ForFileExtension(FileExtension, DefaultValueFactory = null)

        public static IEnumerable<HTTPContentType> ForFileExtension(String                  FileExtension,
                                                                    Func<HTTPContentType>?  DefaultValueFactory   = null)
        {

            if (reverseLookup.TryGetValue(FileExtension, out var httpContentTypes))
                return httpContentTypes;

            if (DefaultValueFactory is not null)
                return new[] { DefaultValueFactory() };

            return Array.Empty<HTTPContentType>();

        }

        #endregion


        #region Operator overloading

        #region Operator == (HTTPContentType1, HTTPContentType2)

        public static Boolean operator == (HTTPContentType? HTTPContentType1,
                                           HTTPContentType? HTTPContentType2)
        {

            // If both are null, or both are same instance, return true.
            if (Object.ReferenceEquals(HTTPContentType1, HTTPContentType2))
                return true;

            // If one is null, but not both, return false.
            if (HTTPContentType1 is null || HTTPContentType2 is null)
                return false;

            return HTTPContentType1.Equals(HTTPContentType2);

        }

        #endregion

        #region Operator != (HTTPContentType1, HTTPContentType2)

        public static Boolean operator != (HTTPContentType? HTTPContentType1,
                                           HTTPContentType? HTTPContentType2)

            => !(HTTPContentType1 == HTTPContentType2);

        #endregion

        #region Operator <  (HTTPContentType1, HTTPContentType2)

        public static Boolean operator < (HTTPContentType? HTTPContentType1,
                                          HTTPContentType? HTTPContentType2)
        {

            if (HTTPContentType1 is null)
                throw new ArgumentNullException(nameof(HTTPContentType1), "The given HTTPContentType1 must not be null!");

            if (HTTPContentType2 is null)
                throw new ArgumentNullException(nameof(HTTPContentType2), "The given HTTPContentType2 must not be null!");

            return HTTPContentType1.CompareTo(HTTPContentType2) < 0;

        }

        #endregion

        #region Operator >  (HTTPContentType1, HTTPContentType2)

        public static Boolean operator > (HTTPContentType? HTTPContentType1,
                                          HTTPContentType? HTTPContentType2)
        {

            if (HTTPContentType1 is null)
                throw new ArgumentNullException(nameof(HTTPContentType1), "The given HTTPContentType1 must not be null!");

            if (HTTPContentType2 is null)
                throw new ArgumentNullException(nameof(HTTPContentType2), "The given HTTPContentType2 must not be null!");

            return HTTPContentType1.CompareTo(HTTPContentType2) > 0;

        }

        #endregion

        #region Operator <= (HTTPContentType1, HTTPContentType2)

        public static Boolean operator <= (HTTPContentType? HTTPContentType1,
                                           HTTPContentType? HTTPContentType2)

            => !(HTTPContentType1 > HTTPContentType2);

        #endregion

        #region Operator >= (HTTPContentType1, HTTPContentType2)

        public static Boolean operator >= (HTTPContentType? HTTPContentType1,
                                           HTTPContentType? HTTPContentType2)

            => !(HTTPContentType1 < HTTPContentType2);

        #endregion

        #endregion

        #region IComparable<HTTPContentType> Members

        #region CompareTo(Object)

        /// <summary>
        /// Compares two HTTP content types.
        /// </summary>
        /// <param name="Object">A HTTP content type to compare with.</param>
        public Int32 CompareTo(Object? Object)

            => Object is HTTPContentType httpContentType
                   ? CompareTo(httpContentType)
                   : throw new ArgumentException("The given object is not a HTTP content type!",
                                                 nameof(Object));

        #endregion

        #region CompareTo(HTTPContentType)

        /// <summary>
        /// Compares two HTTP content types.
        /// </summary>
        /// <param name="HTTPContentType">A HTTP content type to compare with.</param>
        public Int32 CompareTo(HTTPContentType? HTTPContentType)
        {

            if (HTTPContentType is null)
                throw new ArgumentNullException(nameof(HTTPContentType),
                                                "The given HTTP content type must not be null!");

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
        /// Compares two HTTP content types for equality.
        /// </summary>
        /// <param name="HTTPContentType">A HTTP content type to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPContentType httpContentType &&
                   Equals(httpContentType);

        #endregion

        #region Equals(HTTPContentType)

        /// <summary>
        /// Compares two HTTP content types for equality.
        /// </summary>
        /// <param name="HTTPContentType">A HTTP content type to compare with.</param>
        public Boolean Equals(HTTPContentType? HTTPContentType)

            => HTTPContentType is not null &&

               MediaMainType.Equals(HTTPContentType.MediaMainType) &&
               MediaSubType. Equals(HTTPContentType.MediaSubType);

        #endregion

        #endregion

        #region (override) GetHashCode()

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
