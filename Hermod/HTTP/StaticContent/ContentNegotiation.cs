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

using System.Globalization;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    /// <summary>
    /// The small parts of RFC 9110 that static content delivery needs on the
    /// HTTP/1.1 server: choosing a content coding from Accept-Encoding,
    /// evaluating If-None-Match, and telling encoded representations apart
    /// by their entity tags.
    /// </summary>
    public static class ContentNegotiation
    {

        #region Data

        /// <summary>
        /// The content codings offered, in order of server preference.
        /// </summary>
        public static readonly String[] SupportedCodings = [ "br", "gzip" ];

        private static readonly HashSet<String> compressibleSubTypes = new(StringComparer.OrdinalIgnoreCase) {
            "json", "javascript", "ecmascript", "xml", "xhtml+xml", "svg+xml", "ld+json", "manifest+json", "wasm"
        };

        #endregion


        #region (static) SelectContentCoding(AcceptEncoding)

        /// <summary>
        /// Pick the content coding for the given Accept-Encoding header, or
        /// null when the client did not ask for one we offer. Quality values
        /// are honoured; a client that sends "*" gets the preferred coding
        /// unless it excluded it explicitly.
        /// </summary>
        /// <param name="AcceptEncoding">The Accept-Encoding header field value.</param>
        public static String? SelectContentCoding(String? AcceptEncoding)
        {

            if (String.IsNullOrWhiteSpace(AcceptEncoding))
                return null;

            var qualities = new Dictionary<String, Double>(StringComparer.OrdinalIgnoreCase);

            foreach (var element in AcceptEncoding.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {

                var parts    = element.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var coding   = parts[0];
                var quality  = 1.0;

                foreach (var parameter in parts.Skip(1))
                {
                    if (parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                        Double.TryParse(parameter[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        quality = parsed;
                    }
                }

                qualities[coding] = quality;

            }

            foreach (var coding in SupportedCodings)
            {

                if (qualities.TryGetValue(coding, out var quality))
                {
                    if (quality > 0)
                        return coding;
                    continue;
                }

                if (qualities.TryGetValue("*", out var wildcard) && wildcard > 0)
                    return coding;

            }

            return null;

        }

        #endregion

        #region (static) IsCompressible(ContentType)

        /// <summary>
        /// Whether content of this type is worth compressing: text of any
        /// kind and the text-like application types. Fonts, images and other
        /// already compressed formats are not.
        /// </summary>
        /// <param name="ContentType">An HTTP content type.</param>
        public static Boolean IsCompressible(HTTPContentType ContentType)
        {

            var mediaType  = ContentType.ToString().Split(';')[0].Trim();
            var slash      = mediaType.IndexOf('/');

            if (slash < 0)
                return false;

            var mainType   = mediaType[..slash];
            var subType    = mediaType[(slash + 1)..];

            return mainType.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                   compressibleSubTypes.Contains(subType);

        }

        #endregion

        #region (static) IfNoneMatchMatches(IfNoneMatch, ETag)

        /// <summary>
        /// Evaluate an If-None-Match header field for GET/HEAD against the
        /// entity tag of the selected representation, with the weak comparison
        /// of RFC 9110, section 13.1.2.
        /// </summary>
        /// <param name="IfNoneMatch">The If-None-Match header field value.</param>
        /// <param name="ETag">The entity tag of the selected representation, including the quotes.</param>
        public static Boolean IfNoneMatchMatches(String?  IfNoneMatch,
                                                 String   ETag)
        {

            if (String.IsNullOrWhiteSpace(IfNoneMatch))
                return false;

            var target = Opaque(ETag);

            foreach (var candidate in IfNoneMatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (candidate == "*" || Opaque(candidate) == target)
                    return true;
            }

            return false;

        }

        private static String Opaque(String ETag)

            => ETag.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
                   ? ETag[2..]
                   : ETag;

        #endregion

        #region (static) ETagForCoding(ETag, Coding)

        /// <summary>
        /// A strong entity tag must differ between the identity and an encoded
        /// representation of the same content: "abc" becomes "abc-br".
        /// </summary>
        /// <param name="ETag">The entity tag of the identity representation, including the quotes.</param>
        /// <param name="Coding">The content coding, or null for the identity representation.</param>
        public static String ETagForCoding(String   ETag,
                                           String?  Coding)

            => Coding is null || ETag.Length < 2 || !ETag.EndsWith('"')
                   ? ETag
                   : $"{ETag[..^1]}-{Coding}\"";

        #endregion

    }

}
