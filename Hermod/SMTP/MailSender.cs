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

using org.GraphDefined.Vanaheimr.Hermod.Mail;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP;

/// <summary>
/// The ergonomic entry point for sending mail. Accepts a fully-composed, typed
/// <see cref="EMail"/> / <see cref="EMailEnvelop"/> (built with the rich
/// <c>Hermod.Mail</c> builders — HTML, multipart, attachments, OpenPGP signing/encryption),
/// serializes it to the RFC 5322 wire format, splits the recipients by domain, and hands each
/// group to the outbound mail queue for delivery (MX resolution, STARTTLS/DANE/MTA-STS, retries,
/// DKIM signing and DSN bounces are handled downstream by the <see cref="QueueProcessor"/>).
///
/// This is the *only* public way to submit a message: the raw-string paths
/// (<see cref="SMTPOutboundClient"/>.SendAsync and hand-crafting a <see cref="QueuedMail"/>) are
/// internal, so callers cannot accidentally enqueue an unchecked message string.
/// </summary>
public sealed class MailSender
{

    private readonly IMailQueue           mailQueue;
    private readonly SMTPOutboundClient?   directClient;
    private readonly ILogger              logger;

    /// <summary>
    /// Create a new mail sender over the given outbound queue. A <see cref="QueueProcessor"/>
    /// must be draining that same queue for the mail to actually be delivered.
    /// </summary>
    /// <param name="MailQueue">The outbound queue for asynchronous "letter" delivery (<see cref="SendAsync(EMailEnvelop, Boolean, CancellationToken)"/>).</param>
    /// <param name="Logger">A logger.</param>
    /// <param name="DirectDeliveryClient">
    /// Optional outbound client used by <see cref="SendDirectAsync(EMailEnvelop, Boolean, CancellationToken)"/> for
    /// synchronous direct-to-MX delivery. Pass the same <see cref="SMTPOutboundClient"/> the
    /// <see cref="QueueProcessor"/> uses (it is stateless per send, so sharing is safe). When null,
    /// only the queued path is available.
    /// </param>
    public MailSender(IMailQueue           MailQueue,
                      ILogger              Logger,
                      SMTPOutboundClient?  DirectDeliveryClient   = null)
    {
        this.mailQueue     = MailQueue;
        this.logger        = Logger;
        this.directClient  = DirectDeliveryClient;
    }


    #region SendAsync(EMailEnvelop, RequireTls = false, ...)

    /// <summary>
    /// Queue the given e-mail envelope for delivery. Recipients are grouped by domain and one
    /// queue item is created per domain (each targets a single MX set). Returns the queue IDs.
    /// </summary>
    /// <param name="EMailEnvelop">A composed e-mail envelope (sender, recipients, message).</param>
    /// <param name="RequireTls">Demand authenticated TLS for delivery (RFC 8689); defer instead of downgrading.</param>
    /// <param name="CancellationToken">An optional cancellation token.</param>
    public async Task<IReadOnlyList<String>> SendAsync(EMailEnvelop       EMailEnvelop,
                                                       Boolean            RequireTls          = false,
                                                       DsnParameters?     Dsn                 = null,
                                                       SByte              Priority            = 0,
                                                       CancellationToken  CancellationToken   = default)
    {

        var from = EMailEnvelop.MailFrom.FirstOrDefault()?.Address.ToString();
        if (String.IsNullOrEmpty(from))
            throw new ArgumentException("The e-mail envelope has no sender (MAIL FROM).", nameof(EMailEnvelop));

        var recipients = EMailEnvelop.RcptTo.ToList();
        if (recipients.Count == 0)
            throw new ArgumentException("The e-mail envelope has no recipients (RCPT TO).", nameof(EMailEnvelop));

        // Serialize the composed message to the RFC 5322 wire format. CRLF is forced here (never
        // Environment.NewLine); dot-stuffing is applied later by the outbound client, not now.
        var messageContent = String.Join("\r\n", EMailEnvelop.Mail.ToText());
        var dsn            = Dsn ?? DsnParameters.None;

        var ids = new List<String>();

        // A QueuedMail targets exactly one recipient domain, so fan the recipients out per domain.
        foreach (var byDomain in recipients.GroupBy(rcpt => rcpt.Address.Domain, StringComparer.OrdinalIgnoreCase))
        {

            var domain = byDomain.Key.TrimEnd('.').ToLowerInvariant();
            var to     = byDomain.Select(rcpt => rcpt.Address.ToString()).ToArray();
            var id     = Guid.NewGuid().ToString("N");

            await mailQueue.EnqueueAsync(new QueuedMail {
                Id              = id,
                EnvelopeFrom    = from,
                EnvelopeTo      = to,
                MessageContent  = messageContent,
                TargetDomain    = domain,
                RequireTls      = RequireTls,
                Notify          = dsn.Notify,
                Ret             = dsn.Ret,
                EnvId           = dsn.EnvId,
                Priority        = Priority
            }, CancellationToken).ConfigureAwait(false);

            ids.Add(id);
            logger.Log(LogLevel.Info, $"MailSender: queued {id} to {domain} ({to.Length} recipient(s))");

        }

        return ids;

    }

    #endregion

    #region SendAsync(EMail, RequireTls = false, ...)

    /// <summary>
    /// Queue the given e-mail for delivery, deriving the SMTP envelope from its <c>From</c>/<c>To</c>
    /// headers. The rich <c>Hermod.Mail</c> builders (e.g. <see cref="HTMLEMailBuilder"/>) convert
    /// implicitly to <see cref="EMail"/>, so a builder can be passed directly.
    /// </summary>
    public Task<IReadOnlyList<String>> SendAsync(EMail              EMail,
                                                 Boolean            RequireTls          = false,
                                                 DsnParameters?     Dsn                 = null,
                                                 SByte              Priority            = 0,
                                                 CancellationToken  CancellationToken   = default)

        => SendAsync(new EMailEnvelop(EMail), RequireTls, Dsn, Priority, CancellationToken);

    #endregion


    #region SendDirectAsync(EMailEnvelop, RequireTls = false, ...)  — synchronous direct-to-MX

    /// <summary>
    /// Deliver the given e-mail envelope **directly to each recipient domain's MX now**, waiting
    /// for the receiving server's verdict — no queue, no retries. Use this when you must know
    /// immediately whether the recipient's mail server accepted the message (e.g. a password-reset
    /// or contract e-mail), and accept that a transient failure is the caller's to handle.
    /// One delivery is attempted per recipient domain; the per-domain results are returned.
    /// </summary>
    /// <param name="EMailEnvelop">A composed e-mail envelope (sender, recipients, message).</param>
    /// <param name="RequireTls">Demand authenticated TLS; defer/fail instead of downgrading (RFC 8689).</param>
    /// <param name="CancellationToken">An optional cancellation token.</param>
    /// <exception cref="InvalidOperationException">This sender was created without a direct-delivery client.</exception>
    public async Task<IReadOnlyList<DirectSendResult>> SendDirectAsync(EMailEnvelop       EMailEnvelop,
                                                                       Boolean            RequireTls          = false,
                                                                       DsnParameters?     Dsn                 = null,
                                                                       SByte              Priority            = 0,
                                                                       CancellationToken  CancellationToken   = default)
    {

        if (directClient is null)
            throw new InvalidOperationException(
                "This MailSender has no direct-delivery client. Construct it with an SMTPOutboundClient " +
                "(the DirectDeliveryClient parameter) to use SendDirectAsync.");

        var from = EMailEnvelop.MailFrom.FirstOrDefault()?.Address.ToString();
        if (String.IsNullOrEmpty(from))
            throw new ArgumentException("The e-mail envelope has no sender (MAIL FROM).", nameof(EMailEnvelop));

        var recipients = EMailEnvelop.RcptTo.ToList();
        if (recipients.Count == 0)
            throw new ArgumentException("The e-mail envelope has no recipients (RCPT TO).", nameof(EMailEnvelop));

        var messageContent = String.Join("\r\n", EMailEnvelop.Mail.ToText());

        var results = new List<DirectSendResult>();

        // Direct-to-MX delivery is per recipient domain (each has its own MX set).
        foreach (var byDomain in recipients.GroupBy(rcpt => rcpt.Address.Domain, StringComparer.OrdinalIgnoreCase))
        {

            var domain = byDomain.Key.TrimEnd('.').ToLowerInvariant();
            var to     = byDomain.Select(rcpt => rcpt.Address.ToString()).ToArray();

            var result = await directClient.SendAsync(domain, from, to, messageContent, RequireTls, Dsn ?? DsnParameters.None, Priority, CancellationToken)
                                           .ConfigureAwait(false);

            results.Add(new DirectSendResult(domain, to, result));
            logger.Log(result.Status == SendStatus.Success ? LogLevel.Info : LogLevel.Warning,
                       $"MailSender: direct delivery to {domain} → {result.Status} ({result.ResponseCode} {result.ResponseText})");

        }

        return results;

    }

    #endregion

    #region SendDirectAsync(EMail, RequireTls = false, ...)

    /// <summary>
    /// Deliver the given e-mail directly to its recipients' MX now (envelope derived from the
    /// <c>From</c>/<c>To</c> headers). See <see cref="SendDirectAsync(EMailEnvelop, Boolean, CancellationToken)"/>.
    /// </summary>
    public Task<IReadOnlyList<DirectSendResult>> SendDirectAsync(EMail              EMail,
                                                                 Boolean            RequireTls          = false,
                                                                 DsnParameters?     Dsn                 = null,
                                                                 SByte              Priority            = 0,
                                                                 CancellationToken  CancellationToken   = default)

        => SendDirectAsync(new EMailEnvelop(EMail), RequireTls, Dsn, Priority, CancellationToken);

    #endregion

}


/// <summary>
/// The result of a synchronous direct-to-MX delivery to one recipient domain
/// (<see cref="MailSender.SendDirectAsync(EMailEnvelop, Boolean, CancellationToken)"/>).
/// </summary>
/// <param name="TargetDomain">The recipient domain that was delivered to.</param>
/// <param name="Recipients">The recipients at that domain.</param>
/// <param name="Result">The receiving server's verdict (status, SMTP code, response text, MX host).</param>
public sealed record DirectSendResult(String        TargetDomain,
                                      String[]      Recipients,
                                      SendResult    Result)
{
    /// <summary>Whether the receiving MX accepted the message.</summary>
    public Boolean IsOk => Result.Status == SendStatus.Success;
}
