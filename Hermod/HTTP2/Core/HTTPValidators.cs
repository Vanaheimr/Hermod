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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Globalization;
    using System.Security.Cryptography;


    /// <summary>
    /// Validators (RFC 9110, Section 8.8) and the HTTP-date format they travel in:
    /// entity-tags, <c>Last-Modified</c> dates, and the strong/weak comparison
    /// rules that decide whether two responses describe the same representation.
    ///
    /// A server uses these to *evaluate* preconditions it receives; a client uses
    /// them to *build* preconditions it sends and to check what came back. Same
    /// rules, opposite ends — which is why they sit here rather than inside
    /// <see cref="HTTPSemantics"/>, whose public shape (a request-handler wrapper)
    /// a client cannot call at all.
    /// </summary>
    public static class HTTPValidators
    {

        #region HTTP-date (RFC 9110, Section 5.6.7)

        /// <summary>
        /// Parse an HTTP-date (IMF-fixdate, e.g. "Sun, 06 Nov 1994 08:49:37 GMT").
        /// .NET's "r" round-trip specifier is exactly that pattern; a lenient
        /// fallback via general parsing tolerates the handful of peers that send a
        /// slightly different but still valid format.
        /// </summary>
        public static Boolean TryParseDate(String Value, out DateTimeOffset Result)

            => DateTimeOffset.TryParseExact(Value, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out Result) ||
               DateTimeOffset.TryParse     (Value,      CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out Result);

        /// <summary>
        /// Format an HTTP-date as IMF-fixdate.
        /// </summary>
        public static String FormatDate(DateTimeOffset Value)

            => Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);

        /// <summary>
        /// HTTP-date carries no sub-second precision, so two dates must be compared
        /// at one-second granularity or a resume can spuriously fail against a
        /// resource whose stored timestamp has a fractional part.
        /// </summary>
        public static DateTimeOffset Truncate(DateTimeOffset Value)

            => new(Value.Year, Value.Month, Value.Day, Value.Hour, Value.Minute, Value.Second, Value.Offset);

        /// <summary>
        /// Whether two HTTP-dates denote the same second.
        /// </summary>
        public static Boolean SameInstant(DateTimeOffset Left, DateTimeOffset Right)

            => Truncate(Left) == Truncate(Right);

        #endregion

        #region Entity tags (RFC 9110, Section 8.8.3)

        /// <summary>
        /// Parse a comma-separated entity-tag list, as carried by <c>ETag</c>,
        /// <c>If-Match</c> and <c>If-None-Match</c>. <c>*</c> is not an entity-tag
        /// and is expected to be handled by the caller before reaching here; entries
        /// that are not properly quoted are skipped rather than guessed at.
        /// </summary>
        public static List<(String Tag, Boolean Weak)> ParseETagList(String Value)
        {

            var result = new List<(String, Boolean)>();

            foreach (var raw in Value.Split(','))
            {

                var token = raw.Trim();

                if (token.Length == 0)
                    continue;

                var weak = token.StartsWith("W/", StringComparison.Ordinal);

                if (weak)
                    token = token[2..];

                if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
                    result.Add((token, weak));

            }

            return result;

        }

        /// <summary>
        /// Strong comparison (RFC 9110, Section 8.8.3.2): both tags must be present,
        /// neither may be weak, and the opaque strings must be octet-equal. This is
        /// the comparison a range request needs — a weak validator says "equivalent
        /// enough to reuse whole", which is not enough to splice two byte ranges of
        /// what might be different bytes.
        /// </summary>
        public static Boolean StrongMatch(String? Left, String? Right)
        {

            if (Left is null || Right is null)
                return false;

            var (leftTag,  leftWeak)  = SplitETag(Left);
            var (rightTag, rightWeak) = SplitETag(Right);

            return !leftWeak && !rightWeak && leftTag == rightTag;

        }

        /// <summary>
        /// Weak comparison (RFC 9110, Section 8.8.3.2): the opaque strings must
        /// match, but either side may be weak. Used for cache revalidation, where
        /// "semantically equivalent" is the question.
        /// </summary>
        public static Boolean WeakMatch(String? Left, String? Right)
        {

            if (Left is null || Right is null)
                return false;

            return SplitETag(Left).Tag == SplitETag(Right).Tag;

        }

        /// <summary>
        /// Split an entity-tag into its opaque part and its weakness flag.
        /// </summary>
        public static (String Tag, Boolean Weak) SplitETag(String ETag)
        {

            var trimmed = ETag.Trim();
            var weak    = trimmed.StartsWith("W/", StringComparison.Ordinal);

            return (weak ? trimmed[2..] : trimmed, weak);

        }

        /// <summary>
        /// A strong entity-tag derived from the representation itself — a truncated
        /// SHA-256, which changes whenever a single byte does.
        /// </summary>
        public static String ComputeETag(Byte[] Body)

            => $"\"{Convert.ToHexString(SHA256.HashData(Body))[..32].ToLowerInvariant()}\"";

        #endregion

    }

}
