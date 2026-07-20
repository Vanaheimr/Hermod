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

    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;


    /// <summary>
    /// Dials an HTTP/2 server over TLS with ALPN `h2` and returns a ready
    /// <see cref="HTTP2ClientConnection"/> — the client-side counterpart of
    /// <see cref="HTTP2Server"/>'s accept path. Everything protocol-level lives in
    /// <see cref="HTTP2ClientConnection"/>; this type only owns the TCP connect and
    /// the TLS/ALPN handshake.
    /// </summary>
    public static class HTTP2Client
    {

        /// <summary>
        /// Connect to Host:Port, negotiate TLS + ALPN `h2`, exchange the HTTP/2
        /// preface, and return a connection ready for <c>SendRequestAsync</c>.
        /// </summary>
        /// <param name="Host">Host name (also the TLS SNI / ALPN target host).</param>
        /// <param name="Port">TCP port.</param>
        /// <param name="ValidateServerCertificate">
        /// Optional certificate validation callback. Pass a permissive one to accept
        /// the demo server's self-signed certificate; leave null for normal chain
        /// validation.
        /// </param>
        /// <param name="ClientCertificate">
        /// Optional client certificate to present for mutual TLS (mTLS), when the
        /// server requires one. Null for ordinary (server-auth-only) TLS.
        /// </param>
        /// <param name="Cleartext">
        /// Connect in cleartext ("h2c", prior-knowledge — RFC 9113 §3.3): open a
        /// plain TCP connection and send the HTTP/2 preface directly, with no TLS
        /// and no ALPN. <paramref name="ValidateServerCertificate"/> and
        /// <paramref name="ClientCertificate"/> are ignored in this mode. Use only
        /// where the peer is known to speak HTTP/2 (a TLS-terminating proxy, a
        /// trusted internal hop, or local testing).
        /// </param>
        public static async Task<HTTP2ClientConnection> ConnectAsync(
            string                                Host,
            int                                   Port,
            RemoteCertificateValidationCallback?  ValidateServerCertificate = null,
            X509Certificate2?                     ClientCertificate         = null,
            HTTP2ClientOptions?                   Options                   = null,
            bool                                  Cleartext                 = false,
            CancellationToken                     CancellationToken         = default)
        {

            var tcp = new TcpClient();
            await tcp.ConnectAsync(Host, Port, CancellationToken);

            // h2c: skip TLS/ALPN entirely and speak HTTP/2 over the raw socket.
            if (Cleartext)
            {
                var plain = new HTTP2ClientConnection(tcp.GetStream(), CancellationToken, Options);
                await plain.StartAsync();
                return plain;
            }

            var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false, ValidateServerCertificate);

            var clientOptions = new SslClientAuthenticationOptions {
                TargetHost           = Host,
                ApplicationProtocols = [SslApplicationProtocol.Http2],
                EnabledSslProtocols  = SslProtocols.Tls12 | SslProtocols.Tls13
            };

            if (ClientCertificate is not null)
                clientOptions.ClientCertificates = [ClientCertificate];

            await ssl.AuthenticateAsClientAsync(clientOptions, CancellationToken);

            if (ssl.NegotiatedApplicationProtocol != SslApplicationProtocol.Http2)
                throw new HTTP2ConnectionException(HTTP2ErrorCode.PROTOCOL_ERROR,
                    $"Server did not negotiate HTTP/2 over ALPN (got '{ssl.NegotiatedApplicationProtocol}')");

            var connection = new HTTP2ClientConnection(ssl, CancellationToken, Options);
            await connection.StartAsync();

            return connection;

        }

    }

}
