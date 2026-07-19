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

#region DSN Enums

/// <summary>
/// NOTIFY parameter values (RFC 3461)
/// </summary>
[Flags]
public enum DsnNotify
{
    Never   = 0,
    Success = 1,
    Failure = 2,
    Delay   = 4
}

/// <summary>
/// RET parameter values (RFC 3461)
/// </summary>
public enum DsnRet
{
    Full,   // Return full message
    Hdrs    // Return headers only
}

/// <summary>
/// DSN Action values (RFC 3464)
/// </summary>
public enum DsnAction
{
    Failed,
    Delayed,
    Delivered,
    Relayed,
    Expanded
}

#endregion

#region DSN Request

/// <summary>
/// DSN parameters from MAIL FROM and RCPT TO
/// </summary>
public sealed class DsnRequest
{
    // From MAIL FROM
    public string?      EnvId       { get; set; }   // ENVID=
    public DsnRet       Ret         { get; set; } = DsnRet.Full;
    
    // Per-recipient from RCPT TO
    public DsnNotify    Notify      { get; set; } = DsnNotify.Failure;
    public string?      OriginalRecipient { get; set; }  // ORCPT=
}

/// <summary>
/// Per-recipient DSN settings
/// </summary>
public sealed class RecipientDsn
{
    public required string      Recipient           { get; init; }
    public          DsnNotify   Notify              { get; init; } = DsnNotify.Failure;
    public          string?     OriginalRecipient   { get; init; }
}

#endregion

#region DSN outbound request

/// <summary>
/// DSN parameters a sender attaches to an outgoing message (RFC 3461) to request delivery status
/// notifications from the receiving server.
/// </summary>
public sealed record DsnParameters(DsnNotify   Notify   = DsnNotify.Never,
                                   DsnRet      Ret      = DsnRet.Full,
                                   string?     EnvId    = null)
{

    /// <summary>No DSN requested — the receiver applies its default (usually failures only).</summary>
    public static readonly DsnParameters None = new (DsnNotify.Never);

    /// <summary>Whether a DSN was actually requested (a NOTIFY other than Never, or an ENVID).</summary>
    public bool IsRequested => Notify != DsnNotify.Never || EnvId is not null;

}

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
    /// Build a MAIL FROM command, appending RET/ENVID when the remote supports DSN and one was requested.
    /// </summary>
    public static string MailFrom(string envelopeFrom, DsnParameters dsn, bool remoteSupportsDsn)
    {
        var cmd = $"MAIL FROM:<{envelopeFrom}>";

        if (!remoteSupportsDsn || !dsn.IsRequested)
            return cmd;

        cmd += dsn.Ret == DsnRet.Hdrs ? " RET=HDRS" : " RET=FULL";

        if (dsn.EnvId is not null)
            cmd += $" ENVID={dsn.EnvId}";

        return cmd;
    }

    /// <summary>
    /// Build a RCPT TO command, appending NOTIFY/ORCPT when the remote supports DSN and one was requested.
    /// </summary>
    public static string RcptTo(string recipient, DsnParameters dsn, bool remoteSupportsDsn)
    {
        var cmd = $"RCPT TO:<{recipient}>";

        if (!remoteSupportsDsn || dsn.Notify == DsnNotify.Never)
            return cmd;

        cmd += " NOTIFY=" + FormatNotify(dsn.Notify);
        cmd += $" ORCPT=rfc822;{recipient}";

        return cmd;
    }

}

#endregion

#region DSN Generator

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
        var messageId = $"<dsn.{Guid.NewGuid():N}@{_config.Hostname}>";
        var date = DateTime.UtcNow.ToString("R");
        var boundary = $"=_dsn_{Guid.NewGuid():N}";

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

#endregion

#region DSN Parser

/// <summary>
/// Parse DSN parameters from SMTP commands
/// </summary>
public static class DsnParser
{
    /// <summary>
    /// Parse MAIL FROM parameters for DSN
    /// </summary>
    public static (string? EnvId, DsnRet Ret) ParseMailFromParams(string parameters)
    {
        string? envId = null;
        var ret = DsnRet.Full;

        var parts = parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("ENVID=", StringComparison.OrdinalIgnoreCase))
            {
                envId = part[6..];
            }
            else if (part.StartsWith("RET=", StringComparison.OrdinalIgnoreCase))
            {
                ret = part[4..].ToUpperInvariant() switch
                {
                    "HDRS" => DsnRet.Hdrs,
                    "FULL" => DsnRet.Full,
                    _ => DsnRet.Full
                };
            }
        }

        return (envId, ret);
    }

    /// <summary>
    /// Parse RCPT TO parameters for DSN
    /// </summary>
    public static (DsnNotify Notify, string? Orcpt) ParseRcptToParams(string parameters)
    {
        var notify = DsnNotify.Failure; // Default
        string? orcpt = null;

        var parts = parameters.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith("NOTIFY=", StringComparison.OrdinalIgnoreCase))
            {
                notify = ParseNotifyValue(part[7..]);
            }
            else if (part.StartsWith("ORCPT=", StringComparison.OrdinalIgnoreCase))
            {
                orcpt = part[6..];
            }
        }

        return (notify, orcpt);
    }

    private static DsnNotify ParseNotifyValue(string value)
    {
        if (value.Equals("NEVER", StringComparison.OrdinalIgnoreCase))
            return DsnNotify.Never;

        var notify = DsnNotify.Never;
        var parts = value.Split(',');
        
        foreach (var part in parts)
        {
            notify |= part.Trim().ToUpperInvariant() switch
            {
                "SUCCESS" => DsnNotify.Success,
                "FAILURE" => DsnNotify.Failure,
                "DELAY" => DsnNotify.Delay,
                _ => DsnNotify.Never
            };
        }

        return notify;
    }
}

#endregion

#region REQUIRETLS

/// <summary>
/// REQUIRETLS support (RFC 8689)
/// When a message has REQUIRETLS, it must only be transmitted over TLS
/// and the receiving server must also support REQUIRETLS.
/// </summary>
public sealed class RequireTlsHandler
{
    private readonly ILogger _logger;

    public RequireTlsHandler(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if REQUIRETLS is present in MAIL FROM
    /// </summary>
    public static bool ParseRequireTls(string mailFromParams)
    {
        return mailFromParams.Contains("REQUIRETLS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate that REQUIRETLS requirements are met for sending
    /// </summary>
    public bool ValidateForSending(
        bool        requireTls,
        bool        tlsActive,
        MtaStsPolicy mtaStsPolicy)
    {
        if (!requireTls)
            return true;

        // Must have TLS active on current connection
        if (!tlsActive)
        {
            _logger.Log(LogLevel.Warning, "REQUIRETLS: Connection is not TLS-secured");
            return false;
        }

        // Must have MTA-STS policy in enforce mode (or no policy check if we trust the hop)
        if (mtaStsPolicy.Mode == MtaStsMode.Enforce)
        {
            return true;
        }

        _logger.Log(LogLevel.Warning, "REQUIRETLS: Remote domain doesn't have MTA-STS enforce policy");
        return false;
    }

    /// <summary>
    /// Add REQUIRETLS to outbound message if it was on the inbound
    /// </summary>
    public static string PropagateRequireTls(string mailFromCommand, bool requireTls)
    {
        if (!requireTls)
            return mailFromCommand;

        if (mailFromCommand.Contains("REQUIRETLS", StringComparison.OrdinalIgnoreCase))
            return mailFromCommand;

        // Add REQUIRETLS before the closing >
        return mailFromCommand.TrimEnd() + " REQUIRETLS";
    }
}

#endregion
