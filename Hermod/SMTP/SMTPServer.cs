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

using org.GraphDefined.Vanaheimr.Hermod.DNS;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.SMTP.Server
{

    public sealed class SMTPServer : IAsyncDisposable
    {

        private readonly SMTPServerConfig            serverConfig;
        private readonly RateLimitConfig             _rateLimitConfig;
        private readonly IMailStorage                _storage;
        private readonly IDNSClient                   dnsClient;
        private readonly DNSVerifier                 _dnsVerifier;
        private readonly IUserStore                  _userStore;
        private readonly IMailQueue?                 _mailQueue;
        private readonly ConnectionTracker           _connectionTracker;
        private readonly X509Certificate2?           _certificate;
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

        public SMTPServer(SMTPServerConfig  ServerConfig,
                          IDNSClient         DNSClient,
                          ILogger?          logger            = null,
                          IUserStore?       userStore         = null,
                          IMailQueue?       mailQueue         = null,
                          RateLimitConfig?  rateLimitConfig   = null,
                          IMailStorage?     mailStorage       = null)

        {

            this.serverConfig        = ServerConfig;
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

            if (ServerConfig.CertificatePath is not null)
            {

                _certificate = ServerConfig.CertificatePassword is not null
                    ? X509CertificateLoader.LoadPkcs12FromFile     (ServerConfig.CertificatePath, ServerConfig.CertificatePassword)
                    : X509CertificateLoader.LoadCertificateFromFile(ServerConfig.CertificatePath);

                _logger.Log(LogLevel.Info, $"Loaded certificate: {_certificate.Subject}");

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
                                      _logger);
                _logger.Log(LogLevel.Info, "TLS-RPT inbound report ingestion enabled");
            }

        }

        public async Task Start(CancellationToken ct = default)
        {

            var implicitTlsEnabled = serverConfig.EnableImplicitTls && _certificate is not null;

            _logger.Log(LogLevel.Info, $"Starting SMTP server on ports {serverConfig.Port} and {serverConfig.SubmissionPort}"
                                     + (implicitTlsEnabled ? $" and {serverConfig.ImplicitTlsPort} (implicit TLS)" : ""));
            _logger.Log(LogLevel.Info, $"Mail storage: {Path.GetFullPath(serverConfig.MailStoragePath)}");
            _logger.Log(LogLevel.Info, $"STARTTLS: {(_certificate is not null ? "Available" : "Not configured")}");
            _logger.Log(LogLevel.Info, $"AUTH mechanisms: PLAIN, LOGIN, SCRAM-SHA-256, EXTERNAL");
            _logger.Log(LogLevel.Info, $"Local domains: {string.Join(", ", serverConfig.LocalDomains)}");
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

                        // Send rejection message and close
                        try
                        {

                            var writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                            var message = rateLimitResult switch {
                                RateLimitResult.Blacklisted              => "554 5.7.1 Connection refused - blacklisted",
                                RateLimitResult.TooManyConnections       => "421 4.7.0 Too many connections, try again later",
                                RateLimitResult.TooManyConnectionsPerIp  => "421 4.7.0 Too many connections from your IP",
                                RateLimitResult.ConnectionRateExceeded   => "421 4.7.0 Connection rate exceeded, slow down",
                                _                                        => "421 4.7.0 Connection rejected"
                            };

                            await writer.WriteLineAsync(message);

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
                                        _certificate,
                                        _userStore,
                                        _mailQueue,
                                        isSubmissionPort,
                                        _connectionTracker,
                                        _rateLimitConfig,
                                        _logger,
                                        implicitTls,
                                        _dmarcReportService,
                                        _tlsRptIngestor
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
            _certificate?.Dispose();
        }

    }

}
