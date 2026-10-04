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
using System.Text;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// One recipient's part of a DSN: its per-recipient fields (RFC 3464 §2.3).
/// </summary>
/// <param name="Recipient">The recipient address from the envelope (Final-Recipient).</param>
/// <param name="Action">What became of the message for this recipient.</param>
/// <param name="Status">The status code (RFC 3463), e.g. "5.1.1".</param>
/// <param name="OriginalRecipient">The ORCPT parameter as received, or null when there was none.</param>
/// <param name="RemoteMta">The next hop that answered, when there was one.</param>
/// <param name="DiagnosticCode">The diagnostic, type and all: "smtp; 550 5.1.1 No such user".</param>
/// <param name="LastAttemptDate">When delivery was last attempted.</param>
/// <param name="WillRetryUntil">For a delay: until when delivery will be attempted.</param>
public sealed record DsnRecipient(String           Recipient,
                                  DsnAction        Action,
                                  String           Status,
                                  String?          OriginalRecipient   = null,
                                  String?          RemoteMta           = null,
                                  String?          DiagnosticCode      = null,
                                  DateTimeOffset?  LastAttemptDate     = null,
                                  DateTimeOffset?  WillRetryUntil      = null);


/// <summary>
/// Generates DSN messages per RFC 3464: a multipart/report (RFC 6522) with a human-readable
/// part, a message/delivery-status part, and the returned message or its header.
/// </summary>
public sealed partial class DsnGenerator
{

    private readonly SMTPServerConfig _config;

    /// <summary>
    /// A message above this size is returned with its header only, even for a failure with RET=FULL
    /// (RFC 3461 §6.2).
    /// </summary>
    public const Int32 MaxReturnedMessageSize = 1024 * 1024;

    public DsnGenerator(SMTPServerConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Generate a DSN for delivered mail
    /// </summary>
    public string? GenerateDeliveryDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet?         ret,
        string          originalMessage)

        => recipient.ReportsSuccess
               ? Generate(originalFrom, envId, ret, Timestamp.Now, originalMessage,
                          [ new DsnRecipient(recipient.Recipient, DsnAction.Delivered, "2.0.0", recipient.OriginalRecipient, LastAttemptDate: Timestamp.Now) ])
               : null;

    /// <summary>
    /// Generate a DSN for failed delivery
    /// </summary>
    public string? GenerateFailureDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet?         ret,
        string          originalMessage,
        string          statusCode,
        string          diagnosticCode)

        => recipient.ReportsFailure
               ? Generate(originalFrom, envId, ret, Timestamp.Now, originalMessage,
                          [ new DsnRecipient(recipient.Recipient, DsnAction.Failed, statusCode, recipient.OriginalRecipient,
                                             DiagnosticCode: $"smtp; {diagnosticCode}", LastAttemptDate: Timestamp.Now) ])
               : null;

    /// <summary>
    /// Generate a DSN for delayed delivery
    /// </summary>
    public string? GenerateDelayDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet?         ret,
        string          originalMessage,
        TimeSpan        delayDuration,
        string          reason)

        => recipient.ReportsDelay
               ? Generate(originalFrom, envId, ret, Timestamp.Now - delayDuration, originalMessage,
                          [ new DsnRecipient(recipient.Recipient, DsnAction.Delayed, "4.0.0", recipient.OriginalRecipient,
                                             DiagnosticCode: $"X-Hermod; {reason}", LastAttemptDate: Timestamp.Now) ])
               : null;

    /// <summary>
    /// Generate a DSN for relayed mail
    /// </summary>
    public string? GenerateRelayDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet?         ret,
        string          originalMessage,
        string          relayedTo)

        => recipient.ReportsSuccess
               ? Generate(originalFrom, envId, ret, Timestamp.Now, originalMessage,
                          [ new DsnRecipient(recipient.Recipient, DsnAction.Relayed, "2.0.0", recipient.OriginalRecipient,
                                             RemoteMta: relayedTo, LastAttemptDate: Timestamp.Now) ])
               : null;


    /// <summary>
    /// A DSN on one message for one or more of its recipients (RFC 3461 §6, RFC 3464).
    /// </summary>
    /// <param name="OriginalFrom">The reverse-path of the message: who the report goes to.</param>
    /// <param name="EnvId">The ENVID the message was received with, as xtext; null when there was none.</param>
    /// <param name="Ret">The RET the message was received with; null when there was none.</param>
    /// <param name="ArrivalDate">When the message arrived here.</param>
    /// <param name="OriginalMessage">The message, CR LF line ends.</param>
    /// <param name="Recipients">The recipients the report is about.</param>
    public String Generate(String                       OriginalFrom,
                           String?                      EnvId,
                           DsnRet?                      Ret,
                           DateTimeOffset               ArrivalDate,
                           String                       OriginalMessage,
                           IReadOnlyList<DsnRecipient>  Recipients)
    {

        if (Recipients.Count == 0)
            throw new ArgumentException("A DSN reports on at least one recipient.", nameof(Recipients));

        // RFC 5322 §3.6.4: every message its own Message-ID; RFC 2046 §5.1.1: a boundary of bchars.
        var unique    = UUIDv7.Generate().ToString("N");
        var boundary  = $"=_dsn_{unique}";
        var now       = Timestamp.Now;
        var failed    = Recipients.Any(recipient => recipient.Action == DsnAction.Failed);
        var action    = failed ? DsnAction.Failed : Recipients[0].Action;
        var dsn       = new StringBuilder();

        void Line(String text = "")
            => dsn.Append(text).Append("\r\n");

        // Header
        Line($"From: Mail Delivery System <MAILER-DAEMON@{_config.Hostname}>");
        Line($"To: <{OriginalFrom}>");
        Line($"Subject: {GetDsnSubject(action)}");
        Line($"Date: {Rfc5322Date(now)}");
        Line($"Message-ID: <dsn.{unique}@{_config.Hostname}>");
        Line( "MIME-Version: 1.0");
        Line( "Auto-Submitted: auto-replied");
        Line($"Content-Type: multipart/report; report-type=delivery-status; boundary=\"{boundary}\"");
        Line();

        // Part 1: for people
        var explanation = Explanation(Recipients);
        Line($"--{boundary}");
        Line( "Content-Type: text/plain; charset=utf-8");
        if (explanation.Any(c => c > '\x7F'))
            Line("Content-Transfer-Encoding: 8bit");
        Line();
        dsn.Append(explanation);
        Line();

        // Part 2: for programs (RFC 3464 §2.2, §2.3)
        Line($"--{boundary}");
        Line( "Content-Type: message/delivery-status");
        Line();
        Line($"Reporting-MTA: dns; {_config.Hostname}");
        if (EnvId is not null)
            Line($"Original-Envelope-Id: {OneLine(DsnParser.DecodeXtext(EnvId))}");     // RFC 3461 §6.3 (a)
        Line($"Arrival-Date: {Rfc5322Date(ArrivalDate)}");

        foreach (var recipient in Recipients)
        {

            Line();

            // RFC 3461 §6.3 (d): from ORCPT, and only from ORCPT.
            if (recipient.OriginalRecipient is { } orcpt)
            {
                var semicolon = orcpt.IndexOf(';');
                Line(semicolon > 0
                         ? $"Original-Recipient: {orcpt[..semicolon].Trim()}; {OneLine(DsnParser.DecodeXtext(orcpt[(semicolon + 1)..].Trim()))}"
                         : $"Original-Recipient: unknown; {OneLine(DsnParser.DecodeXtext(orcpt))}");
            }

            Line($"Final-Recipient: rfc822; {OneLine(recipient.Recipient)}");
            Line($"Action: {recipient.Action.ToString().ToLowerInvariant()}");
            Line($"Status: {recipient.Status}");

            if (recipient.RemoteMta is not null)
                Line($"Remote-MTA: dns; {recipient.RemoteMta}");

            if (recipient.DiagnosticCode is not null)
                Line($"Diagnostic-Code: {OneLine(recipient.DiagnosticCode)}");

            if (recipient.LastAttemptDate is { } lastAttempt)
                Line($"Last-Attempt-Date: {Rfc5322Date(lastAttempt)}");

            if (recipient.WillRetryUntil is { } retryUntil)
                Line($"Will-Retry-Until: {Rfc5322Date(retryUntil)}");

        }

        Line();

        // Part 3: the message, or its header (RFC 3461 §4.3, §6.2; RFC 6522 §4): the whole message
        // for a failure unless RET=HDRS asked otherwise, or it is too large; else the header.
        var message      = CrLf(OriginalMessage);
        var headerEnd    = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var header       = headerEnd >= 0 ? message[..(headerEnd + 2)] : message.EndsWith("\r\n") ? message : message + "\r\n";
        var wholeMessage = failed && Ret != DsnRet.Hdrs && message.Length <= MaxReturnedMessageSize;

        Line($"--{boundary}");
        Line(wholeMessage ? "Content-Type: message/rfc822" : "Content-Type: text/rfc822-headers");
        if ((wholeMessage ? message : header).Any(c => c > '\x7F'))
            Line("Content-Transfer-Encoding: 8bit");
        Line();
        dsn.Append(wholeMessage ? message : header);
        if (wholeMessage && !message.EndsWith("\r\n"))
            Line();

        Line($"--{boundary}--");

        return dsn.ToString();

    }


    /// <summary>
    /// The status code a reply carries (RFC 3463, RFC 2034): its enhanced status code when it has
    /// one of the reply's class, else X.0.0 of the reply's class (RFC 3461 §6.3 (g)).
    /// </summary>
    /// <param name="Code">The reply code, or 0 when there was no reply.</param>
    /// <param name="Text">The reply text, with or without the code in front.</param>
    /// <param name="Permanent">For no reply at all: whether the failure is permanent.</param>
    public static String StatusOf(Int32 Code, String? Text, Boolean Permanent)
    {

        var match = EnhancedStatusCode().Match(Text ?? "");

        if (match.Success && (Code == 0 || match.Groups[1].Value[0] - '0' == Code / 100))
            return match.Groups[1].Value;

        return Code switch {
                   >= 500 => "5.0.0",
                   >= 400 => "4.0.0",
                   >= 200 and < 300 => "2.0.0",
                   _      => Permanent ? "5.0.0" : "4.0.0"
               };

    }

    /// <summary>
    /// A reply as it was received - "550 5.1.1 No such user" - from its code and text, whether
    /// the text starts with the code or not; null when there was no reply.
    /// </summary>
    public static String? Reply(Int32 Code, String? Text)
    {

        if (Code < 200 || Code > 599)
            return null;

        var text = Text ?? "";
        var code = Code.ToString(CultureInfo.InvariantCulture);

        return text.StartsWith(code + " ") || text.StartsWith(code + "-") || text == code
                   ? text
                   : $"{code} {text}".TrimEnd();

    }


    [GeneratedRegex(@"^(?:\d{3}[ -])?([245]\.\d{1,3}\.\d{1,3})(?:\s|$)")]
    private static partial Regex EnhancedStatusCode();

    // RFC 5322 §3.3 date-time, with a numeric zone (the obsolete "GMT" is not to be generated).
    private static String Rfc5322Date(DateTimeOffset timestamp)
        => timestamp.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss '+0000'", CultureInfo.InvariantCulture);

    // A field value on one line: CR and LF of a multi-line reply become spaces.
    private static String OneLine(String text)
        => Regex.Replace(text, @"\s*[\r\n]+\s*", " ").Trim();

    // CR LF line ends throughout: lone CRs and LFs become CR LF, CR LF stays as it is.
    private static String CrLf(String text)
        => Regex.Replace(text, "\r\n|\r|\n", "\r\n");

    private static string GetDsnSubject(DsnAction action)
    {
        return action switch
        {
            DsnAction.Delivered => "Successful Mail Delivery Report",
            DsnAction.Failed => "Undelivered Mail Returned to Sender",
            DsnAction.Delayed => "Warning: Mail Delivery Delayed",
            DsnAction.Relayed => "Mail Successfully Relayed",
            DsnAction.Expanded => "Mail Delivered to Mailing List",
            _ => "Delivery Status Notification"
        };
    }

    private string Explanation(IReadOnlyList<DsnRecipient> recipients)
    {

        var text = new StringBuilder();

        void Line(String line = "")
            => text.Append(line).Append("\r\n");

        Line($"This is the mail system at host {_config.Hostname}.");
        Line();

        foreach (var group in recipients.GroupBy(recipient => recipient.Action))
        {

            Line(group.Key switch {
                     DsnAction.Failed    => "Your message could not be delivered to the following recipients:",
                     DsnAction.Delayed   => "Your message has not yet been delivered to the following recipients. The mail system will keep trying; you do not need to send it again:",
                     DsnAction.Delivered => "Your message was delivered to:",
                     DsnAction.Relayed   => "Your message was relayed to a system that does not report delivery, for:",
                     DsnAction.Expanded  => "Your message was delivered to a mailing list:",
                     _                   => "Delivery status:"
                 });
            Line();

            foreach (var recipient in group)
            {
                Line($"    <{OneLine(recipient.Recipient)}>");
                if (recipient.RemoteMta is not null)
                    Line($"        Remote server: {recipient.RemoteMta}");
                if (recipient.DiagnosticCode is not null)
                    Line($"        {OneLine(recipient.DiagnosticCode)}");
                if (recipient.WillRetryUntil is { } until)
                    Line($"        Delivery will be attempted until {Rfc5322Date(until)}.");
            }

            Line();

        }

        if (recipients.Any(recipient => recipient.Action == DsnAction.Failed))
            Line("For further assistance, please send mail to postmaster, and include this report.");

        return text.ToString();

    }

}
