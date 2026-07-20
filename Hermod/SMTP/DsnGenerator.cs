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
/// Generates DSN messages per RFC 3464
/// </summary>
public sealed class DsnGenerator
{
    private readonly SMTPServerConfig _config;

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
        DsnRet          ret,
        string          originalMessage)
    {
        if (!recipient.Notify.HasFlag(DsnNotify.Success))
            return null;

        return GenerateDsn(
            originalFrom,
            recipient,
            envId,
            ret,
            originalMessage,
            DsnAction.Delivered,
            "2.0.0",
            "Message delivered successfully"
        );
    }

    /// <summary>
    /// Generate a DSN for failed delivery
    /// </summary>
    public string? GenerateFailureDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet          ret,
        string          originalMessage,
        string          statusCode,
        string          diagnosticCode)
    {
        if (!recipient.Notify.HasFlag(DsnNotify.Failure))
            return null;

        return GenerateDsn(
            originalFrom,
            recipient,
            envId,
            ret,
            originalMessage,
            DsnAction.Failed,
            statusCode,
            diagnosticCode
        );
    }

    /// <summary>
    /// Generate a DSN for delayed delivery
    /// </summary>
    public string? GenerateDelayDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet          ret,
        string          originalMessage,
        TimeSpan        delayDuration,
        string          reason)
    {
        if (!recipient.Notify.HasFlag(DsnNotify.Delay))
            return null;

        return GenerateDsn(
            originalFrom,
            recipient,
            envId,
            ret,
            originalMessage,
            DsnAction.Delayed,
            "4.0.0",
            $"Message delayed for {delayDuration.TotalHours:F1} hours: {reason}"
        );
    }

    /// <summary>
    /// Generate a DSN for relayed mail
    /// </summary>
    public string? GenerateRelayDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet          ret,
        string          originalMessage,
        string          relayedTo)
    {
        if (!recipient.Notify.HasFlag(DsnNotify.Success))
            return null;

        return GenerateDsn(
            originalFrom,
            recipient,
            envId,
            ret,
            originalMessage,
            DsnAction.Relayed,
            "2.0.0",
            $"Message relayed to {relayedTo}"
        );
    }

    private string GenerateDsn(
        string          originalFrom,
        RecipientDsn    recipient,
        string?         envId,
        DsnRet          ret,
        string          originalMessage,
        DsnAction       action,
        string          status,
        string          diagnosticCode)
    {
        var messageId = $"<dsn.{UUIDv7.Generate():N}@{_config.Hostname}>";
        var date = Timestamp.Now.ToString("R");
        var boundary = $"=_dsn_{UUIDv7.Generate():N}";

        var sb = new StringBuilder();

        // Message headers
        sb.AppendLine($"From: Mail Delivery Subsystem <MAILER-DAEMON@{_config.Hostname}>");
        sb.AppendLine($"To: {originalFrom}");
        sb.AppendLine($"Subject: {GetDsnSubject(action)}");
        sb.AppendLine($"Date: {date}");
        sb.AppendLine($"Message-ID: {messageId}");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Auto-Submitted: auto-replied");
        sb.AppendLine($"Content-Type: multipart/report; report-type=delivery-status; boundary=\"{boundary}\"");
        sb.AppendLine();

        // Part 1: Human-readable explanation
        sb.AppendLine($"--{boundary}");
        sb.AppendLine("Content-Type: text/plain; charset=utf-8");
        sb.AppendLine();
        sb.AppendLine(GetHumanReadableExplanation(action, recipient.Recipient, diagnosticCode));
        sb.AppendLine();

        // Part 2: Machine-readable delivery-status (RFC 3464)
        sb.AppendLine($"--{boundary}");
        sb.AppendLine("Content-Type: message/delivery-status");
        sb.AppendLine();
        
        // Per-message fields
        sb.AppendLine($"Reporting-MTA: dns; {_config.Hostname}");
        if (envId is not null)
            sb.AppendLine($"Original-Envelope-Id: {envId}");
        sb.AppendLine($"Arrival-Date: {date}");
        sb.AppendLine();
        
        // Per-recipient fields
        sb.AppendLine($"Final-Recipient: rfc822; {recipient.Recipient}");
        if (recipient.OriginalRecipient is not null)
            sb.AppendLine($"Original-Recipient: {recipient.OriginalRecipient}");
        sb.AppendLine($"Action: {action.ToString().ToLowerInvariant()}");
        sb.AppendLine($"Status: {status}");
        sb.AppendLine($"Diagnostic-Code: smtp; {diagnosticCode}");
        sb.AppendLine($"Last-Attempt-Date: {date}");
        sb.AppendLine();

        // Part 3: Original message (or headers only)
        sb.AppendLine($"--{boundary}");
        if (ret == DsnRet.Hdrs)
        {
            sb.AppendLine("Content-Type: text/rfc822-headers");
            sb.AppendLine();
            sb.AppendLine(ExtractHeaders(originalMessage));
        }
        else
        {
            sb.AppendLine("Content-Type: message/rfc822");
            sb.AppendLine();
            sb.AppendLine(originalMessage);
        }

        sb.AppendLine($"--{boundary}--");

        return sb.ToString().ReplaceLineEndings("\r\n");
    }

    private static string GetDsnSubject(DsnAction action)
    {
        return action switch
        {
            DsnAction.Delivered => "Successful Mail Delivery Report",
            DsnAction.Failed => "Undeliverable: Mail Delivery Failed",
            DsnAction.Delayed => "Warning: Mail Delivery Delayed",
            DsnAction.Relayed => "Mail Successfully Relayed",
            DsnAction.Expanded => "Mail Delivered to Mailing List",
            _ => "Delivery Status Notification"
        };
    }

    private string GetHumanReadableExplanation(DsnAction action, string recipient, string diagnostic)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"This is an automatically generated Delivery Status Notification.");
        sb.AppendLine($"This message was created by the mail system at {_config.Hostname}.");
        sb.AppendLine();

        switch (action)
        {
            case DsnAction.Delivered:
                sb.AppendLine($"Your message was successfully delivered to:");
                sb.AppendLine($"    {recipient}");
                break;
                
            case DsnAction.Failed:
                sb.AppendLine($"Delivery to the following recipient failed permanently:");
                sb.AppendLine($"    {recipient}");
                sb.AppendLine();
                sb.AppendLine($"Technical details:");
                sb.AppendLine($"    {diagnostic}");
                break;
                
            case DsnAction.Delayed:
                sb.AppendLine($"Delivery to the following recipient has been delayed:");
                sb.AppendLine($"    {recipient}");
                sb.AppendLine();
                sb.AppendLine($"The mail system will continue to try to deliver your message.");
                sb.AppendLine($"You do not need to resend your message.");
                sb.AppendLine();
                sb.AppendLine($"Reason for delay:");
                sb.AppendLine($"    {diagnostic}");
                break;
                
            case DsnAction.Relayed:
                sb.AppendLine($"Your message was relayed to:");
                sb.AppendLine($"    {recipient}");
                sb.AppendLine();
                sb.AppendLine($"Delivery confirmation is not available for relayed messages.");
                break;
                
            case DsnAction.Expanded:
                sb.AppendLine($"Your message was delivered to a mailing list:");
                sb.AppendLine($"    {recipient}");
                break;
        }

        return sb.ToString();
    }

    private static string ExtractHeaders(string message)
    {
        var headerEnd = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headerEnd < 0)
            headerEnd = message.IndexOf("\n\n", StringComparison.Ordinal);
        
        return headerEnd > 0 ? message[..headerEnd] : message;
    }
}
