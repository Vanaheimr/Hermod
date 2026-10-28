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
    /// One alternative service (RFC 7838, Section 3): "the same origin is also
    /// reachable at this protocol, host and port". The classic use is a server
    /// announcing an HTTP/3 endpoint over an HTTP/2 connection, or in an
    /// Alt-Svc field over HTTP/1.1.
    ///
    /// Version-neutral since 2026-10-28: it lived in HTTP2/Core, where the
    /// HTTP/1.x stack could not reach it, although the Alt-Svc field it parses
    /// is the only way an HTTP/1.1 server can announce an alternative at all.
    ///
    /// An alternative is *not* a redirect. It names a different route to the same
    /// origin — the authority in requests stays what it was, and the server remains
    /// authoritative for it. That distinction is what makes an alternative safe to
    /// use without re-checking the origin, and it is why this type deliberately
    /// carries no notion of "the new origin".
    /// </summary>
    /// <param name="ProtocolId">ALPN protocol name, percent-decoded (e.g. "h3", "h2").</param>
    /// <param name="Host">Host of the alternative, or empty for "the same host".</param>
    /// <param name="Port">Port of the alternative.</param>
    /// <param name="MaxAge">How long the alternative may be used; the RFC's default is 24 hours.</param>
    /// <param name="Persist">Whether it survives a network change (the <c>persist=1</c> parameter).</param>
    public sealed record HTTPAlternativeService(String    ProtocolId,
                                                String    Host,
                                                UInt16    Port,
                                                TimeSpan  MaxAge,
                                                Boolean   Persist)
    {

        #region Data

        /// <summary>
        /// RFC 7838, Section 3.1: <c>ma</c> defaults to 24 hours.
        /// </summary>
        public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);

        #endregion

        #region ToFieldValue() / ToString()

        /// <summary>
        /// This alternative as it appears in an <c>Alt-Svc</c> field value or an
        /// ALTSVC frame payload.
        /// </summary>
        public String ToFieldValue()
        {

            var value = new StringBuilder(EncodeProtocolId(ProtocolId)).
                            Append("=\"").Append(Host).Append(':').Append(Port).Append('"');

            if (MaxAge != DefaultMaxAge)
                value.Append("; ma=").Append((Int64) MaxAge.TotalSeconds);

            if (Persist)
                value.Append("; persist=1");

            return value.ToString();

        }

        public override String ToString()
            => ToFieldValue();

        #endregion

        #region (private) EncodeProtocolId(ProtocolId)

        /// <summary>
        /// RFC 7838, Section 3: the protocol-id is a token, an ALPN name is any
        /// octet sequence. Octets that are not token characters MUST be
        /// percent-encoded, and so MUST "%"; every other octet MUST NOT be, and
        /// the hex digits are upper case. "http/1.1" is "http%2F1.1" on the wire.
        /// </summary>
        private static String EncodeProtocolId(String ProtocolId)
        {

            var encoded = new StringBuilder();

            foreach (var octet in Encoding.UTF8.GetBytes(ProtocolId))
            {

                if (octet != '%' && IsTokenCharacter(octet))
                    encoded.Append((Char) octet);

                else
                    encoded.Append('%').Append(octet.ToString("X2"));

            }

            return encoded.ToString();

        }

        /// <summary>
        /// tchar of RFC 9110, Section 5.6.2.
        /// </summary>
        private static Boolean IsTokenCharacter(Byte Octet)

            => Octet is >= (Byte) 'a' and <= (Byte) 'z'
                     or >= (Byte) 'A' and <= (Byte) 'Z'
                     or >= (Byte) '0' and <= (Byte) '9'
                     or (Byte) '!' or (Byte) '#' or (Byte) '$' or (Byte) '%' or (Byte) '&'
                     or (Byte) '\'' or (Byte) '*' or (Byte) '+' or (Byte) '-' or (Byte) '.'
                     or (Byte) '^' or (Byte) '_' or (Byte) '`' or (Byte) '|' or (Byte) '~';

        #endregion

        #region Parse (FieldValue)

        /// <summary>
        /// Parse an <c>Alt-Svc</c> field value into its alternatives, in the order
        /// the sender listed them (which is its order of preference).
        ///
        /// The single token <c>clear</c> is not an alternative but an instruction to
        /// forget all of them; it is reported by <paramref name="Clear"/> rather than
        /// smuggled in as an entry, since "no alternatives were listed" and "discard
        /// what you have" mean opposite things.
        ///
        /// A value that lists <c>clear</c> beside alternatives is invalid, and RFC
        /// 7838, Section 3 still has it invalidate every alternative, "including
        /// those specified in the same response". So <c>clear</c> anywhere in the
        /// list clears, and none of the alternatives beside it is returned. It is
        /// an ABNF literal, and so case-insensitive.
        ///
        /// Malformed entries are skipped rather than failing the whole value: this is
        /// advisory routing information, and dropping one unparseable alternative is
        /// better than discarding the ones next to it that were fine.
        /// </summary>
        public static IReadOnlyList<HTTPAlternativeService> Parse(String FieldValue, out Boolean Clear)
        {

            Clear = false;

            var trimmed = FieldValue.Trim();

            if (SplitOutsideQuotes(trimmed, ',').Any(entry => entry.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase)))
            {
                Clear = true;
                return [];
            }

            var result = new List<HTTPAlternativeService>();

            foreach (var entry in SplitOutsideQuotes(trimmed, ','))
            {

                var parts      = SplitOutsideQuotes(entry, ';').ToList();
                var alternative = parts.FirstOrDefault()?.Trim();

                if (String.IsNullOrEmpty(alternative))
                    continue;

                var equals = alternative.IndexOf('=');

                if (equals <= 0)
                    continue;

                // The ALPN name may be percent-encoded, since it is a token here but
                // an arbitrary octet sequence in ALPN itself (RFC 7838, Section 3).
                var protocolId = Uri.UnescapeDataString(alternative[..equals].Trim());
                var authority  = Unquote(alternative[(equals + 1)..].Trim());

                if (!TrySplitAuthority(authority, out var host, out var port))
                    continue;

                var maxAge  = DefaultMaxAge;
                var persist = false;

                foreach (var parameter in parts.Skip(1))
                {

                    var split = parameter.IndexOf('=');

                    if (split <= 0)
                        continue;

                    var name  = parameter[..split].Trim();
                    var value = Unquote(parameter[(split + 1)..].Trim());

                    if (name.Equals("ma", StringComparison.OrdinalIgnoreCase) &&
                        Int64.TryParse(value, out var seconds) && seconds >= 0)
                        maxAge = TimeSpan.FromSeconds(seconds);

                    else if (name.Equals("persist", StringComparison.OrdinalIgnoreCase))
                        persist = value == "1";

                }

                result.Add(new HTTPAlternativeService(protocolId, host, port, maxAge, persist));

            }

            return result;

        }

        /// <summary>
        /// Parse an <c>Alt-Svc</c> value, ignoring whether it was "clear".
        /// </summary>
        public static IReadOnlyList<HTTPAlternativeService> Parse(String FieldValue)
            => Parse(FieldValue, out _);

        #endregion

        #region (private) parsing helpers

        /// <summary>
        /// Split on a separator that appears outside a quoted string — the
        /// alt-authority is quoted and contains a colon, and parameter values may be
        /// quoted too, so naive splitting corrupts both.
        /// </summary>
        private static IEnumerable<String> SplitOutsideQuotes(String Value, Char Separator)
        {

            var start    = 0;
            var inQuotes = false;

            for (var i = 0; i < Value.Length; i++)
            {

                if (Value[i] == '"' && (i == 0 || Value[i - 1] != '\\'))
                    inQuotes = !inQuotes;

                else if (Value[i] == Separator && !inQuotes)
                {
                    yield return Value[start..i];
                    start = i + 1;
                }

            }

            if (start < Value.Length)
                yield return Value[start..];

        }

        private static String Unquote(String Value)

            => Value.Length >= 2 && Value[0] == '"' && Value[^1] == '"'
                   ? Value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\")
                   : Value;

        /// <summary>
        /// Split an alt-authority — <c>[host]:port</c>, where the host may be absent
        /// ("the same host") and an IPv6 literal is bracketed.
        /// </summary>
        private static Boolean TrySplitAuthority(String Authority, out String Host, out UInt16 Port)
        {

            Host = "";
            Port = 0;

            var colon = Authority.LastIndexOf(':');

            if (colon < 0 || colon == Authority.Length - 1)
                return false;

            if (!UInt16.TryParse(Authority[(colon + 1)..], out Port) || Port == 0)
                return false;

            Host = Authority[..colon];
            return true;

        }

        #endregion

    }

}
