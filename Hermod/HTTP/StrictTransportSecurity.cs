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
    /// An HSTS policy, as carried by the Strict-Transport-Security response
    /// header field (RFC 6797, Section 6.1).
    /// </summary>
    /// <remarks>
    /// Emitting the field has been possible all along, through
    /// <c>SecurityHeaderOptions.StrictTransportSecurity</c>, which takes the
    /// text as it is to be sent. What was missing is the value: nothing could
    /// read the field, and nothing checked what was written into it. H-12 in
    /// HTTP1ConformanceTests, whose row said "no HSTS" and was half wrong in
    /// the same way H-2's was.
    ///
    /// Whether to send it remains the application's decision, and not a small
    /// one: a policy is a promise about every future request to a host, and a
    /// host that sends <c>max-age</c> and then loses its certificate has locked
    /// its own users out. Hermod's default is to send nothing, and that stays.
    /// </remarks>
    public readonly struct StrictTransportSecurity
    {

        #region Properties

        /// <summary>
        /// How long the host is to be regarded as a Known HSTS Host (RFC 6797,
        /// Section 6.1.1). Zero tells a user agent to stop regarding it as one.
        /// </summary>
        public TimeSpan  MaxAge             { get; }

        /// <summary>
        /// Whether the policy applies to subdomains as well (Section 6.1.2).
        /// </summary>
        public Boolean   IncludeSubDomains  { get; }

        /// <summary>
        /// The "preload" directive, which RFC 6797 does not define.
        /// </summary>
        /// <remarks>
        /// It is the condition browser vendors attach to their preload lists
        /// rather than a standardised directive, and Section 6.1 item 5 is why
        /// carrying it is harmless: a user agent that does not recognise a
        /// directive ignores it and processes the rest.
        /// </remarks>
        public Boolean   Preload            { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new HSTS policy.
        /// </summary>
        /// <param name="MaxAge">How long the host is to be regarded as a Known HSTS Host.</param>
        /// <param name="IncludeSubDomains">Whether the policy applies to subdomains as well.</param>
        /// <param name="Preload">The non-standard "preload" directive.</param>
        public StrictTransportSecurity(TimeSpan  MaxAge,
                                       Boolean   IncludeSubDomains   = false,
                                       Boolean   Preload             = false)
        {

            if (MaxAge < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(MaxAge), "max-age is a number of seconds and cannot be negative!");

            this.MaxAge             = MaxAge;
            this.IncludeSubDomains  = IncludeSubDomains;
            this.Preload            = Preload;

        }

        #endregion

        #region TryParse(Text, out Policy)

        /// <summary>
        /// Parse a Strict-Transport-Security field value.
        /// </summary>
        /// <remarks>
        /// RFC 6797, Section 6.1 is strict about what is refused, and all of it
        /// matters because the alternative to refusing is enforcing a policy
        /// nobody stated:
        ///
        /// - max-age is REQUIRED (6.1.1), so a field without one is refused;
        /// - "All directives MUST appear only once", so a repeated one refuses
        ///   the whole field rather than the duplicate;
        /// - directive names are case-insensitive;
        /// - an unrecognised directive is ignored and the rest processed (item
        ///   5), which is the one place this is permissive on purpose;
        /// - a value that does not conform is refused (item 4) rather than
        ///   repaired.
        /// </remarks>
        /// <param name="Text">The field value to parse.</param>
        /// <param name="Policy">The parsed policy.</param>
        public static Boolean TryParse(String                       Text,
                                       out StrictTransportSecurity  Policy)
        {

            Policy = default;

            if (Text is null)
                return false;

            TimeSpan?  maxAge             = null;
            Boolean?   includeSubDomains  = null;
            Boolean?   preload            = null;

            foreach (var directive in Text.Split(';'))
            {

                var text = directive.Trim();

                // "[ directive ] *( ";" [ directive ] )" — an empty one is
                // what that bracket allows, so "max-age=1;" is not malformed.
                if (text.Length == 0)
                    continue;

                var equals  = text.IndexOf('=');
                var name    = (equals < 0 ? text : text[..equals]).Trim();
                var value   =  equals < 0 ? null : text[(equals + 1)..].Trim();

                if (name.Equals("max-age", StringComparison.OrdinalIgnoreCase))
                {

                    // Only once.
                    if (maxAge.HasValue)
                        return false;

                    if (value is null)
                        return false;

                    // A directive-value may be a quoted-string, and the value
                    // is read "after quoted-string unescaping, if necessary"
                    // (6.1.1). Only the quotes, because 1*DIGIT has nothing in
                    // it that a backslash could escape.
                    if (value.Length > 1 && value[0] == '"' && value[^1] == '"')
                        value = value[1..^1];

                    if (value.Length == 0 || !value.All(Char.IsAsciiDigit))
                        return false;

                    // delta-seconds is 1*DIGIT and has no upper bound, so a
                    // value too large for a TimeSpan is clamped rather than
                    // refused: it is syntactically fine and means "a very long
                    // time", which is what the clamp says.
                    maxAge = UInt64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
                             seconds <= (UInt64) TimeSpan.MaxValue.TotalSeconds
                                 ? TimeSpan.FromSeconds(seconds)
                                 : TimeSpan.MaxValue;

                }

                else if (name.Equals("includeSubDomains", StringComparison.OrdinalIgnoreCase))
                {

                    if (includeSubDomains.HasValue)
                        return false;

                    // "a valueless directive" (6.1.2).
                    if (value is not null)
                        return false;

                    includeSubDomains = true;

                }

                else if (name.Equals("preload", StringComparison.OrdinalIgnoreCase))
                {

                    if (preload.HasValue)
                        return false;

                    if (value is not null)
                        return false;

                    preload = true;

                }

                // Item 5: an unrecognised directive is ignored, and the
                // recognised ones are still processed.

            }

            if (!maxAge.HasValue)
                return false;

            Policy = new StrictTransportSecurity(
                         maxAge.Value,
                         includeSubDomains ?? false,
                         preload           ?? false
                     );

            return true;

        }

        #endregion

        #region (override) ToString()

        /// <summary>
        /// The field value, with max-age first and the asserted directives
        /// after it.
        /// </summary>
        public override String ToString()

            => String.Concat(

                   $"max-age={(UInt64) MaxAge.TotalSeconds}",

                   IncludeSubDomains
                       ? "; includeSubDomains"
                       : "",

                   Preload
                       ? "; preload"
                       : ""

               );

        #endregion

    }

}
