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

    /// <summary>
    /// A parsed <c>Cache-Control</c> field value (RFC 9111, Section 5.2) — the
    /// same grammar serves request and response contexts, so this holds the union
    /// of both; a given field only ever sets the directives valid for its context.
    /// Unknown directives are ignored (Section 5.2.3).
    /// </summary>
    public sealed class HTTPCacheControl
    {

        public bool  NoStore              { get; private set; }
        public bool  NoCache              { get; private set; }
        public bool  Private              { get; private set; }
        public bool  Public               { get; private set; }
        public bool  MustRevalidate       { get; private set; }
        public bool  ProxyRevalidate      { get; private set; }
        public bool  Immutable            { get; private set; }
        public bool  OnlyIfCached         { get; private set; }   // request
        public bool  NoTransform          { get; private set; }

        public long? MaxAge               { get; private set; }
        public long? SMaxAge              { get; private set; }
        public long? MinFresh             { get; private set; }   // request
        public long? StaleWhileRevalidate { get; private set; }   // RFC 5861
        public long? StaleIfError         { get; private set; }   // RFC 5861

        /// <summary>Request <c>max-stale</c> with a value (seconds); see <see cref="MaxStaleAny"/> for the bare form.</summary>
        public long? MaxStale             { get; private set; }
        /// <summary>Request bare <c>max-stale</c> (no value) — accept a stale response of any age.</summary>
        public bool  MaxStaleAny          { get; private set; }

        private HTTPCacheControl() { }

        public static readonly HTTPCacheControl Empty = new();

        /// <summary>Parse the first <c>cache-control</c> field in a header list (comma-joining multiples).</summary>
        public static HTTPCacheControl FromHeaders(List<(string Name, string Value)> Headers)
        {
            var values = Headers.Where(h => h.Name == "cache-control").Select(h => h.Value).ToList();
            return values.Count == 0 ? Empty : Parse(string.Join(",", values));
        }

        public static HTTPCacheControl Parse(string? Value)
        {

            var cc = new HTTPCacheControl();
            if (string.IsNullOrWhiteSpace(Value))
                return cc;

            foreach (var raw in Value.Split(','))
            {

                var directive = raw.Trim();
                if (directive.Length == 0)
                    continue;

                var eq    = directive.IndexOf('=');
                var name  = (eq < 0 ? directive : directive[..eq]).Trim().ToLowerInvariant();
                var value = eq < 0 ? null : directive[(eq + 1)..].Trim().Trim('"');

                long? Seconds() => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : null;

                switch (name)
                {
                    case "no-store":               cc.NoStore = true;                 break;
                    case "no-cache":               cc.NoCache = true;                 break;
                    case "private":                cc.Private = true;                 break;
                    case "public":                 cc.Public = true;                  break;
                    case "must-revalidate":        cc.MustRevalidate = true;          break;
                    case "proxy-revalidate":       cc.ProxyRevalidate = true;         break;
                    case "immutable":              cc.Immutable = true;               break;
                    case "only-if-cached":         cc.OnlyIfCached = true;            break;
                    case "no-transform":           cc.NoTransform = true;             break;
                    case "max-age":                cc.MaxAge = Seconds();             break;
                    case "s-maxage":               cc.SMaxAge = Seconds();            break;
                    case "min-fresh":              cc.MinFresh = Seconds();           break;
                    case "stale-while-revalidate": cc.StaleWhileRevalidate = Seconds(); break;
                    case "stale-if-error":         cc.StaleIfError = Seconds();       break;
                    case "max-stale":
                        if (value is null) cc.MaxStaleAny = true;
                        else               cc.MaxStale = Seconds();
                        break;
                }

            }

            return cc;

        }

    }

}
