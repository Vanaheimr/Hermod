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

using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3.WebTransport;

/// <summary>
/// Application protocol negotiation for WebTransport (draft-ietf-webtrans-http3-13 §3.3): in the
/// CONNECT request the client offers protocols in order of preference via
/// <c>WT-Available-Protocols</c> (a structured-fields list of strings, RFC 9651); the server picks
/// exactly one of them in the 2xx response via <c>WT-Protocol</c> (an SF item string). Value types
/// other than string invalidate the ENTIRE field (MUST ignore); parameters are ignored (no
/// semantics defined).
/// </summary>
public static class WebTransportProtocols
{
    /// <summary>
    /// Header field name of the client offer list (draft §3.3/§9.7; HTTP/3 requires lowercase).
    /// </summary>
    public const string AvailableProtocolsHeader = "wt-available-protocols";

    /// <summary>
    /// Header field name of the server pick (draft §3.3/§9.7).
    /// </summary>
    public const string ProtocolHeader = "wt-protocol";

    // ---- Serialising ----------------------------------------------------------------------

    /// <summary>
    /// Serialises the offer list as an SF list of strings (RFC 9651 §4.1), preference first.
    /// Throws <see cref="ArgumentException"/> for an empty list or characters not representable as
    /// an SF string (only %x20-7E allowed).
    /// </summary>
    public static string SerializeProtocolList(IReadOnlyList<string> protocols)
    {
        if (protocols.Count == 0)
            throw new ArgumentException("The protocol list must not be empty.", nameof(protocols));
        var sb = new StringBuilder();
        for (int i = 0; i < protocols.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            AppendSfString(sb, protocols[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Serialises the server pick as an SF item string (RFC 9651 §4.1).
    /// </summary>
    public static string SerializeProtocol(string protocol)
    {
        var sb = new StringBuilder();
        AppendSfString(sb, protocol);
        return sb.ToString();
    }

    /// <summary>
    /// Writes an SF string (RFC 9651 §4.1.6): DQUOTE-delimited, <c>"</c> and <c>\</c> escaped with a
    /// backslash; only printable ASCII characters (%x20-7E) are permitted.
    /// </summary>
    private static void AppendSfString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (char c in value)
        {
            if (c is < '\x20' or > '\x7e')
                throw new ArgumentException($"Character U+{(int)c:X4} is not representable in an SF string.", nameof(value));
            if (c is '"' or '\\')
                sb.Append('\\');
            sb.Append(c);
        }
        sb.Append('"');
    }

    // ---- Parsing --------------------------------------------------------------------------

    /// <summary>
    /// Parses <c>WT-Available-Protocols</c> as an SF list of strings. A member that is not a string
    /// (token, number, inner list, …), or a syntax error, invalidates the ENTIRE field (draft §3.3
    /// MUST) ⇒ <c>false</c>. Parameters are skipped over and ignored.
    /// </summary>
    public static bool TryParseProtocolList(string? value, out List<string> protocols)
    {
        protocols = [];
        if (string.IsNullOrWhiteSpace(value))
            return false;
        ReadOnlySpan<char> s = value.AsSpan();
        int i = 0;
        SkipOws(s, ref i);
        while (true)
        {
            if (!TryParseSfString(s, ref i, out string member) || !SkipParameters(s, ref i))
            {
                protocols.Clear();
                return false; // non-string member or syntax error ⇒ ignore the whole field
            }
            protocols.Add(member);
            SkipOws(s, ref i);
            if (i >= s.Length)
                return true;
            if (s[i] != ',')
            {
                protocols.Clear();
                return false;
            }
            i++; // ","
            SkipOws(s, ref i);
            if (i >= s.Length)
            {
                protocols.Clear();
                return false; // RFC 9651 §4.2.1: a comma without a following member is an error
            }
        }
    }

    /// <summary>
    /// Parses <c>WT-Protocol</c> as an SF item string. Another value type, a syntax error or leftover
    /// characters ⇒ ignore the field (<c>false</c>). Parameters are skipped over and ignored.
    /// </summary>
    public static bool TryParseProtocol(string? value, out string protocol)
    {
        protocol = "";
        if (string.IsNullOrWhiteSpace(value))
            return false;
        ReadOnlySpan<char> s = value.AsSpan();
        int i = 0;
        SkipOws(s, ref i);
        if (!TryParseSfString(s, ref i, out protocol) || !SkipParameters(s, ref i))
            return false;
        SkipOws(s, ref i);
        return i >= s.Length; // leftover characters ⇒ not a valid item
    }

    /// <summary>
    /// Reads an SF string (RFC 9651 §4.2.5): DQUOTE, characters %x20-7E with <c>\"</c>/<c>\\</c>
    /// escapes, DQUOTE. <c>false</c> when no valid string sits at the position.
    /// </summary>
    private static bool TryParseSfString(ReadOnlySpan<char> s, ref int i, out string result)
    {
        result = "";
        if (i >= s.Length || s[i] != '"')
            return false;
        i++;
        var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"')
            {
                result = sb.ToString();
                return true;
            }
            if (c == '\\')
            {
                if (i >= s.Length || (s[i] != '"' && s[i] != '\\'))
                    return false; // only \" and \\ are permissible escapes
                sb.Append(s[i++]);
                continue;
            }
            if (c is < '\x20' or > '\x7e')
                return false;
            sb.Append(c);
        }
        return false; // unterminated string
    }

    /// <summary>
    /// Skips a member's parameters (RFC 9651 §4.2.3.2: <c>*( ";" *SP key [ "=" bare-item ] )</c>).
    /// The values are only skipped syntactically — parameters have no semantics here (draft §3.3).
    /// </summary>
    private static bool SkipParameters(ReadOnlySpan<char> s, ref int i)
    {
        while (i < s.Length && s[i] == ';')
        {
            i++;
            while (i < s.Length && s[i] == ' ')
                i++;
            // key = (lcalpha / "*") *( lcalpha / DIGIT / "_" / "-" / "." / "*" )
            if (i >= s.Length || (s[i] is not (>= 'a' and <= 'z') && s[i] != '*'))
                return false;
            while (i < s.Length && (s[i] is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.' or '*'))
                i++;
            if (i < s.Length && s[i] == '=')
            {
                i++;
                if (!SkipBareItem(s, ref i))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Skips a bare item of any type (RFC 9651 §4.2.3.1: string, token, number, boolean,
    /// byte sequence, date, display string) — only syntactically, the value is discarded.
    /// </summary>
    private static bool SkipBareItem(ReadOnlySpan<char> s, ref int i)
    {
        if (i >= s.Length)
            return false;
        char c = s[i];
        if (c == '"')
            return TryParseSfString(s, ref i, out _);
        if (c == '?') // boolean: ?0 / ?1
        {
            if (i + 1 >= s.Length || (s[i + 1] != '0' && s[i + 1] != '1'))
                return false;
            i += 2;
            return true;
        }
        if (c == ':') // byte sequence: :base64:
        {
            int close = s[(i + 1)..].IndexOf(':');
            if (close < 0)
                return false;
            i += close + 2;
            return true;
        }
        if (c == '%') // display string: %"…" (escapes via %xx, a DQUOTE inside is always %22)
        {
            if (i + 1 >= s.Length || s[i + 1] != '"')
                return false;
            int close = s[(i + 2)..].IndexOf('"');
            if (close < 0)
                return false;
            i += close + 3;
            return true;
        }
        if (c == '@') // date: @ followed by an integer
            i++;
        if (i < s.Length && (s[i] == '-' || char.IsAsciiDigit(s[i]))) // integer/decimal
        {
            if (s[i] == '-')
                i++;
            if (i >= s.Length || !char.IsAsciiDigit(s[i]))
                return false;
            while (i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] == '.'))
                i++;
            return true;
        }
        if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '*') // token
        {
            while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) ||
                   s[i] is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~' or ':' or '/'))
                i++;
            return true;
        }
        return false;
    }

    private static void SkipOws(ReadOnlySpan<char> s, ref int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t'))
            i++;
    }
}
