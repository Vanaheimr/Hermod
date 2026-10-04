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

        if (dsn.EnvId is not null)
            s += $" ENVID={dsn.EnvId}";

        return s;
    }

    /// <summary>
    /// The NOTIFY/ORCPT parameter suffix for a RCPT TO command, or "" when the remote does not support
    /// DSN or nothing was requested. Append to an existing RCPT TO command.
    /// </summary>
    public static string RcptToParams(DsnParameters dsn, string recipient, bool remoteSupportsDsn)
    {
        if (!remoteSupportsDsn || dsn.Notify == DsnNotify.Never)
            return "";

        return " NOTIFY=" + FormatNotify(dsn.Notify) + $" ORCPT=rfc822;{recipient}";
    }

    /// <summary>
    /// The NOTIFY/ORCPT parameter suffix for relaying one recipient as it was received (RFC 3461
    /// §5.2.1), or "" when the remote does not support DSN. NOTIFY=NEVER is a value the client
    /// chose and is passed on like any other, and a NOTIFY the client did not give is not made
    /// up (§5.2.1 (c)); a received ORCPT is passed on unchanged, and only in its absence is one
    /// built from the recipient (§5.2.1 (d)).
    /// </summary>
    public static string RcptToParams(RecipientDsn recipient, bool remoteSupportsDsn)
    {
        if (!remoteSupportsDsn)
            return "";

        return (recipient.Notify is { } notify ? " NOTIFY=" + FormatNotify(notify) : "") +
               " ORCPT="  + (recipient.OriginalRecipient ?? $"rfc822;{recipient.Recipient}");
    }

    /// <summary>
    /// Build a full MAIL FROM command, appending RET/ENVID when the remote supports DSN and one was requested.
    /// </summary>
    public static string MailFrom(string envelopeFrom, DsnParameters dsn, bool remoteSupportsDsn)
        => $"MAIL FROM:<{envelopeFrom}>" + MailFromParams(dsn, remoteSupportsDsn);

    /// <summary>
    /// Build a full RCPT TO command, appending NOTIFY/ORCPT when the remote supports DSN and one was requested.
    /// </summary>
    public static string RcptTo(string recipient, DsnParameters dsn, bool remoteSupportsDsn)
        => $"RCPT TO:<{recipient}>" + RcptToParams(dsn, recipient, remoteSupportsDsn);

}
