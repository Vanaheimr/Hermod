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

using org.GraphDefined.Vanaheimr.Hermod.HTTP3.Qpack;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// Malformed detection for HTTP/3 messages (RFC 9114 §4.1.2, §4.2, §4.3): forbidden/missing/
/// invalid pseudo-headers, pseudo-headers after regular fields or in trailers, uppercase and
/// invalid characters in field names/values, connection-specific fields, and content-length
/// consistency. The checks are deliberately strict — §4.1.2: "they are deliberately strict
/// because being permissive can expose implementations to these vulnerabilities."
/// Return: <c>null</c> = well-formed, otherwise a short reason.
/// </summary>
internal static class Http3MessageValidator
{
    /// <summary>
    /// Checks a request header section (RFC 9114 §4.3.1). CONNECT (§4.4) counts as well-formed when
    /// :authority is present and :scheme/:path are absent — whether it is supported is up to the
    /// server (501).
    /// </summary>
    public static string? ValidateRequestHeaders(IReadOnlyList<HeaderField> fields)
    {
        bool regularSeen = false;
        int method = 0, scheme = 0, path = 0, authority = 0, protocol = 0;
        string methodValue = "", schemeValue = "", pathValue = "", authorityValue = "", protocolValue = "";
        string? hostValue = null;

        foreach (HeaderField field in fields)
        {
            if (field.Name.StartsWith(':'))
            {
                if (regularSeen)
                    return "pseudo-header after regular field"; // §4.3
                if (!IsValidFieldValue(field.Value))
                    return "invalid characters in pseudo-header value";
                switch (field.Name)
                {
                    case ":method": method++; methodValue = field.Value; break;
                    case ":scheme": scheme++; schemeValue = field.Value; break;
                    case ":path": path++; pathValue = field.Value; break;
                    case ":authority": authority++; authorityValue = field.Value; break;
                    case ":protocol": protocol++; protocolValue = field.Value; break; // RFC 8441 §4
                    default: return "undefined pseudo-header in request"; // §4.3 (incl. :status)
                }
            }
            else
            {
                regularSeen = true;
                if (ValidateRegularField(field) is { } problem)
                    return problem;
                if (field.Name == "host")
                    hostValue = field.Value;
            }
        }

        if (method != 1)
            return "exactly one :method required"; // §4.3.1
        if (methodValue.Length == 0)
            return "empty :method";

        // :protocol is ONLY defined on CONNECT requests (RFC 8441 §4).
        if (protocol > 0 && methodValue != "CONNECT")
            return ":protocol on non-CONNECT request";
        if (protocol > 1)
            return "multiple :protocol values"; // RFC 8441 §4: single valued

        // Classic CONNECT (§4.4): :scheme/:path MUST be absent, :authority MUST be present.
        // Extended CONNECT (RFC 8441 §4): with :protocol, :scheme and :path MUST be present and
        // :authority follows the NORMAL request rules — that case falls through below.
        if (methodValue == "CONNECT" && protocol == 0)
            return scheme > 0 || path > 0 ? "CONNECT with :scheme/:path"
                 : authority != 1 || authorityValue.Length == 0 ? "CONNECT without :authority"
                 : null;
        if (methodValue == "CONNECT" && protocolValue.Length == 0)
            return "empty :protocol";

        if (scheme != 1 || path != 1)
            return "exactly one :scheme and :path required"; // §4.3.1
        if (schemeValue.Length == 0)
            return "empty :scheme";
        if (pathValue.Length == 0)
            return "empty :path"; // §4.3.1: for http/https, "/" or "*" MUST be sent
        if (authority > 1)
            return "multiple :authority values";

        if (schemeValue is "http" or "https")
        {
            // §4.3.1: :authority OR Host required, non-empty; both present ⇒ identical;
            // the deprecated userinfo component ("…@…") is forbidden.
            string? effective = authority == 1 ? authorityValue : hostValue;
            if (effective is not { Length: > 0 })
                return "missing :authority/Host for http(s) scheme";
            if (authority == 1 && hostValue is not null && authorityValue != hostValue)
                return ":authority and Host differ";
            if (effective.Contains('@'))
                return "userinfo in :authority";
        }

        return null;
    }

    /// <summary>
    /// Checks a response header section (RFC 9114 §4.3.2): exactly ONE valid <c>:status</c>
    /// (three digits, 100–599), no other pseudo-headers, pseudo-headers before regular fields.
    /// </summary>
    public static string? ValidateResponseHeaders(IReadOnlyList<HeaderField> fields, out int status)
    {
        status = 0;
        bool regularSeen = false;
        int statusCount = 0;

        foreach (HeaderField field in fields)
        {
            if (field.Name.StartsWith(':'))
            {
                if (regularSeen)
                    return "pseudo-header after regular field"; // §4.3
                if (field.Name != ":status")
                    return "undefined pseudo-header in response"; // §4.3
                statusCount++;
                if (field.Value.Length != 3 || !field.Value.All(char.IsAsciiDigit) ||
                    !int.TryParse(field.Value, out status) || status < 100)
                    return "invalid :status value";
            }
            else
            {
                regularSeen = true;
                if (ValidateRegularField(field) is { } problem)
                    return problem;
            }
        }

        return statusCount != 1 ? "exactly one :status required" : null; // §4.3.2
    }

    /// <summary>
    /// Checks a trailer section (RFC 9114 §4.3): pseudo-headers are forbidden there.
    /// </summary>
    public static string? ValidateTrailers(IReadOnlyList<HeaderField> fields)
    {
        foreach (HeaderField field in fields)
        {
            if (field.Name.StartsWith(':'))
                return "pseudo-header in trailer section"; // §4.3
            if (ValidateRegularField(field) is { } problem)
                return problem;
        }
        return null;
    }

    /// <summary>
    /// Content-length consistency (RFC 9114 §4.1.2): when a <c>content-length</c> is present, it
    /// MUST equal the sum of the DATA lengths — unless the message is body-less by definition
    /// (<paramref name="contentNeverPresent"/>: HEAD responses, 204/304) and no content arrived.
    /// </summary>
    public static string? ValidateContentLength(IReadOnlyList<HeaderField> fields, ulong actualLength, bool contentNeverPresent)
    {
        ulong? declared = null;
        foreach (HeaderField field in fields)
        {
            if (field.Name != "content-length")
                continue;
            if (field.Value.Length == 0 || !field.Value.All(char.IsAsciiDigit) || !ulong.TryParse(field.Value, out ulong value))
                return "invalid content-length value";
            if (declared is { } previous && previous != value)
                return "conflicting content-length values";
            declared = value;
        }

        if (declared is { } length && length != actualLength && !(contentNeverPresent && actualLength == 0))
            return "content-length does not match DATA length";
        return null;
    }

    /// <summary>
    /// Regular field (RFC 9114 §4.2): name of ASCII "token" characters in lowercase, value without
    /// NUL/CR/LF; connection-specific fields are forbidden, <c>te</c> only with "trailers".
    /// </summary>
    private static string? ValidateRegularField(HeaderField field)
    {
        if (!IsValidFieldName(field.Name))
            return field.Name.Any(char.IsAsciiLetterUpper)
                ? "uppercase field name"            // §4.2 MUST
                : "invalid characters in field name";
        if (!IsValidFieldValue(field.Value))
            return "invalid characters in field value";
        if (field.Name is "connection" or "proxy-connection" or "keep-alive" or "transfer-encoding" or "upgrade")
            return "connection-specific field";     // §4.2 (transfer-encoding also §4.1)
        if (field.Name == "te" && !field.Value.Equals("trailers", StringComparison.OrdinalIgnoreCase))
            return "te value other than trailers";  // §4.2
        return null;
    }

    /// <summary>
    /// Field name: non-empty, only "token" characters (RFC 9110 §5.1) in lowercase.
    /// </summary>
    private static bool IsValidFieldName(string name)
    {
        if (name.Length == 0)
            return false;
        foreach (char c in name)
        {
            bool ok = c is >= 'a' and <= 'z' or >= '0' and <= '9'
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.'
                or '^' or '_' or '`' or '|' or '~';
            if (!ok)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Field value: no NUL/CR/LF bytes (§4.1.2 "invalid characters", protection against request smuggling).
    /// </summary>
    private static bool IsValidFieldValue(string value)
    {
        foreach (char c in value)
            if (c is '\0' or '\r' or '\n')
                return false;
        return true;
    }
}
