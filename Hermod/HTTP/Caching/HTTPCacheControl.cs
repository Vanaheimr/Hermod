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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP
{

    using System.Text;

    /// <summary>
    /// A parsed <c>Cache-Control</c> field value (RFC 9111, Section 5.2) — the
    /// same grammar serves request and response contexts, so this holds the union
    /// of both; a given field only ever sets the directives valid for its context.
    /// Unknown directives are ignored (Section 5.2.3).
    ///
    /// Where a directive occurs more than once, the first occurrence counts
    /// (Section 4.2.1). A max-age or s-maxage whose argument is not
    /// delta-seconds is read as 0, so that the response is stale rather than
    /// fresh on a guess (Section 4.2.1: "a cache SHOULD consider responses with
    /// invalid freshness information to be stale"), and an argument too large
    /// to represent is read as <see cref="MaxDeltaSeconds"/> (Section 1.2.2).
    /// Commas inside a quoted argument, as in <c>no-cache="Set-Cookie, Foo"</c>,
    /// do not end the directive.
    /// </summary>
    public sealed class HTTPCacheControl
    {

        #region Data

        /// <summary>
        /// What a delta-seconds value too large to represent is read as: 2^38
        /// seconds, some 8 700 years. RFC 9111, Section 1.2.2 lets a cache take
        /// either 2^31 or "the greatest positive integer it can conveniently
        /// represent". 2^31 is the limit of 32-bit arithmetic, which a TimeSpan
        /// does not have, and with it max-age=4294967295 lasted 68 years, and a
        /// response whose Age was already near 2^31 went stale seconds after it
        /// arrived. The values become TimeSpans, and 2^38 is chosen so that
        /// the arithmetic of Section 4.2.3 on them cannot overflow one: it adds
        /// a response delay and a resident time, and subtracts a freshness
        /// lifetime that an Expires before its Date can make as negative as the
        /// span between the first and the last date a DateTimeOffset holds,
        /// under 2^39 seconds. A TimeSpan holds more than 2^38 + 2^39 seconds,
        /// which leaves some 3 000 years for the delay and the resident time.
        /// </summary>
        public const Int64 MaxDeltaSeconds = 274877906944;

        #endregion

        #region Properties

        public Boolean  NoStore               { get; private set; }
        public Boolean  NoCache               { get; private set; }
        public Boolean  Private               { get; private set; }
        public Boolean  Public                { get; private set; }
        public Boolean  MustRevalidate        { get; private set; }
        public Boolean  ProxyRevalidate       { get; private set; }
        public Boolean  Immutable             { get; private set; }   // RFC 8246
        public Boolean  OnlyIfCached          { get; private set; }   // request
        public Boolean  NoTransform           { get; private set; }
        public Int64?   MaxAge                { get; private set; }
        public Int64?   SMaxAge               { get; private set; }
        public Int64?   MinFresh              { get; private set; }   // request
        public Int64?   StaleWhileRevalidate  { get; private set; }   // RFC 5861
        public Int64?   StaleIfError          { get; private set; }   // RFC 5861

        /// <summary>
        /// Request <c>max-stale</c> with a value (seconds); see <see cref="MaxStaleAny"/> for the bare form.
        /// </summary>
        public Int64?   MaxStale              { get; private set; }

        /// <summary>
        /// Request bare <c>max-stale</c> (no value) — accept a stale response of any age.
        /// </summary>
        public Boolean  MaxStaleAny           { get; private set; }

        #endregion

        #region Constructor(s)

        private HTTPCacheControl() { }

        public static readonly HTTPCacheControl Empty = new();

        #endregion


        #region FromHeaders(Headers)

        /// <summary>
        /// Parse every <c>cache-control</c> field line in a header list, as one
        /// comma-joined value (RFC 9110, Section 5.3).
        /// </summary>
        public static HTTPCacheControl FromHeaders(List<(String Name, String Value)> Headers)
        {

            var values = Headers.Where (header => header.Name == "cache-control").
                                 Select(header => header.Value).
                                 ToList();

            return values.Count == 0
                       ? Empty
                       : Parse(String.Join(",", values));

        }

        #endregion

        #region Parse(Value)

        public static HTTPCacheControl Parse(String? Value)
        {

            var cc = new HTTPCacheControl();

            if (String.IsNullOrWhiteSpace(Value))
                return cc;

            var seen = new HashSet<String>();

            foreach (var raw in SplitDirectives(Value))
            {

                var directive = raw.Trim();
                if (directive.Length == 0)
                    continue;

                var eq        = directive.IndexOf('=');
                var name      = (eq < 0 ? directive : directive[..eq]).Trim().ToLowerInvariant();
                var argument  = eq < 0 ? null : Unquote(directive[(eq + 1)..].Trim());

                // Section 4.2.1: the first occurrence is the one used.
                if (!seen.Add(name))
                    continue;

                switch (name)
                {
                    case "no-store":                cc.NoStore               = true;                       break;
                    case "no-cache":                cc.NoCache               = true;                       break;
                    case "private":                 cc.Private               = true;                       break;
                    case "public":                  cc.Public                = true;                       break;
                    case "must-revalidate":         cc.MustRevalidate        = true;                       break;
                    case "proxy-revalidate":        cc.ProxyRevalidate       = true;                       break;
                    case "immutable":               cc.Immutable             = true;                       break;
                    case "only-if-cached":          cc.OnlyIfCached          = true;                       break;
                    case "no-transform":            cc.NoTransform           = true;                       break;
                    case "max-age":                 cc.MaxAge                = DeltaSeconds(argument) ?? 0; break;
                    case "s-maxage":                cc.SMaxAge               = DeltaSeconds(argument) ?? 0; break;
                    case "min-fresh":               cc.MinFresh              = DeltaSeconds(argument);      break;
                    case "stale-while-revalidate":  cc.StaleWhileRevalidate  = DeltaSeconds(argument);      break;
                    case "stale-if-error":          cc.StaleIfError          = DeltaSeconds(argument);      break;
                    case "max-stale":
                        if (argument is null) cc.MaxStaleAny = true;
                        else                  cc.MaxStale    = DeltaSeconds(argument);
                        break;
                }

            }

            return cc;

        }

        #endregion


        #region (private) SplitDirectives(Value)

        /// <summary>
        /// Split at the commas between directives, not at those inside a quoted argument.
        /// </summary>
        private static IEnumerable<String> SplitDirectives(String Value)
        {

            var start   = 0;
            var quoted  = false;

            for (var i = 0; i < Value.Length; i++)
            {

                var c = Value[i];

                if (quoted)
                {
                    if      (c == '\\') i++;
                    else if (c == '"')  quoted = false;
                }

                else if (c == '"')
                    quoted = true;

                else if (c == ',')
                {
                    yield return Value[start..i];
                    start = i + 1;
                }

            }

            if (start <= Value.Length)
                yield return Value[start..];

        }

        #endregion

        #region (private) Unquote(Argument)

        /// <summary>
        /// The argument without its quotes. A sender must not quote max-age and
        /// its kind (Section 5.2.2.1), and a recipient that reads "60" as 60
        /// anyway loses nothing.
        /// </summary>
        private static String Unquote(String Argument)
        {

            if (Argument.Length < 2 || Argument[0] != '"' || Argument[^1] != '"')
                return Argument;

            var text = new StringBuilder(Argument.Length);

            for (var i = 1; i < Argument.Length - 1; i++)
            {
                if (Argument[i] == '\\' && i + 1 < Argument.Length - 1)
                    i++;
                text.Append(Argument[i]);
            }

            return text.ToString();

        }

        #endregion

        #region (internal) DeltaSeconds(Argument)

        /// <summary>
        /// delta-seconds = 1*DIGIT (RFC 9111, Section 1.2.2), or null when the
        /// argument is missing or anything else. A value too large is
        /// <see cref="MaxDeltaSeconds"/>. The Age field's value is parsed here too.
        /// </summary>
        internal static Int64? DeltaSeconds(String? Argument)
        {

            if (String.IsNullOrEmpty(Argument))
                return null;

            Int64 value = 0;

            foreach (var c in Argument)
            {

                if (c is < '0' or > '9')
                    return null;

                if (value < MaxDeltaSeconds)
                    value = value * 10 + (c - '0');

            }

            return Math.Min(value, MaxDeltaSeconds);

        }

        #endregion

    }

}
