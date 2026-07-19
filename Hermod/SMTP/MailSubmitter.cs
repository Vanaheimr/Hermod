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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

#region Submission result

/// <summary>The outcome of a mail submission (RFC 6409) through a relay/submission server.</summary>
public enum MailSubmissionStatus
{
    /// <summary>The submission server accepted the message.</summary>
    Ok,
    /// <summary>A transient error (4xx / connection problem); the caller may retry later.</summary>
    TemporaryFailure,
    /// <summary>A permanent error (5xx); the message was rejected.</summary>
    PermanentFailure,
    /// <summary>The envelope had no recipients — nothing was sent.</summary>
    NoRecipients,
    /// <summary>No submission server is configured (e.g. a <see cref="NullMailSubmitter"/>); nothing was sent.</summary>
    NotConfigured
}

/// <summary>The result of submitting one message.</summary>
/// <param name="Status">The submission outcome.</param>
/// <param name="ResponseCode">The final SMTP response code (0 if the server was never reached).</param>
/// <param name="ResponseText">The final SMTP response text or an error description.</param>
public sealed record MailSubmissionResult(MailSubmissionStatus  Status,
                                          Int32                 ResponseCode,
                                          String                ResponseText)
{
    /// <summary>Whether the message was accepted by the submission server.</summary>
    public Boolean IsOk => Status == MailSubmissionStatus.Ok;

    public static MailSubmissionResult NoRecipients   => new (MailSubmissionStatus.NoRecipients,  0, "no recipients");
    public static MailSubmissionResult NotConfigured  => new (MailSubmissionStatus.NotConfigured, 0, "no submission server configured");

}

#endregion

#region IMailSubmitter

/// <summary>
/// Submits a composed message to a configured relay/submission server (RFC 6409) — i.e. the
/// "send my mail through my outgoing server (with AUTH)" role, synchronous and status-returning.
/// This is the interface applications (e.g. an HTTP API) depend on to send transactional mail;
/// inject a <see cref="MailSubmitter"/> in production or a <see cref="NullMailSubmitter"/> in tests.
///
/// Contrast with <see cref="MailSender"/>, which is the MTA outbound path (queue + direct-MX
/// delivery); this facade instead hands the message to one configured server and returns its verdict.
/// </summary>
public interface IMailSubmitter
{

    /// <summary>
    /// Submit the given e-mail envelope and return the server's verdict. The envelope carries the
    /// transaction parameters (DSN request, MT-PRIORITY, REQUIRETLS).
    /// </summary>
    Task<MailSubmissionResult> SubmitAsync(EMailEnvelop       EMailEnvelop,
                                           CancellationToken  CancellationToken  = default);

    /// <summary>
    /// Submit the given e-mail (envelope derived from its From/To headers). To set transaction
    /// parameters, construct an <see cref="EMailEnvelop"/> instead.
    /// </summary>
    Task<MailSubmissionResult> SubmitAsync(EMail              EMail,
                                           CancellationToken  CancellationToken  = default);

}

#endregion

#region MailSubmitter

/// <summary>Configuration for a <see cref="MailSubmitter"/>.</summary>
/// <param name="RelayHost">The submission/relay server host name.</param>
/// <param name="RelayPort">The submission port (587 for RFC 6409 submission; 25/465 also possible).</param>
/// <param name="Username">Optional SASL username. When set, the submitter authenticates.</param>
/// <param name="Password">Optional SASL password.</param>
/// <param name="LocalHostname">The name used in EHLO.</param>
/// <param name="RequireTls">Require STARTTLS and a valid certificate; defer/fail instead of sending in the clear.</param>
public sealed record MailSubmitterConfig(String    RelayHost,
                                         UInt16    RelayPort       = 587,
                                         String?   Username        = null,
                                         String?   Password        = null,
                                         String    LocalHostname   = "localhost",
                                         Boolean   RequireTls      = true);

/// <summary>
/// The default <see cref="IMailSubmitter"/>: hands each message to one configured submission server
/// (STARTTLS + optional SASL AUTH) via the shared outbound SMTP engine in smarthost mode.
/// </summary>
public sealed class MailSubmitter : IMailSubmitter
{

    private readonly MailSubmitterConfig  config;
    private readonly SMTPOutboundClient   client;
    private readonly ILogger              logger;

    /// <summary>
    /// Create a new submitter for the given relay/submission server.
    /// </summary>
    public MailSubmitter(MailSubmitterConfig  Config,
                         DNSClient            DNSClient,
                         ILogger              Logger,
                         DkimSigner?          DkimSigner   = null)
    {

        this.config  = Config;
        this.logger  = Logger;
        this.client  = new SMTPOutboundClient(
                           new SmtpOutboundConfig {
                               LocalHostname      = Config.LocalHostname,
                               SmartHost          = Config.RelayHost,     // smarthost mode: no MX lookup, connect here
                               SmartHostPort      = Config.RelayPort,
                               SmartHostUsername  = Config.Username,
                               SmartHostPassword  = Config.Password,
                               RequireStartTls    = Config.RequireTls,
                               PreferStartTls     = true
                           },
                           DkimSigner,
                           DNSClient,
                           Logger
                       );

    }


    public async Task<MailSubmissionResult> SubmitAsync(EMailEnvelop       EMailEnvelop,
                                                        CancellationToken  CancellationToken  = default)
    {

        var from = EMailEnvelop.MailFrom.FirstOrDefault()?.Address.ToString() ?? "";

        // All recipients go to the relay in one transaction — it sorts out onward delivery.
        var recipients = EMailEnvelop.RcptTo.Select(rcpt => rcpt.Address.ToString()).ToArray();
        if (recipients.Length == 0)
            return MailSubmissionResult.NoRecipients;

        var messageContent = String.Join("\r\n", EMailEnvelop.Mail.ToText());

        // In smarthost mode the targetDomain is only informational (the connection goes to the
        // configured relay); pass the relay host. TLS is required if either the submitter is
        // configured to demand it or this specific envelope does (REQUIRETLS).
        var result = await client.SendAsync(
                               config.RelayHost,
                               from,
                               recipients,
                               messageContent,
                               config.RequireTls || EMailEnvelop.RequireTls,
                               EMailEnvelop.Dsn,
                               EMailEnvelop.Priority,
                               CancellationToken
                           ).ConfigureAwait(false);

        var status = result.Status switch {
            SendStatus.Success  => MailSubmissionStatus.Ok,
            SendStatus.PermFail => MailSubmissionStatus.PermanentFailure,
            _                   => MailSubmissionStatus.TemporaryFailure
        };

        logger.Log(status == MailSubmissionStatus.Ok ? LogLevel.Info : LogLevel.Warning,
                   $"MailSubmitter: {status} via {config.RelayHost} ({result.ResponseCode} {result.ResponseText})");

        return new MailSubmissionResult(status, result.ResponseCode, result.ResponseText);

    }


    public Task<MailSubmissionResult> SubmitAsync(EMail              EMail,
                                                  CancellationToken  CancellationToken  = default)

        => SubmitAsync(new EMailEnvelop(EMail), CancellationToken);

}

#endregion

#region NullMailSubmitter

/// <summary>
/// A no-op <see cref="IMailSubmitter"/> (the counterpart of the legacy <c>NullMailer</c>): accepts
/// every message and sends nothing. Use it where mail delivery is not configured or in tests.
/// </summary>
public sealed class NullMailSubmitter : IMailSubmitter
{

    private readonly ILogger? logger;

    public NullMailSubmitter(ILogger? Logger = null)
    {
        this.logger = Logger;
    }

    public Task<MailSubmissionResult> SubmitAsync(EMailEnvelop EMailEnvelop, CancellationToken CancellationToken = default)
    {
        logger?.Log(LogLevel.Debug, $"NullMailSubmitter: dropped a message to {EMailEnvelop.RcptTo.Count()} recipient(s)");
        return Task.FromResult(MailSubmissionResult.NotConfigured);
    }

    public Task<MailSubmissionResult> SubmitAsync(EMail EMail, CancellationToken CancellationToken = default)
        => SubmitAsync(new EMailEnvelop(EMail), CancellationToken);

}

#endregion
