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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// A pragmatic RFC 5322 address-list parser: extracts the addr-specs (local@domain)
/// from a From/To/Cc-style header value. Handles display names, angle addresses,
/// quoted local-parts, domain-literals, comments, groups, and commas inside quotes
/// or angle brackets. Bare hosts without a dot (e.g. "user@localhost") are accepted.
/// </summary>
public static class MailAddressParser
{

    /// <summary>
    /// Parse a header value and return the first addr-spec, or null.
    /// </summary>
    public static String? ParseSingle(String headerValue)
        => ParseAddressList(headerValue).FirstOrDefault();

    /// <summary>
    /// Parse a header value into its list of addr-specs (order preserved).
    /// </summary>
    public static List<String> ParseAddressList(String headerValue)
    {

        var result = new List<String>();
        if (String.IsNullOrWhiteSpace(headerValue))
            return result;

        var withoutComments = StripComments(headerValue);

        foreach (var mailbox in SplitTopLevel(withoutComments, ','))
        {
            var addr = TryExtractAddrSpec(mailbox);
            if (addr is not null)
                result.Add(addr);
        }

        return result;

    }


    #region (private) Mailbox -> addr-spec

    private static String? TryExtractAddrSpec(String mailbox)
    {

        // name-addr: use the content of the (last) angle-addr "<addr-spec>".
        var lt = mailbox.LastIndexOf('<');
        if (lt >= 0)
        {
            var gt    = mailbox.IndexOf('>', lt + 1);
            var inner = gt > lt ? mailbox[(lt + 1)..gt] : mailbox[(lt + 1)..];
            return CleanAddrSpec(inner);
        }

        // bare addr-spec: drop a leading group-name ("Group:") and a trailing ';'.
        var s     = mailbox;
        var colon = FindTopLevelColon(s);
        if (colon >= 0)
            s = s[(colon + 1)..];

        return CleanAddrSpec(s.Trim().TrimEnd(';'));

    }

    private static String? CleanAddrSpec(String s)
    {

        s = s.Trim();
        if (s.Length == 0)
            return null;

        var at = FindSeparatorAt(s);
        if (at <= 0 || at >= s.Length - 1)
            return null;

        var local  = s[..at].     Trim();
        var domain = s[(at + 1)..].Trim();

        if (!IsPlausibleLocalPart(local) || !IsPlausibleDomain(domain))
            return null;

        return $"{local}@{domain}";

    }

    #endregion

    #region (private) Validation

    private static Boolean IsPlausibleLocalPart(String local)
    {

        if (local.Length == 0)
            return false;

        // quoted-string local part, e.g. "john doe"
        if (local.Length >= 2 && local[0] == '"' && local[^1] == '"')
            return true;

        // dot-atom: reject only embedded whitespace (allows +, ., _, -, etc.)
        foreach (var c in local)
            if (Char.IsWhiteSpace(c))
                return false;

        return true;

    }

    private static Boolean IsPlausibleDomain(String domain)
    {

        // domain-literal, e.g. [192.0.2.1] or [IPv6:2001:db8::1]
        if (domain.Length > 2 && domain[0] == '[' && domain[^1] == ']')
            return true;

        var d = domain.TrimEnd('.');   // tolerate a single trailing dot (FQDN)
        if (d.Length == 0 || d.Length > 253)
            return false;

        foreach (var label in d.Split('.'))
        {
            if (label.Length == 0 || label.Length > 63)
                return false;
            if (label[0] == '-' || label[^1] == '-')
                return false;
            foreach (var c in label)
                if (!(Char.IsLetterOrDigit(c) || c == '-'))   // IsLetterOrDigit also accepts IDN/UTF-8
                    return false;
        }

        return true;

    }

    #endregion

    #region (private) Scanning helpers

    // Split at the separator only at the top level (not inside quotes, angle brackets,
    // parentheses, or domain-literal brackets).
    private static List<String> SplitTopLevel(String s, Char sep)
    {

        var parts   = new List<String>();
        var sb      = new StringBuilder();
        var inQuote = false;
        int paren = 0, angle = 0, bracket = 0;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (inQuote)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < s.Length) { sb.Append(s[++i]); continue; }
                if (c == '"') inQuote = false;
                continue;
            }

            switch (c)
            {
                case '"': inQuote = true;                sb.Append(c); break;
                case '(': paren++;                       sb.Append(c); break;
                case ')': if (paren   > 0) paren--;      sb.Append(c); break;
                case '<': angle++;                       sb.Append(c); break;
                case '>': if (angle   > 0) angle--;      sb.Append(c); break;
                case '[': bracket++;                     sb.Append(c); break;
                case ']': if (bracket > 0) bracket--;    sb.Append(c); break;
                default:
                    if (c == sep && paren == 0 && angle == 0 && bracket == 0)
                    {
                        parts.Add(sb.ToString());
                        sb.Clear();
                    }
                    else
                        sb.Append(c);
                    break;
            }
        }

        if (sb.Length > 0)
            parts.Add(sb.ToString());

        return parts;

    }

    // Remove RFC 5322 comments (nested parentheses), honoring quoted strings and escapes.
    private static String StripComments(String s)
    {

        var sb      = new StringBuilder(s.Length);
        var inQuote = false;
        var depth   = 0;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (inQuote)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < s.Length) { sb.Append(s[++i]); continue; }
                if (c == '"') inQuote = false;
                continue;
            }

            if (depth > 0)
            {
                if (c == '\\' && i + 1 < s.Length) { i++; continue; }
                if (c == '(') depth++;
                else if (c == ')') depth--;
                continue;
            }

            if (c == '"') { inQuote = true; sb.Append(c); }
            else if (c == '(') depth++;
            else sb.Append(c);
        }

        return sb.ToString();

    }

    // First ':' at the top level (not inside quotes / brackets / angle brackets).
    private static int FindTopLevelColon(String s)
    {

        var inQuote = false;
        int angle = 0, bracket = 0;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (inQuote)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inQuote = false;
                continue;
            }

            switch (c)
            {
                case '"': inQuote = true;              break;
                case '<': angle++;                     break;
                case '>': if (angle   > 0) angle--;    break;
                case '[': bracket++;                   break;
                case ']': if (bracket > 0) bracket--;  break;
                case ':': if (angle == 0 && bracket == 0) return i; break;
            }
        }

        return -1;

    }

    // Index of the '@' separating local-part and domain (first one outside quotes).
    private static int FindSeparatorAt(String s)
    {

        var inQuote = false;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (inQuote)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inQuote = false;
                continue;
            }

            if (c == '"') inQuote = true;
            else if (c == '@') return i;
        }

        return -1;

    }

    #endregion

}
