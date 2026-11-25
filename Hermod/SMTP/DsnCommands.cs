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

using org.GraphDefined.Vanaheimr.Illias;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// Builds RFC 3461 MAIL FROM / RCPT TO parameter strings for outbound DSN requests. Parameters are
/// only appended when the remote server advertised the DSN extension and a request is present.
/// </summary>
public static class DsnCommands
{

    /// <summary>
    /// Format a NOTIFY value per RFC 3461: NEVER, or a comma-separated list of SUCCESS/FAILURE/DELAY.
    /// </summary>
    public static string FormatNotify(DsnNotify notify)
    {
        if (notify == DsnNotify.Never)
            return "NEVER";

        var parts = new List<string>();
        if (notify.HasFlag(DsnNotify.Success)) parts.Add("SUCCESS");
        if (notify.HasFlag(DsnNotify.Failure)) parts.Add("FAILURE");
        if (notify.HasFlag(DsnNotify.Delay))   parts.Add("DELAY");

        return parts.Count > 0 ? string.Join(",", parts) : "NEVER";
    }

    /// <summary>
    /// The RET/ENVID parameter suffix for a MAIL FROM command (e.g. " RET=FULL ENVID=abc"), or "" when
    /// the remote does not support DSN or nothing was requested. Append to an existing MAIL FROM command.
    /// </summary>
    public static string MailFromParams(DsnParameters dsn, bool remoteSupportsDsn)
    {
        if (!remoteSupportsDsn || !dsn.IsRequested)
            return "";

        // RFC 3461 §5.2.1 (a), (b): RET and ENVID as they were given - and only then.
        var s = dsn.Ret switch {
                    DsnRet.Hdrs  => " RET=HDRS",
                    DsnRet.Full  => " RET=FULL",
                    _            => ""
                };

        // RFC 3461 §4.4: "envid-parameter = 'ENVID=' xtext". An identifier that cannot be one is
        // the caller's to refuse before MAIL; here it never makes a command line of its own.
        if (dsn.EnvIdError is { } envIdError)
            throw new ArgumentException(envIdError, nameof(dsn));

        if (dsn.EnvIdXText is { } envId)
            s += $" ENVID={envId}";

        return s;
    }

    /// <summary>
    /// The NOTIFY/ORCPT parameter suffix for a RCPT TO command, or "" when the remote does not support
    /// DSN or nothing was requested. Append to an existing RCPT TO command.
    /// </summary>
    /// <param name="smtpUtf8">Whether the transaction is SMTPUTF8 (MAIL had the parameter): a UTF-8 address may then be written in UTF-8.</param>
    public static string RcptToParams(DsnParameters dsn, string recipient, bool remoteSupportsDsn, bool smtpUtf8 = false)
    {

        // RFC 3461 §4.1: no NOTIFY is the server's default; NOTIFY=NEVER "requests that a DSN not
        // be returned to the sender under any conditions" - a request like any other.
        if (!remoteSupportsDsn || dsn.Notify is not { } notify)
            return "";

        return " NOTIFY=" + FormatNotify(notify) + " ORCPT=" + OriginalRecipientOf(recipient, smtpUtf8);

    }

    /// <summary>
    /// The ORCPT value for an address (RFC 3461 §4.2): original-recipient-address = addr-type
    /// ";" xtext. An ASCII address is "rfc822;" and its xtext - "+" is "+2B", "=" is "+3D" (§4);
    /// one with UTF-8 in it is of the UTF-8 address type (RFC 6533 §3): "utf-8;" and the address
    /// with "\x{HEXPOINT}" for each character outside QCHAR - for each character outside ASCII
    /// too, unless the transaction is SMTPUTF8 ("When sending data to an SMTP server that does not
    /// advertise SMTPUTF8, then the EmbeddedUnicodeChar syntax MUST be used instead of UTF-8").
    /// </summary>
    public static string OriginalRecipientOf(string address, bool smtpUtf8 = false)
    {

        if (address.All(c => c < 0x80))
            return "rfc822;" + XText(address);

        var value = new StringBuilder("utf-8;");

        foreach (var rune in address.EnumerateRunes())
        {
            if (rune.Value is >= 0x21 and <= 0x7E and not ('+' or '=' or '\\') || rune.Value > 0x7F && smtpUtf8)
                value.Append(rune.ToString());
            else
                value.Append(EmbeddedUnicodeChar(rune.Value));
        }

        return value.ToString();

    }

    /// <summary>
    /// RFC 3461 §4: xtext - "!" to "~" as they are but "+" and "=", every other octet "+" and
    /// two upper-case hex digits.
    /// </summary>
    public static string XText(string text)
    {
        var xtext = new StringBuilder();
        foreach (var octet in Encoding.UTF8.GetBytes(text))
            xtext.Append(octet is >= 0x21 and <= 0x7E and not ((byte) '+' or (byte) '=')
                             ? ((char) octet).ToString()
                             : $"+{octet:X2}");
        return xtext.ToString();
    }

    // RFC 6533 §3: EmbeddedUnicodeChar = %x5C.78 "{" HEXPOINT "}", HEXPOINT 2 to 6 hex digits,
    // two for what is below 0x100, no leading zero above.
    private static string EmbeddedUnicodeChar(int codePoint)
        => codePoint < 0x100 ? $"\\x{{{codePoint:X2}}}" : $"\\x{{{codePoint:X}}}";

    /// <summary>
    /// A received ORCPT as it may go to this next hop: as it came (RFC 3461 §5.2.1 (d): "an
    /// ORCPT parameter with the same value MUST be specified"), except that the UTF-8 in one of
    /// the UTF-8 address type becomes "\x{HEXPOINT}" when the transaction is not SMTPUTF8 (RFC
    /// 6533 §3) - the same address in its 7-bit form.
    /// </summary>
    private static string Relayable(string orcpt, bool smtpUtf8)
    {

        if (smtpUtf8 || orcpt.All(c => c < 0x80))
            return orcpt;

        var semicolon = orcpt.IndexOf(';');
        var value     = new StringBuilder(orcpt[..(semicolon + 1)]);

        foreach (var rune in orcpt[(semicolon + 1)..].EnumerateRunes())
            value.Append(rune.Value > 0x7F ? EmbeddedUnicodeChar(rune.Value) : rune.ToString());

        return value.ToString();

    }

    /// <summary>
    /// The NOTIFY/ORCPT parameter suffix for relaying one recipient as it was received (RFC 3461
    /// §5.2.1), or "" when the remote does not support DSN. NOTIFY=NEVER is a value the client
    /// chose and is passed on like any other, and a NOTIFY the client did not give is not made
    /// up (§5.2.1 (c)); a received ORCPT is passed on unchanged, and only in its absence is one
    /// built from the recipient (§5.2.1 (d)).
    /// </summary>
    /// <param name="smtpUtf8">Whether the transaction is SMTPUTF8 (MAIL had the parameter).</param>
    public static string RcptToParams(RecipientDsn recipient, bool remoteSupportsDsn, bool smtpUtf8 = false)
    {
        if (!remoteSupportsDsn)
            return "";

        return (recipient.Notify is { } notify ? " NOTIFY=" + FormatNotify(notify) : "") +
               " ORCPT="  + (recipient.OriginalRecipient is { } orcpt
                                 ? Relayable(orcpt, smtpUtf8)
                                 : OriginalRecipientOf(recipient.Recipient, smtpUtf8));
    }

    /// <summary>
    /// Build a full MAIL FROM command, appending RET/ENVID when the remote supports DSN and one was requested.
    /// </summary>
    public static string MailFrom(string envelopeFrom, DsnParameters dsn, bool remoteSupportsDsn)
        => $"MAIL FROM:<{envelopeFrom}>" + MailFromParams(dsn, remoteSupportsDsn);

    /// <summary>
    /// Build a full RCPT TO command, appending NOTIFY/ORCPT when the remote supports DSN and one was requested.
    /// </summary>
    public static string RcptTo(string recipient, DsnParameters dsn, bool remoteSupportsDsn, bool smtpUtf8 = false)
        => $"RCPT TO:<{recipient}>" + RcptToParams(dsn, recipient, remoteSupportsDsn, smtpUtf8);

}
