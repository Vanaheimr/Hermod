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

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    public sealed class SMTPServer : IAsyncDisposable
    {

        private readonly SMTPServerConfig            serverConfig;
        private readonly RateLimitConfig             _rateLimitConfig;
        private readonly IMailStorage                _storage;
        private readonly IRecipientValidator?        _recipientValidator;
        private readonly IMailSubmission?            _mailSubmission;
        private readonly IDNSClient                   dnsClient;
        private readonly DNSVerifier                 _dnsVerifier;
        private readonly IUserStore                  _userStore;
        private readonly IMailQueue?                 _mailQueue;
        private readonly ConnectionTracker           _connectionTracker;
        private readonly SMTPServerCertificates?     _certificates;
        private readonly ServerCertificateChain?     _ownCertificate;   // read from CertificatePath: this server's to dispose
        private readonly DmarcReportService?         _dmarcReportService;
        private readonly TlsRptIngestor?             _tlsRptIngestor;
        private readonly ILogger                     _logger;
        private readonly ConcurrentBag<TcpListener>  _listeners    = [];

        /// <summary>
        /// Where the MTA port (25) listens, once <see cref="Start"/> has been called - one end point
        /// per <see cref="SMTPServerConfig.ListenAddresses"/> entry, with the port the system chose
        /// when the configured one is 0.
        /// </summary>
        public IReadOnlyList<System.Net.IPEndPoint>  MtaEndPoints          { get; private set; } = [];

        /// <summary>
        /// Where the submission port (587) listens, once <see cref="Start"/> has been called.
        /// </summary>
        public IReadOnlyList<System.Net.IPEndPoint>  SubmissionEndPoints   { get; private set; } = [];

        /// <summary>
        /// Where the implicit-TLS port (465) listens, once <see cref="Start"/> has been called;
        /// empty when it is not bound.
        /// </summary>
        public IReadOnlyList<System.Net.IPEndPoint>  ImplicitTlsEndPoints  { get; private set; } = [];
        private readonly ConcurrentBag<Task>         _sessionTasks = [];
        private readonly CancellationTokenSource     _cts = new();

        /// <param name="recipientValidator">Decides at RCPT whether a recipient on a local domain
        /// exists; without one every such address is accepted (catch-all). See
        /// <see cref="IRecipientValidator"/>.</param>
        public SMTPServer(SMTPServerConfig      ServerConfig,
                          IDNSClient            DNSClient,
                          ILogger?              logger               = null,
                          IUserStore?           userStore            = null,
                          IMailQueue?           mailQueue            = null,
                          RateLimitConfig?      rateLimitConfig      = null,
                          IMailStorage?         mailStorage          = null,
                          IRecipientValidator?  recipientValidator   = null,
                          IMailSubmission?      mailSubmission       = null)

        {

            this.serverConfig        = ServerConfig;
            this._recipientValidator = recipientValidator;
            this._mailSubmission     = mailSubmission;
            this._rateLimitConfig    = rateLimitConfig ?? new RateLimitConfig();
            this._logger             = logger          ?? new ConsoleLogger();
            this.dnsClient           = DNSClient;
            this._dnsVerifier        = new DNSVerifier(this.dnsClient, _logger);
            this._userStore          = userStore ?? new FileUserStore(Path.Combine(ServerConfig.MailStoragePath, "users.txt"));
            this._mailQueue          = mailQueue;
            this._connectionTracker  = new ConnectionTracker(_rateLimitConfig, _logger);

            // Mail storage: an injected IMailStorage, or the default file store. When automatic read
            // receipts are enabled (and there is a queue to send them through), wrap it so a stored
            // message that requested one triggers an MDN (RFC 8098) back to the sender.
            var baseStorage          = mailStorage ?? new FileMailStorage(ServerConfig.MailStoragePath, _logger);
            this._storage            = ServerConfig.EnableAutoMdn && _mailQueue is not null
                                           ? new MdnGeneratingMailStorage(baseStorage, _mailQueue, _logger)
                                           : baseStorage;

            if (ServerConfig.EnableAutoMdn && _mailQueue is not null)
                _logger.Log(LogLevel.Info, "Automatic MDN (read receipt) generation on local delivery is enabled");

            // The certificate: asked for at every handshake, so that one renewed while the
            // server runs is presented from the next connection on - or read once from a file.
            if (ServerConfig.ServerCertificateSelector is not null)
                _certificates = new SMTPServerCertificates(ServerConfig.ServerCertificateSelector, _logger);

            else if (ServerConfig.CertificatePath is not null)
            {

                _ownCertificate  = ServerCertificateChain.Load(ServerConfig.CertificatePath,
                                                               ServerConfig.CertificateKeyPath,
                                                               ServerConfig.CertificatePassword);

                _certificates    = SMTPServerCertificates.Of(_ownCertificate, _logger);

                _logger.Log(LogLevel.Info, $"Loaded certificate: {_ownCertificate}");

            }

            // DMARC reporting (RFC 7489 §7) — opt-in, and only when there is an outbound queue
            // to send the reports through.
            if (ServerConfig.EnableDmarcReporting && _mailQueue is not null)
            {
                var reportEmail     = ServerConfig.DmarcReportEmail ?? $"dmarc-reports@{ServerConfig.Hostname}";
                var reportingDomain = DmarcReportService.AddressDomain(reportEmail);
                if (reportingDomain.Length == 0)
                    reportingDomain = ServerConfig.Hostname;

                var aggregator = new DmarcAggregator(
                                     Path.Combine(ServerConfig.MailStoragePath, "dmarc-reports", "aggregate-state.json"),
                                     _logger);

                var options = new DmarcReportingOptions(
                    OrgName:           ServerConfig.DmarcReportOrgName ?? ServerConfig.Hostname,
                    ReportFromDisplay: $"DMARC Reports <{reportEmail}>",
                    ReportFromAddress: reportEmail,
                    ReportingDomain:   reportingDomain,
                    Interval:          ServerConfig.DmarcReportInterval,
                    EnableForensic:    ServerConfig.EnableDmarcForensic);

                _dmarcReportService = new DmarcReportService(aggregator, _mailQueue, _dnsVerifier, options, _logger);
                _logger.Log(LogLevel.Info, $"DMARC reporting enabled (from {reportEmail}, forensic={ServerConfig.EnableDmarcForensic})");
            }

            // TLS-RPT (RFC 8460) inbound report ingestion — opt-in.
            if (ServerConfig.EnableTlsRptIngestion)
            {
                _tlsRptIngestor = new TlsRptIngestor(
                                      Path.Combine(ServerConfig.MailStoragePath, "tls-reports-received"),
                                      _logger,
                                      ServerConfig.MaxTlsRptReportSize);
                _logger.Log(LogLevel.Info, "TLS-RPT inbound report ingestion enabled");
            }

        }

        public async Task Start(CancellationToken ct = default)
        {

            // With a selector the port is bound even while it has no certificate to give yet:
            // one may come - from certbot, say - and then the port is there.
            var implicitTlsEnabled = serverConfig.EnableImplicitTls && _certificates is not null;

            _logger.Log(LogLevel.Info, $"Starting SMTP server on ports {serverConfig.Port} and {serverConfig.SubmissionPort}"
                                     + (implicitTlsEnabled ? $" and {serverConfig.ImplicitTlsPort} (implicit TLS)" : ""));
            _logger.Log(LogLevel.Info, $"Mail storage: {Path.GetFullPath(serverConfig.MailStoragePath)}");
            _logger.Log(LogLevel.Info, $"STARTTLS: {(_certificates is null ? "Not configured" : _certificates.Available ? "Available" : "Configured, no certificate yet")}");
            _logger.Log(LogLevel.Info, $"AUTH mechanisms: {(serverConfig.AuthMechanisms is { } mechanisms ? String.Join(", ", mechanisms) : "PLAIN, LOGIN, SCRAM-SHA-256, EXTERNAL")}" +
                                       (serverConfig.AuthOnMtaPort ? "" : " - on the submission ports only"));
            _logger.Log(LogLevel.Info, serverConfig.LocalDomainCheck is not null
                                           ? "Local domains: decided at runtime (LocalDomainCheck)"
                                           : $"Local domains: {string.Join(", ", serverConfig.LocalDomains)}");
            _logger.Log(LogLevel.Info, $"Relay auth required: {serverConfig.RequireAuthForRelay}");
            _logger.Log(LogLevel.Info, $"Verification: SPF={serverConfig.VerifySpf} DKIM={serverConfig.VerifyDkim} DMARC={serverConfig.VerifyDmarc}");
            _logger.Log(LogLevel.Info, $"Rate limiting: {_rateLimitConfig.MaxConnectionsPerIp} conn/IP, {_rateLimitConfig.MaxAuthAttemptsPerIpPerHour} auth/hr");

            Directory.CreateDirectory(serverConfig.MailStoragePath);

            var sessionLoops = new List<Task>();

            // Bind a port on every listen address. Done before the first await, so the end points
            // are known as soon as Start returns its task. An IPv6 listener is IPv6 only: with
            // both Any and IPv6Any listed, a dual-mode socket would collide with the IPv4 one.
            IReadOnlyList<System.Net.IPEndPoint> Listen(UInt16 port, Boolean isSubmissionPort, Boolean implicitTls)
            {

                var endPoints = new List<System.Net.IPEndPoint>();

                foreach (var address in serverConfig.ListenAddresses)
                {

                    var listener = new TcpListener(address, port);

                    if (address.AddressFamily == AddressFamily.InterNetworkV6)
                        listener.Server.DualMode = false;

                    listener.Start();
                    _listeners.Add(listener);
                    endPoints.Add((System.Net.IPEndPoint) listener.LocalEndpoint);

                    sessionLoops.Add(AcceptConnections(listener, isSubmissionPort, implicitTls, _cts.Token));

                }

                return endPoints;

            }

            // Port 25:  MTA-to-MTA (inbound)
            MtaEndPoints         = Listen(serverConfig.Port,            isSubmissionPort: false, implicitTls: false);

            // Port 587: MUA-to-MTA (submission) RFC 6409 — AUTH required, STARTTLS offered.
            SubmissionEndPoints  = Listen(serverConfig.SubmissionPort,  isSubmissionPort: true,  implicitTls: false);

            // Port 465: MUA-to-MTA implicit-TLS submission ("SMTPS", RFC 8314). Only bound when
            // a certificate is available, since the connection is TLS from the first byte.
            if (implicitTlsEnabled)
                ImplicitTlsEndPoints = Listen(serverConfig.ImplicitTlsPort, isSubmissionPort: true, implicitTls: true);
            if (!implicitTlsEnabled && serverConfig.EnableImplicitTls)
            {
                _logger.Log(LogLevel.Warning, $"Implicit-TLS port {serverConfig.ImplicitTlsPort} not bound: no certificate configured");
            }

            // DMARC aggregate-report generation loop (RFC 7489 §7.2).
            if (_dmarcReportService is not null)
                _ = _dmarcReportService.RunAsync(_cts.Token);

            _logger.Log(LogLevel.Info, $"Listening on {String.Join(", ", MtaEndPoints.Concat(SubmissionEndPoints).Concat(ImplicitTlsEndPoints))}");
            _logger.Log(LogLevel.Info, "Server started. Waiting for connections...");

            try
            {
                await Task.WhenAll(sessionLoops);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.Log(LogLevel.Info, "Server shutdown requested");
            }

        }

        private async Task AcceptConnections(TcpListener        listener,
                                             Boolean            isSubmissionPort,
                                             Boolean            implicitTls,
                                             CancellationToken  ct)
        {

            var portType = implicitTls ? "SMTPS" : isSubmissionPort ? "Submission" : "MTA";

            while (!ct.IsCancellationRequested)
            {
                try
                {

                    var client    = await listener.AcceptTcpClientAsync(ct);
                    var endpoint  = client.Client.RemoteEndPoint as IPEndPoint;

                    if (endpoint is null)
                    {
                        client.Close();
                        continue;
                    }

                    // Check rate limiting
                    var rateLimitResult = _connectionTracker.CanConnect(endpoint.Address);
                    if (rateLimitResult != RateLimitResult.Allowed)
                    {

                        _logger.Log(LogLevel.Warning,
                            $"[{portType}] Connection rejected from {endpoint.Address}: {rateLimitResult}");

                        // On the implicit-TLS port the client expects a TLS handshake first, so a
                        // plaintext rejection banner would just corrupt the connection — close it.
                        if (implicitTls)
                        {
                            client.Close();
                            continue;
                        }

                        // A 554 instead of the greeting is held open until the client's QUIT
                        // (RFC 5321 §3.1) - away from this loop, which goes on accepting.
                        if (rateLimitResult == RateLimitResult.Blacklisted)
                        {
                            _sessionTasks.Add(RefuseAsync(client, ct));
                            continue;
                        }

                        // A 421 is "closing transmission channel" (RFC 5321 §4.2.2): send it and close.
                        try
                        {

                            var message = rateLimitResult switch {
                                RateLimitResult.TooManyConnections       => "421 4.7.0 Too many connections, try again later",
                                RateLimitResult.TooManyConnectionsPerIp  => "421 4.7.0 Too many connections from your IP",
                                RateLimitResult.ConnectionRateExceeded   => "421 4.7.0 Connection rate exceeded, slow down",
                                _                                        => "421 4.7.0 Connection rejected"
                            };

                            await WriteReplyAsync(client.GetStream(), message, ct);

                        }
                        catch { /* ignore */ }

                        client.Close();
                        continue;

                    }

                    _logger.Log(LogLevel.Info, $"[{portType}] Connection from {endpoint.Address}:{endpoint.Port}");


                    var session   = new SMTPSession(
                                        client,
                                        serverConfig,
                                        _storage,
                                        _dnsVerifier,
                                        _certificates,
                                        _userStore,
                                        _mailQueue,
                                        isSubmissionPort,
                                        _connectionTracker,
                                        _rateLimitConfig,
                                        _logger,
                                        implicitTls,
                                        _dmarcReportService,
                                        _tlsRptIngestor,
                                        _recipientValidator,
                                        _mailSubmission
                                    );

                    var task      = session.HandleAsync(ct);

                    _sessionTasks.Add(task);

                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, $"Accept error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// One reply line, ended by CR LF whatever the platform's newline is: RFC 5321 §2.3.8,
        /// lines are "terminated by the sequence ASCII character "CR" (hex 0D) followed
        /// immediately by ASCII character "LF" (hex 0A)". A StreamWriter's WriteLine ends
        /// them in Environment.NewLine - a bare LF on Linux.
        /// </summary>
        private static Task WriteReplyAsync(Stream Stream, String Reply, CancellationToken CancellationToken)

            => Stream.WriteAsync(Encoding.ASCII.GetBytes(Reply + "\r\n"), CancellationToken).AsTask();


        #region Refusing a client with 554 (RFC 5321 §3.1)

        /// <summary>
        /// How long a client refused with 554 is waited for, in all: it has nothing to do but
        /// QUIT. RFC 5321 §4.5.3.2 gives a session minutes; a refused one holds a socket for
        /// nothing, and a sending MTA says QUIT at once.
        /// </summary>
        private static readonly TimeSpan RefusalTimeout   = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How many commands other than QUIT a client refused with 554 is answered (503)
        /// before the connection is closed.
        /// </summary>
        private const           Int32    RefusalCommands  = 10;

        /// <summary>
        /// The 554 refusals held open right now; more than <see cref="RateLimitConfig.MaxTotalConnections"/>
        /// and the next one is closed after its 554, as a session over the limit would be.
        /// </summary>
        private                 Int32    _refusalsHeld;

        /// <summary>
        /// Refuse a client with 554 instead of the greeting. RFC 5321 §3.1: "A server taking
        /// this approach MUST still wait for the client to send a QUIT (see Section 4.1.1.10)
        /// before closing the connection and SHOULD respond to any intervening commands with
        /// "503 bad sequence of commands"." So QUIT is answered 221 and the connection closed,
        /// every other line 503 5.5.1 - within bounds, so that a refused client holds nothing
        /// for long: <see cref="RefusalTimeout"/> in all, <see cref="RefusalCommands"/> commands,
        /// lines of <see cref="SMTPServerConfig.MaxCommandLineLength"/>, and no more refusals
        /// held at once than <see cref="RateLimitConfig.MaxTotalConnections"/>.
        /// </summary>
        private async Task RefuseAsync(TcpClient Client, CancellationToken CancellationToken)
        {

            var held = Interlocked.Increment(ref _refusalsHeld) <= _rateLimitConfig.MaxTotalConnections;

            try
            {

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                cts.CancelAfter(RefusalTimeout);

                var stream    = Client.GetStream();
                var reader    = new SMTPLineReader(stream);

                await WriteReplyAsync(stream, "554 5.7.1 Connection refused - blacklisted", cts.Token);

                for (var commands = 0; held && commands < RefusalCommands; commands++)
                {

                    if (await reader.ReadLineAsync(serverConfig.MaxCommandLineLength, cts.Token) is not { } line)
                        return;

                    if (line.Status == SMTPLineStatus.Ok &&
                        line.Text.TrimEnd(' ').Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteReplyAsync(stream, $"221 2.0.0 {serverConfig.Hostname} closing connection", cts.Token);
                        return;
                    }

                    await WriteReplyAsync(stream, "503 5.5.1 Bad sequence of commands", cts.Token);

                }

            }
            catch
            {
                // the client is gone, or its time is up
            }
            finally
            {
                Interlocked.Decrement(ref _refusalsHeld);
                Client.Close();
            }

        }

        #endregion

        public async Task Stop()
        {

            _logger.Log(LogLevel.Info, "Stopping server...");
            await _cts.CancelAsync();

            foreach (var listener in _listeners)
            {
                listener.Stop();
            }

            await Task.WhenAll(_sessionTasks.ToArray());
            _connectionTracker.Dispose();
            _logger.Log(LogLevel.Info, "Server stopped");

        }

        public async ValueTask DisposeAsync()
        {
            await Stop();
            _cts.         Dispose();
            _ownCertificate?.Certificate.Dispose();
        }

    }

}
