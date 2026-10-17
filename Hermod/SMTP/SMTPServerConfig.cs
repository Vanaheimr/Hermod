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

        /// <summary>
        /// The addresses to listen on; every port below is bound on each of them. Default: every
        /// IPv4 address. <see cref="System.Net.IPAddress.IPv6Any"/> is every IPv6 address and IPv6
        /// only - list it beside <see cref="System.Net.IPAddress.Any"/> for both;
        /// <see cref="System.Net.IPAddress.Loopback"/> keeps the server to this host.
        /// </summary>
        public IReadOnlyList<System.Net.IPAddress> ListenAddresses { get; init; } = [ System.Net.IPAddress.Any ];

        /// <summary>
        /// The MTA port. 0 lets the system choose one; <see cref="Server.SMTPServer.MtaEndPoints"/>
        /// tells which (likewise for the other ports).
        /// </summary>
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

        /// <summary>
        /// The server certificate, read once at start: a PKCS#12 file, or a PEM file with
        /// the certificate and its intermediates (certbot's fullchain.pem) - the key then in
        /// <see cref="CertificateKeyPath"/> or in the same file. For a certificate that is
        /// renewed while the server runs, see <see cref="ServerCertificateSelector"/>.
        /// </summary>
        public          String?   CertificatePath           { get; init; }

        /// <summary>
        /// For a PEM <see cref="CertificatePath"/>: the file with the private key (certbot's
        /// privkey.pem); null when it is in the certificate file.
        /// </summary>
        public          String?   CertificateKeyPath        { get; init; }

        /// <summary>
        /// For a PKCS#12 <see cref="CertificatePath"/>: its password.
        /// </summary>
        public          String?   CertificatePassword       { get; init; }

        /// <summary>
        /// Asked at every TLS handshake - STARTTLS and implicit TLS - for the certificate to
        /// present and its intermediates, with the name the client asked for (SNI; null when
        /// it named none). So a certificate renewed while the server runs is presented from
        /// the next handshake on, and one server can present different certificates for
        /// different names. An answer of null means no TLS for now: STARTTLS is not offered,
        /// and an implicit-TLS connection is closed. Takes precedence over
        /// <see cref="CertificatePath"/>. <see cref="ServerCertificateFile.Selector"/> is one.
        /// </summary>
        public Func<String?, ServerCertificateChain?>?  ServerCertificateSelector  { get; init; }
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
        /// Decides whether a domain is local, instead of <see cref="LocalDomains"/>: for a server
        /// whose domains change while it runs - managed in a web interface, say - and which
        /// should not have to restart, and drop the sessions in flight, for a new one.
        /// Called for every RCPT, with the domain as the client sent it; it must be thread-safe
        /// and answer without regard to case.
        /// </summary>
        public Func<String, Boolean>?  LocalDomainCheck      { get; init; }

        /// <summary>
        /// Require authentication for relaying to external domains.
        /// MUST be true in production to prevent becoming an open relay!
        /// </summary>
        public          Boolean  RequireAuthForRelay        { get; init; } = true;

        /// <summary>
        /// The DNS blocklists asked about clients that are not signed in, and what a
        /// client gets that one names - none: no list is asked.
        /// </summary>
        public DnsBlocklistConfig?  DnsBlocklists           { get; init; }

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
        /// Check if a domain is local (case-insensitive): <see cref="LocalDomainCheck"/> when
        /// there is one, otherwise <see cref="LocalDomains"/>.
        /// </summary>
        public          Boolean  IsLocalDomain(String domain)

             => LocalDomainCheck is not null
                    ? LocalDomainCheck(domain)
                    : LocalDomains.Contains(
                          domain,
                          StringComparer.OrdinalIgnoreCase
                      );

    }

}
