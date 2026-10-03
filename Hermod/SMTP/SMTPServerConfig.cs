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

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP
{

    public sealed record SMTPServerConfig
    {
        public required String    Hostname                  { get; init; }
        public          UInt16    Port                      { get; init; } = 25;
        public          UInt16    SubmissionPort            { get; init; } = 587;

        /// <summary>
        /// Implicit-TLS submission port (RFC 8314 "SMTPS"): the whole connection is TLS from
        /// the first byte, no plaintext STARTTLS upgrade. Only bound when a certificate is
        /// configured and <see cref="EnableImplicitTls"/> is true.
        /// </summary>
        public          UInt16    ImplicitTlsPort           { get; init; } = 465;

        /// <summary>
        /// Whether to bind the implicit-TLS submission port (requires a certificate).
        /// </summary>
        public          Boolean   EnableImplicitTls         { get; init; } = true;
        public          String    MailStoragePath           { get; init; } = "./mailstore";
        public          String?   CertificatePath           { get; init; }
        public          String?   CertificatePassword       { get; init; }
        public          TimeSpan  SessionTimeout            { get; init; } = TimeSpan.FromMinutes(5);
        public          Int32     MaxMessageSize            { get; init; } = 25 * 1024 * 1024; // 25 MB
        public          Int32     MaxRecipients             { get; init; } = 100;

        /// <summary>
        /// Maximum length of a command line incl. CRLF (RFC 5321 §4.5.3.1.4 requires ≥ 512;
        /// larger here to accommodate AUTH exchanges and ESMTP parameters).
        /// </summary>
        public          Int32     MaxCommandLineLength      { get; init; } = 1024;

        /// <summary>
        /// Maximum length of a DATA text line incl. CRLF (RFC 5321 §4.5.3.1.6 recommends 1000;
        /// defaulted higher to tolerate real-world long-line mail while still bounding memory).
        /// </summary>
        public          Int32     MaxTextLineLength         { get; init; } = 2048;
        /// <summary>
        /// What to do with a bare CR or LF inside DATA content. Neither is ever a line end
        /// (RFC 5321 §2.3.8) — that is what keeps "&lt;LF&gt;.&lt;LF&gt;" from ending a message
        /// (SMTP smuggling). True (default): reject the message with 550 after the end of data.
        /// False: accept it, turning each bare CR or LF into a CR LF of its own, for peers that
        /// send bare LFs and cannot be fixed.
        /// </summary>
        public          Boolean   RejectBareLineEndings     { get; init; } = true;
        public          Boolean   RequireStartTls           { get; init; } = false;

        /// <summary>
        /// Verify DKIM signatures of inbound mail and report them in Authentication-Results.
        /// DKIM is advisory either way (RFC 6376 §6.1); DMARC still evaluates it for alignment.
        /// </summary>
        public          Boolean   VerifyDkim                { get; init; } = true;

        /// <summary>
        /// Check SPF for inbound mail: a hard fail (-all) is rejected with 550 5.7.23, and the
        /// result is reported in Authentication-Results. False skips both; DMARC still
        /// evaluates SPF for alignment.
        /// </summary>
        public          Boolean   VerifySpf                 { get; init; } = true;

        /// <summary>
        /// Evaluate DMARC for inbound mail: enforce p=reject/quarantine, report the result in
        /// Authentication-Results and feed DMARC reporting. False skips all of it.
        /// </summary>
        public          Boolean   VerifyDmarc               { get; init; } = true;

        /// <summary>
        /// Domains considered "local" - mail to these is stored locally.
        /// Mail to other domains requires authentication (relay).
        /// </summary>
        public HashSet<String>   LocalDomains               { get; init; } = ["localhost", "localhost.localdomain"];

        /// <summary>
        /// Require authentication for relaying to external domains.
        /// MUST be true in production to prevent becoming an open relay!
        /// </summary>
        public          Boolean  RequireAuthForRelay        { get; init; } = true;

        /// <summary>
        /// Require authentication on submission port (587) even for local delivery.
        /// RFC 6409 recommends this.
        /// </summary>
        public          Boolean  RequireAuthOnSubmission    { get; init; } = true;


        #region DMARC reporting (RFC 7489 §7)

        /// <summary>
        /// Emit DMARC aggregate (RUA) reports for domains that request them. Off by default:
        /// a receiver is not required to send reports, and doing so needs a working outbound
        /// path and a domain identity the reports can be DKIM-aligned with.
        /// </summary>
        public          Boolean   EnableDmarcReporting       { get; init; } = false;

        /// <summary>
        /// Also emit DMARC forensic (RUF/failure) reports. Separate opt-in because forensic
        /// reports contain message content and are privacy-sensitive (RFC 7489 §7.3).
        /// </summary>
        public          Boolean   EnableDmarcForensic        { get; init; } = false;

        /// <summary>
        /// Automatically generate a Message Disposition Notification (read receipt, RFC 8098) when a
        /// locally-delivered message requested one. Opt-in and privacy-sensitive: automatic MDNs confirm
        /// a live address and can be abused (RFC 8098 §2.1). Only effective when an outbound queue exists
        /// to send the MDN through.
        /// </summary>
        public          Boolean   EnableAutoMdn              { get; init; } = false;

        /// <summary>
        /// How often aggregate reports are generated (RFC 7489 default 86400 s).
        /// </summary>
        public          TimeSpan  DmarcReportInterval        { get; init; } = TimeSpan.FromHours(24);

        /// <summary>
        /// org_name reported in aggregate reports. Defaults to <see cref="Hostname"/>.
        /// </summary>
        public          String?   DmarcReportOrgName         { get; init; }

        /// <summary>
        /// From/contact address for outgoing reports. Defaults to postmaster@&lt;hostname&gt;.
        /// </summary>
        public          String?   DmarcReportEmail           { get; init; }

        #endregion

        #region TLS-RPT ingestion (RFC 8460)

        /// <summary>
        /// Ingest inbound SMTP TLS Reporting (TLS-RPT) reports delivered to our <c>_smtp._tls</c>
        /// <c>rua</c> mailbox: detect them, decompress + parse the RFC 8460 JSON, persist and log
        /// a summary. Off by default.
        /// </summary>
        public          Boolean   EnableTlsRptIngestion      { get; init; } = false;

        #endregion



        /// <summary>
        /// Check if a domain is local (case-insensitive)
        /// </summary>
        public          Boolean  IsLocalDomain(String domain)

             => LocalDomains.Contains(
                    domain,
                    StringComparer.OrdinalIgnoreCase
                );

    }

}
