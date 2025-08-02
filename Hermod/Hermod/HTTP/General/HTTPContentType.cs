/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Reflection;
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
    /// An HTTP content type.
    /// </summary>
    /// <param name="MediaMainType">The media main type for the HTTP content type.</param>
    /// <param name="MediaSubType">The media sub type for the HTTP content type.</param>
    /// <param name="CharSet">The char set of the HTTP content type.</param>
    /// <param name="FileExtensions">Well-known file extensions using this HTTP content type.</param>
    [DebuggerDisplay("{DebugView}")]
    public sealed class HTTPContentType(String                      MediaMainType,
                                        String                      MediaSubType,
                                        String?                     CharSet,
                                        String?                     Action,
                                        String?                     MIMEBoundary,
                                        params IEnumerable<String>  FileExtensions) : IEquatable<HTTPContentType>,
                                                                                      IComparable<HTTPContentType>,
                                                                                      IComparable
    {

        #region Data

        private static readonly  Char[]                                 splitter              = [';'];

        private static readonly  Dictionary<String, HTTPContentType>    lookup                = [];
        private static readonly  Dictionary<String, HTTPContentType[]>  fileExtensionLookup   = [];

        private        readonly  IEnumerable<String>                    fileExtensions        = FileExtensions ?? [];

        #endregion

        #region Properties

        /// <summary>
        /// The media main type.
        /// </summary>
        public String               MediaMainType    { get; } = MediaMainType;

        /// <summary>
        /// The media sub type.
        /// </summary>
        public String               MediaSubType     { get; } = MediaSubType;

        /// <summary>
        /// The optional character set.
        /// </summary>
        public String?              CharSet          { get; } = CharSet;

        /// <summary>
        /// The (optional) (SOAP) action.
        /// </summary>
        public String?              Action           { get; } = Action;

        /// <summary>
        /// The (optional) MIME boundary.
        /// </summary>
        public String?              MIMEBoundary     { get; } = MIMEBoundary;

        /// <summary>
        /// Well-known file extensions using this HTTP content type.
        /// </summary>
        public IEnumerable<String>  FileExtensions
            => fileExtensions;


        /// <summary>
        /// The constructed media type.
        /// </summary>
        public String               MediaType
            => $"{MediaMainType}/{MediaSubType}";

        #endregion


        #region (static) HTTPContentType()

        /// <summary>
        /// In order to discover all values within the subclasses during type initialization!
        /// </summary>
        static HTTPContentType()
        {

            var list         = new List<Object?>();
            var nestedTypes  = typeof(HTTPContentType).GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (var nestedType in nestedTypes)
            {
                var properties = nestedType.GetProperties(BindingFlags.Static | BindingFlags.Public);
                foreach (var property in properties)
                {
                    list.Add(property.GetValue(null));
                }
            }

            list.Clear();

        }

        #endregion


        #region (private static) Register(Text, NumericId = 0)

        private static HTTPContentType Register(String           MediaMainType,
                                                String           MediaSubType,
                                                String?          CharSet,
                                                String?          Action,
                                                String?          MIMEBoundary,
                                                params String[]  FileExtensions)
        {

            var httpContentType = new HTTPContentType(
                                      MediaMainType,
                                      MediaSubType,
                                      CharSet,
                                      Action,
                                      MIMEBoundary,
                                      FileExtensions
                                  );

            lookup.Add(
                $"{MediaMainType}/{MediaSubType}",
                httpContentType
            );

            FileExtensions.ForEach(fileExtension => {
                if (fileExtensionLookup.TryGetValue(fileExtension, out var value))
                    fileExtensionLookup[fileExtension] = [.. new List<HTTPContentType>(value) { httpContentType }];
                else
                    fileExtensionLookup.Add(fileExtension, [httpContentType]);
            });

            return httpContentType;

        }

        #endregion

        #region TryParse(Text, out HTTPContentType)

        public static Boolean TryParse(String Text, out HTTPContentType? HTTPContentType)
        {

            try
            {

                var parts       = Text.Split  (splitter, StringSplitOptions.RemoveEmptyEntries).
                                       Select (part => part.Trim()).
                                       ToArray();

                var mediaTypes  = parts[0].Split('/');
                var charset     = Array.Find(parts, part => part.StartsWith("charset",  StringComparison.OrdinalIgnoreCase));
                var boundary    = Array.Find(parts, part => part.StartsWith("boundary", StringComparison.OrdinalIgnoreCase));

                    charset     = charset is not null && charset.IsNeitherNullNorEmpty()
                                      ? charset[(charset.IndexOf('=') + 1)..].Trim()
                                      : null;

                if (boundary is not null)
                {

                    HTTPContentType = new HTTPContentType(
                                          mediaTypes[0],
                                          mediaTypes[1],
                                          charset,
                                          null,
                                          boundary[boundary.IndexOf("----")..]
                                      );
                    return true;

                }

                else
                {

                    if (!lookup.TryGetValue(parts[0], out HTTPContentType))
                        HTTPContentType = Register(
                                              mediaTypes[0],
                                              mediaTypes[1],
                                              charset,
                                              null,
                                              null
                                          );

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


        #region ForMediaType    (MediaType,     DefaultValue = null)

        public static HTTPContentType? ForMediaType(String            MediaType,
                                                    HTTPContentType?  DefaultValue = null)
        {

            if (lookup.TryGetValue(MediaType, out var HTTPContentType))
                return HTTPContentType;

            if (DefaultValue is not null)
                return DefaultValue;

            return ALL;

        }

        #endregion

        #region ForMediaType    (MediaType,     DefaultValueFactory)

        public static HTTPContentType? ForMediaType(String                 MediaType,
                                                    Func<HTTPContentType>  DefaultValueFactory)
        {

            if (lookup.TryGetValue(MediaType, out var HTTPContentType))
                return HTTPContentType;

            return DefaultValueFactory();

        }

        #endregion


        #region ForFileExtension(FileExtension, DefaultValue = null)

        public static IEnumerable<HTTPContentType> ForFileExtension(String            FileExtension,
                                                                    HTTPContentType?  DefaultValue = null)
        {

            if (fileExtensionLookup.TryGetValue(FileExtension, out var httpContentTypes))
                return httpContentTypes;

            if (DefaultValue is not null)
                return [DefaultValue ];

            return [];

        }

        #endregion

        #region ForFileExtension(FileExtension, DefaultValueFactory)

        public static IEnumerable<HTTPContentType> ForFileExtension(String                 FileExtension,
                                                                    Func<HTTPContentType>  DefaultValueFactory)
        {

            if (fileExtensionLookup.TryGetValue(FileExtension, out var httpContentTypes))
                return httpContentTypes;

            return [ DefaultValueFactory() ];

        }

        #endregion


        #region Clone()

        /// <summary>
        /// Clone this HTTP content type.
        /// </summary>
        public HTTPContentType Clone()

                => new (
                       MediaMainType.CloneString(),
                       MediaSubType. CloneString(),
                       CharSet?.     CloneString(),
                       Action?.      CloneString(),
                       MIMEBoundary?.CloneString(),
                       fileExtensions.Select(fileExtension => fileExtension.CloneString())
                   );

        #endregion


        #region Static definitions

        public static HTTPContentType ALL                    { get; }
            = Register("*", "*",                                    "utf-8", null, null);

        public static class Text
        {

            public static HTTPContentType PLAIN                  { get; }
                = Register("text", "plain",                             "utf-8", null, null, "txt");
            public static HTTPContentType HTML_UTF8              { get; }
                = Register("text", "html",                              "utf-8", null, null, "htm", "html", "shtml");
            public static HTTPContentType CSS_UTF8               { get; }
                = Register("text", "css",                               "utf-8", null, null, "css");
            public static HTTPContentType CSV_UTF8               { get; }
                = Register("text", "csv",                               "utf-8", null, null, "css");
            public static HTTPContentType JAVASCRIPT_UTF8        { get; }
                = Register("text", "javascript",                        "utf-8", null, null, "js");
            public static HTTPContentType XML_UTF8               { get; }
                = Register("text", "xml",                               "utf-8", null, null, "xml");
            public static HTTPContentType MARKDOWN_UTF8          { get; }
                = Register("text", "markdown",                          "utf-8", null, null, "md");

            public static HTTPContentType EVENTSTREAM            { get; }
                = Register("text", "event-stream",                      "utf-8", null, null);

        }

        public static class Application
        {

            public static HTTPContentType JSON_UTF8              { get; }
                = Register("application", "json",                       "utf-8", null, null, "json");
            public static HTTPContentType JSONLD_UTF8            { get; }
                = Register("application", "ld+json",                    "utf-8", null, null, "json-ld");
            public static HTTPContentType GeoJSON_UTF8           { get; }
                = Register("application", "geo+json",                   "utf-8", null, null, "geojson");
            public static HTTPContentType JSONMergePatch_UTF8    { get; }
                = Register("application", "merge-patch+json",           "utf-8", null, null);
            public static HTTPContentType XML_UTF8               { get; }
                = Register("application", "xml",                        "utf-8", null, null, "xml");
            public static HTTPContentType SOAPXML_UTF8           { get; }
                = Register("application", "soap+xml",                   "utf-8", null, null, "soap");
            public static HTTPContentType DNSMessage             { get; }
                = Register("application", "dns-message",                "utf-8", null, null, "dns");

            public static HTTPContentType GEXF_UTF8              { get; }
                = Register("application", "gexf+xml",                   "utf-8", null, null, "gexf");
            public static HTTPContentType GRAPHML_UTF8           { get; }
                = Register("application", "graphml+xml",                "utf-8", null, null, "graphml");
            public static HTTPContentType SWF                    { get; }
                = Register("application", "x-shockwave-flash",          null,    null, null, "swf");
            public static HTTPContentType PDF                    { get; }
                = Register("application", "pdf",                        "utf-8", null, null, "pdf");
            public static HTTPContentType CSV_App_UTF8           { get; }
                = Register("application", "csv",                        "utf-8", null, null, "csv");
            public static HTTPContentType SIG                    { get; }
                = Register("application", "pgp-signature",              "utf-8", null, null, "sig");

            public static HTTPContentType WOFF                   { get; }
                = Register("application", "font-woff",                  "utf-8", null, null, "woff", "woff2");

            public static HTTPContentType XWWWFormUrlEncoded     { get; }
                = Register("application", "x-www-form-urlencoded",      "utf-8", null, null);
            public static HTTPContentType OCTETSTREAM            { get; }
                = Register("application", "octet-stream",               "utf-8", null, null);

            public static HTTPContentType JavaScript             { get; }
                = Register("application", "javascript",                 "utf-8", null, null);
            public static HTTPContentType TypeScript             { get; }
                = Register("application", "typescript",                 "utf-8", null, null);

        }

        public static class Image
        {

            public static HTTPContentType GIF                    { get; }
                = Register("image", "gif",                              null,    null, null, "gif");
            public static HTTPContentType ICO                    { get; }
                = Register("image", "ico",                              null,    null, null, "ico");
            public static HTTPContentType PNG                    { get; }
                = Register("image", "png",                              null,    null, null, "png");
            public static HTTPContentType JPEG                   { get; }
                = Register("image", "jpeg",                             null,    null, null, "jpg", "jpeg");
            public static HTTPContentType SVG                    { get; }
                = Register("image", "svg+xml",                          "utf-8", null, null, "svg");

        }

        public static class Video
        {
            public static HTTPContentType OGM                    { get; }
                = Register("video", "ogm",                              null,    null, null, "ogm");
            public static HTTPContentType OGV                    { get; }
                = Register("video", "ogv",                              null,    null, null, "ogv");
            public static HTTPContentType OGG                    { get; }
                = Register("video", "ogg",                              null,    null, null, "ogg");
            public static HTTPContentType MP4                    { get; }
                = Register("video", "mp4",                              null,    null, null, "mp4");
            public static HTTPContentType WEBM                   { get; }
                = Register("video", "webm",                             null,    null, null, "webm");

        }

        public static class Multipart
        {

            public static HTTPContentType FORMDATA     { get; }
                = Register("multipart", "form-data",                    "utf-8", null, null);

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
        /// <param name="Object">An HTTP content type to compare with.</param>
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
        /// <param name="HTTPContentType">An HTTP content type to compare with.</param>
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
        /// <param name="HTTPContentType">An HTTP content type to compare with.</param>
        public override Boolean Equals(Object? Object)

            => Object is HTTPContentType httpContentType &&
                   Equals(httpContentType);

        #endregion

        #region Equals(HTTPContentType)

        /// <summary>
        /// Compares two HTTP content types for equality.
        /// </summary>
        /// <param name="HTTPContentType">An HTTP content type to compare with.</param>
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

        #region DebugView

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public String DebugView

            => String.Concat(

                   ToString(),

                   fileExtensions.Any()
                       ? $", file extensions: {fileExtensions.Aggregate((a, b) => a + ", " + b)}"
                       : String.Empty

               );

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this object.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"{MediaMainType}/{MediaSubType}",

                   CharSet is not null
                       ? $"; charset={CharSet}"
                       : String.Empty,

                   MIMEBoundary.IsNotNullOrEmpty()
                       ? $"; boundary=\"{MIMEBoundary}\""
                       : String.Empty,

                   Action.IsNotNullOrEmpty()
                       ? $"; action=\"{Action}\""
                       : String.Empty

               );

        #endregion


    }

}
