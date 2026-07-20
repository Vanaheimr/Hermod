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

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP2
{

    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;


    /// <summary>
    /// A minimal HTTP/2 server that accepts TLS connections with ALPN "h2" negotiation,
    /// then hands each connection off to an HTTP2Connection for frame-level processing.
    /// 
    /// Usage:
    ///   var server = new HTTP2Server(
    ///       IPAddress.Any, 8443,
    ///       new X509Certificate2("server.pfx", "password"),
    ///       MyRequestHandler
    ///   );
    ///   await server.RunAsync();
    /// 
    /// The request handler receives decoded headers + body and returns response headers + body.
    /// This is where you'd plug in your existing HTTP/1.1 application logic.
    /// </summary>
    public sealed class HTTP2Server
    {

        private readonly IPEndPoint           endpoint;
        private readonly X509Certificate2?    certificate;
        private readonly bool                 cleartext;
        private readonly HTTP2RequestHandler  requestHandler;
        private readonly HTTP2ConnectHandler? connectHandler;
        private readonly bool                 requireClientCertificate;
        private readonly RemoteCertificateValidationCallback? validateClientCertificate;
        private readonly HTTP2Timeouts         timeouts;
        private readonly HTTP2StreamingHandler? streamingHandler;
        private readonly long                  maxRequestBodySize;
        private readonly CancellationTokenSource cts = new();

        /// <summary>Default buffered-request-body cap handed to each connection: 16 MiB.</summary>
        public const long DefaultMaxRequestBodySize = 16 * 1024 * 1024;

        /// <summary>
        /// Tracks currently-running connections so StopAsync can notify each of
        /// them with a GOAWAY before tearing everything down. Keyed by the
        /// connection itself (reference identity) — the value is unused.
        /// </summary>
        private readonly ConcurrentDictionary<HTTP2Connection, byte> activeConnections = new();


        /// <param name="RequireClientCertificate">
        /// Enable mutual TLS: require the client to present an X.509 certificate
        /// during the TLS handshake (a client that presents none is rejected at the
        /// handshake, before any HTTP/2 frame). The validated certificate is
        /// surfaced to request handlers as a synthetic <c>x-client-cert-subject</c>
        /// header. mTLS is transport-layer authentication — orthogonal to, and
        /// combinable with, the HTTP-layer <see cref="HTTPAuthenticator"/>.
        /// </param>
        /// <param name="ValidateClientCertificate">
        /// Optional callback to validate the presented client certificate. If null
        /// while <paramref name="RequireClientCertificate"/> is true, the platform's
        /// default chain validation applies.
        /// </param>
        /// <param name="Cleartext">
        /// Serve HTTP/2 in cleartext ("h2c") with no TLS — the client is expected to
        /// have prior knowledge and sends the HTTP/2 connection preface directly over
        /// plain TCP (RFC 9113 §3.3). No ALPN negotiation and no
        /// <paramref name="Certificate"/> are involved (the RFC 7540 <c>Upgrade:
        /// h2c</c> dance was removed in RFC 9113 §3.1 and is not implemented). Chiefly
        /// useful behind a TLS-terminating proxy or for local testing. When true,
        /// <paramref name="Certificate"/> may be null and mTLS is unavailable.
        /// </param>
        public HTTP2Server(
            IPAddress            Address,
            int                  Port,
            X509Certificate2?    Certificate,
            HTTP2RequestHandler  RequestHandler,
            HTTP2ConnectHandler? ConnectHandler = null,
            bool                 RequireClientCertificate  = false,
            RemoteCertificateValidationCallback? ValidateClientCertificate = null,
            HTTP2Timeouts?       Timeouts = null,
            HTTP2StreamingHandler? StreamingHandler = null,
            bool                 Cleartext = false,
            long                 MaxRequestBodySize = DefaultMaxRequestBodySize)
        {

            if (!Cleartext && Certificate is null)
                throw new ArgumentNullException(nameof(Certificate),
                    "A server certificate is required for HTTP/2 over TLS. Pass Cleartext: true to serve plaintext h2c instead.");

            this.endpoint                  = new IPEndPoint(Address, Port);
            this.certificate               = Certificate;
            this.cleartext                 = Cleartext;
            this.requestHandler            = RequestHandler;
            this.connectHandler            = ConnectHandler;
            this.requireClientCertificate  = RequireClientCertificate;
            this.validateClientCertificate = ValidateClientCertificate;
            this.timeouts                  = Timeouts ?? HTTP2Timeouts.Default;
            this.streamingHandler          = StreamingHandler;
            this.maxRequestBodySize        = MaxRequestBodySize;
        }


        /// <summary>
        /// Start listening and accepting connections. Runs until cancelled.
        /// </summary>
        public async Task RunAsync(CancellationToken ExternalToken = default)
        {

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ExternalToken);
            var       token     = linkedCts.Token;

            var listener = new TcpListener(endpoint);
            listener.Start();

            Console.WriteLine($"[HTTP/2] Listening on {endpoint}");

            try
            {

                while (!token.IsCancellationRequested)
                {

                    var tcpClient = await listener.AcceptTcpClientAsync(token);
                    var remoteEP  = tcpClient.Client.RemoteEndPoint;

                    Console.WriteLine($"[HTTP/2] Accepted TCP connection from {remoteEP}");

                    // Handle each connection concurrently
                    _ = Task.Run(() => HandleConnectionAsync(tcpClient, token), token);

                }

            }
            finally
            {
                listener.Stop();
            }

        }

        /// <summary>
        /// Gracefully stop: notify every active connection with a GOAWAY (so
        /// peers see a proper reason instead of the socket just vanishing), then
        /// cancel to actually tear the connections and listener down. The GOAWAY
        /// sends are best-effort and bounded — a dead/unresponsive peer can't
        /// hang shutdown indefinitely.
        /// </summary>
        public async Task StopAsync()
        {

            var notifications = activeConnections.Keys
                                     .Select(connection => connection.InitiateGracefulShutdownAsync())
                                     .ToArray();

            try
            {
                await Task.WhenAll(notifications).WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Best-effort — proceed to hard-cancel regardless of stragglers
            }

            cts.Cancel();

        }


        /// <summary>
        /// Perform TLS handshake with ALPN negotiation, then run the HTTP/2 connection handler.
        /// </summary>
        private async Task HandleConnectionAsync(TcpClient Client, CancellationToken Token)
        {

            var remoteEP = Client.Client.RemoteEndPoint;

            try
            {

                using (Client)
                {

                    await using var networkStream = Client.GetStream();

                    // h2c (cleartext, prior-knowledge — RFC 9113 §3.3): no TLS and
                    // no ALPN. The client sends the HTTP/2 connection preface
                    // straight over plain TCP; we hand the raw network stream to the
                    // connection unchanged. mTLS is unavailable here (no TLS layer).
                    if (cleartext)
                    {
                        Console.WriteLine($"[HTTP/2] h2c (cleartext) connection from {remoteEP}");
                        await RunConnectionAsync(networkStream, clientCertificate: null, Token);
                        return;
                    }

                    var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);

                    await using (sslStream)
                    {

                        // Configure TLS with ALPN for HTTP/2
                        var sslOptions = new SslServerAuthenticationOptions {

                            ServerCertificate          = certificate,
                            ClientCertificateRequired  = requireClientCertificate,   // mTLS
                            EnabledSslProtocols        = SslProtocols.Tls12 | SslProtocols.Tls13,

                            // ALPN: Advertise "h2" (HTTP/2 over TLS)
                            // If the client also supports h2, it will be selected.
                            ApplicationProtocols       = [
                                new SslApplicationProtocol("h2"),
                                new SslApplicationProtocol("http/1.1")
                            ]

                        };

                        if (validateClientCertificate is not null)
                            sslOptions.RemoteCertificateValidationCallback = validateClientCertificate;

                        // Bound the TLS handshake: a client that opens the TCP
                        // connection but stalls the handshake must not tie up a
                        // connection slot indefinitely (Slowloris at the TLS layer).
                        using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(Token))
                        {
                            handshakeCts.CancelAfter(timeouts.Handshake);
                            try
                            {
                                await sslStream.AuthenticateAsServerAsync(sslOptions, handshakeCts.Token);
                            }
                            catch (OperationCanceledException) when (!Token.IsCancellationRequested)
                            {
                                Console.Error.WriteLine($"[HTTP/2] TLS handshake timed out with {remoteEP}");
                                return;
                            }
                        }

                        // Check which protocol was negotiated
                        var negotiatedProtocol = sslStream.NegotiatedApplicationProtocol;

                        if (negotiatedProtocol == SslApplicationProtocol.Http2)
                        {

                            Console.WriteLine($"[HTTP/2] ALPN negotiated h2 with {remoteEP}");

                            // mTLS: the validated client certificate, if one was presented.
                            var clientCertificate = sslStream.RemoteCertificate as X509Certificate2;

                            await RunConnectionAsync(sslStream, clientCertificate, Token);

                        }
                        else if (negotiatedProtocol == SslApplicationProtocol.Http11)
                        {
                            Console.WriteLine($"[HTTP/2] ALPN negotiated http/1.1 with {remoteEP} — falling back");
                            // Here you would hand off to your existing HTTP/1.1 handler.
                            // For this demo, we just close the connection.
                            await HandleHTTP11FallbackAsync(sslStream);
                        }
                        else
                        {
                            Console.WriteLine($"[HTTP/2] Unknown ALPN protocol '{negotiatedProtocol}' from {remoteEP}");
                        }

                    }

                }

            }
            catch (AuthenticationException ex)
            {
                Console.Error.WriteLine($"[HTTP/2] TLS handshake failed with {remoteEP}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[HTTP/2] Connection error with {remoteEP}: {ex.Message}");
            }
            finally
            {
                Console.WriteLine($"[HTTP/2] Connection closed: {remoteEP}");
            }

        }


        /// <summary>
        /// Run one HTTP/2 connection over an already-established byte transport
        /// (a TLS <see cref="SslStream"/> or a cleartext network stream), tracking
        /// it in <see cref="activeConnections"/> for graceful shutdown.
        /// </summary>
        private async Task RunConnectionAsync(Stream Transport, X509Certificate2? clientCertificate, CancellationToken Token)
        {

            var connection = new HTTP2Connection(Transport, requestHandler, connectHandler, Token, clientCertificate, timeouts, streamingHandler, maxRequestBodySize);
            activeConnections.TryAdd(connection, 0);

            try
            {
                await connection.RunAsync();
            }
            finally
            {
                activeConnections.TryRemove(connection, out _);
            }

        }


        /// <summary>
        /// Placeholder for HTTP/1.1 fallback — in a real server, you'd hand off to your
        /// existing HTTP/1.1 pipeline here.
        /// </summary>
        private static async Task HandleHTTP11FallbackAsync(SslStream Stream)
        {

            // Simple HTTP/1.1 response for demonstration
            var response = "HTTP/1.1 200 OK\r\n" +
                           "Content-Type: text/plain\r\n" +
                           "Content-Length: 39\r\n" +
                           "Connection: close\r\n" +
                           "\r\n" +
                           "HTTP/1.1 fallback — upgrade to HTTP/2!";

            var bytes = Encoding.ASCII.GetBytes(response);
            await Stream.WriteAsync(bytes);
            await Stream.FlushAsync();

        }

    }

}
