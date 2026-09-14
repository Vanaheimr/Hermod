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

using System.Diagnostics.CodeAnalysis;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.DNS
{

    /// <summary>
    /// The master file format of RFC 1035 §5.1 — a whole zone file, as opposed to
    /// the single line of one that <see cref="ADNSResourceRecord.ParseZoneFileString"/>
    /// reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The difference is not cosmetic. §5.1 makes a name that does not end in a dot
    /// relative to the *current origin*, and the current origin is a property of the
    /// file: it comes from a <c>$ORIGIN</c> line, or from whoever loaded the zone.
    /// A reader that takes one line at a time has no origin and therefore no way to
    /// complete such a name — and completing it anyway, by treating it as though it
    /// already ended in a dot, turns <c>ns1</c> into the top-level name <c>ns1.</c>
    /// without an error. A zone loaded that way is wrong everywhere and complains
    /// nowhere.
    /// </para>
    /// <para>
    /// What this reader understands, and what it does not: <c>$ORIGIN</c> and
    /// <c>$TTL</c> (RFC 2308 §4) are honoured, <c>@</c> is the origin, an owner name
    /// omitted by beginning the line with a blank repeats the previous one,
    /// parentheses group a record across several lines, and <c>;</c> begins a
    /// comment everywhere except inside a quoted string. <c>$INCLUDE</c> needs a
    /// file system that a reader handed a string does not have, and <c>$GENERATE</c>
    /// is a BIND extension rather than part of §5.1; both are refused by name rather
    /// than skipped, because the records they stand for would otherwise be silently
    /// missing from the zone.
    /// </para>
    /// </remarks>
    public static class DNSZoneFile
    {

        #region (static) Parse   (Text, Origin = null, DefaultTimeToLive = null)

        /// <summary>
        /// Read a zone file.
        /// </summary>
        /// <param name="Text">The text of the zone file.</param>
        /// <param name="Origin">The origin relative names are completed against, unless the file names its own with $ORIGIN.</param>
        /// <param name="DefaultTimeToLive">A TTL for records that state none and are not covered by a $TTL.</param>
        public static List<IDNSResourceRecord> Parse(String       Text,
                                                     DomainName?  Origin              = null,
                                                     TimeSpan?    DefaultTimeToLive   = null)
        {

            if (TryParse(Text, out var records, out var errorResponse, Origin, DefaultTimeToLive))
                return records;

            throw new ArgumentException(errorResponse, nameof(Text));

        }

        #endregion

        #region (static) TryParse(Text, out Records, out ErrorResponse, Origin = null, DefaultTimeToLive = null)

        /// <summary>
        /// Try to read a zone file.
        /// </summary>
        /// <param name="Text">The text of the zone file.</param>
        /// <param name="Records">The resource records it holds.</param>
        /// <param name="ErrorResponse">An optional error response in case the parsing fails.</param>
        /// <param name="Origin">The origin relative names are completed against, unless the file names its own with $ORIGIN.</param>
        /// <param name="DefaultTimeToLive">A TTL for records that state none and are not covered by a $TTL.</param>
        public static Boolean TryParse(String                                            Text,
                                       [NotNullWhen(true)]  out List<IDNSResourceRecord> Records,
                                       [NotNullWhen(false)] out String?                  ErrorResponse,
                                       DomainName?                                       Origin              = null,
                                       TimeSpan?                                         DefaultTimeToLive   = null)
        {

            Records        = [];
            ErrorResponse  = null;

            var origin           = Origin;
            var dollarTTL        = (TimeSpan?) null;
            var lastExplicitTTL  = (TimeSpan?) null;
            var lastOwner        = (String?)   null;

            foreach (var (line, ownerOmitted, number) in LogicalLines(Text))
            {

                #region $ORIGIN, $TTL — and the two that are refused by name

                if (line.StartsWith('$'))
                {

                    var directive = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    var keyword   = directive[0].ToUpperInvariant();

                    if (keyword == "$ORIGIN")
                    {

                        if (directive.Length < 2)
                        {
                            ErrorResponse = $"Line {number}: $ORIGIN needs a domain name!";
                            return false;
                        }

                        if (!DomainName.TryParseLenient(directive[1], out var newOrigin, out var originError))
                        {
                            ErrorResponse = $"Line {number}: $ORIGIN '{directive[1]}' is not a domain name: {originError}";
                            return false;
                        }

                        origin = newOrigin;
                        continue;

                    }

                    if (keyword == "$TTL")
                    {

                        if (directive.Length < 2 ||
                            !ADNSResourceRecord.TryParseZoneFileTimeToLive(directive[1], out var directiveTTL))
                        {
                            ErrorResponse = $"Line {number}: $TTL needs a TTL — '{(directive.Length < 2 ? "" : directive[1])}' is not one!";
                            return false;
                        }

                        dollarTTL = directiveTTL;
                        continue;

                    }

                    // Refused, not ignored: a zone quietly missing whatever these
                    // stand for is worse than a zone that will not load.
                    ErrorResponse = keyword switch {
                                        "$INCLUDE"   => $"Line {number}: $INCLUDE names another file, which a reader given a string cannot open!",
                                        "$GENERATE"  => $"Line {number}: $GENERATE is a BIND extension and is not part of RFC 1035 §5.1!",
                                        _            => $"Line {number}: unknown zone-file directive '{directive[0]}'!"
                                    };

                    return false;

                }

                #endregion

                var tokens = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length == 0)
                    continue;

                #region The owner name: stated, or repeated from the record before

                String owner;

                if (ownerOmitted)
                {

                    if (lastOwner is null)
                    {
                        ErrorResponse = $"Line {number}: the owner name is omitted, but there is no record before it to take one from!";
                        return false;
                    }

                    owner = lastOwner;

                }
                else
                {
                    owner   = tokens[0];
                    tokens  = tokens[1..];
                }

                // The one place where guessing is worse than failing. A relative
                // name needs an origin; with none, the older readers took it as
                // complete, which put "ns1" at the top level and said nothing.
                // A file has no way to be right about this, so it says so.
                if (origin is null && owner != "@" && !owner.EndsWith('.'))
                {
                    ErrorResponse = $"Line {number}: the owner name '{owner}' is relative (RFC 1035 §5.1), " +
                                    "but the file declares no $ORIGIN and none was given. Taking it as complete " +
                                    "would put the record at the top level instead of in the zone.";
                    return false;
                }

                lastOwner = owner;

                #endregion

                #region The TTL: RFC 2308 §4's $TTL, else the last one actually written down

                // RFC 1035 §5.1 defaults an omitted TTL to "the last explicitly
                // stated value"; RFC 2308 §4 added $TTL, which takes precedence
                // where it is present — so a file with a $TTL does not drift when
                // one record states a TTL of its own.
                if (tokens.Length > 0 && ADNSResourceRecord.TryParseZoneFileTimeToLive(tokens[0], out var explicitTTL))
                    lastExplicitTTL = explicitTTL;

                var defaultTTL = dollarTTL ?? lastExplicitTTL ?? DefaultTimeToLive;

                #endregion

                if (!ADNSResourceRecord.TryParseZoneFileString($"{owner} {String.Join(' ', tokens)}",
                                                               out var record,
                                                               out var recordError,
                                                               defaultTTL,
                                                               origin))
                {
                    ErrorResponse = $"Line {number}: {recordError}";
                    return false;
                }

                Records.Add(record);

            }

            return true;

        }

        #endregion

        #region (private static) LogicalLines(Text)

        /// <summary>
        /// The physical lines of a zone file, with comments removed and records
        /// that parentheses spread across several lines joined back into one.
        /// </summary>
        /// <remarks>
        /// Whether the owner name was omitted has to be decided here and carried
        /// out, because it is a property of the *physical* line — "if a line begins
        /// with a blank, then the owner is assumed to be the same as that of the
        /// previous RR" — and joining lines destroys the evidence.
        /// </remarks>
        private static IEnumerable<(String Line, Boolean OwnerOmitted, Int32 Number)> LogicalLines(String Text)
        {

            var joined        = new StringBuilder();
            var depth         = 0;
            var ownerOmitted  = false;
            var startedAt     = 0;
            var number        = 0;

            foreach (var physical in (Text ?? "").Split('\n'))
            {

                number++;

                var line = StripComment(physical.TrimEnd('\r'), ref depth);

                if (depth == 0 && joined.Length == 0 && line.Trim().Length == 0)
                    continue;

                if (joined.Length == 0)
                {
                    ownerOmitted  = line.Length > 0 && (line[0] == ' ' || line[0] == '\t');
                    startedAt     = number;
                }
                else
                    joined.Append(' ');

                joined.Append(line.Trim());

                if (depth > 0)
                    continue;

                var complete = joined.ToString().Trim();
                joined.Clear();

                if (complete.Length > 0)
                    yield return (complete, ownerOmitted, startedAt);

            }

            if (joined.Length > 0)
                yield return (joined.ToString().Trim(), ownerOmitted, startedAt);

        }

        #endregion

        #region (private static) StripComment(Line, ref Depth)

        /// <summary>
        /// Remove a <c>;</c> comment and track the parenthesis depth, both of which
        /// stop mattering inside a quoted string — a TXT record is entitled to hold
        /// a semicolon, and several do.
        /// </summary>
        private static String StripComment(String Line, ref Int32 Depth)
        {

            var result     = new StringBuilder(Line.Length);
            var inQuote    = false;
            var isEscaped  = false;

            foreach (var c in Line)
            {

                if (isEscaped)
                {
                    result.Append(c);
                    isEscaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    result.Append(c);
                    isEscaped = true;
                    continue;
                }

                if (c == '"')
                    inQuote = !inQuote;

                if (!inQuote)
                {

                    if (c == ';')
                        break;

                    if (c == '(')
                        Depth++;

                    else if (c == ')')
                        Depth = Math.Max(0, Depth - 1);

                }

                result.Append(c);

            }

            return result.ToString();

        }

        #endregion

    }

}
