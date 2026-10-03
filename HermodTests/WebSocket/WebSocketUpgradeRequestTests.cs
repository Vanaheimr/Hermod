/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// The HTTP request that asks a WebSocket server on a port of its own for an
    /// upgrade says what came with it: the sockets of its connection, and the
    /// certificate the client showed in the TLS handshake.
    /// </summary>
    /// <remarks>
    /// The server parsed the request from its bytes alone. The sockets of the
    /// request were then localhost:443 on both ends, whoever had connected from
    /// wherever, and its client certificate was null: the connection had both,
    /// and the request said otherwise. A validator or an AuthenticateAsync that
    /// asks the request who is there - the request of an HTTP server has always
    /// known it - was told nobody, and an application that knows a client by
    /// its certificate could not tell it from a client without one. The request
    /// is parsed with what its connection knows now.
    ///
    /// Over a bare TCP client and an SslStream, which say exactly what they
    /// are told to; the certificates are self-signed, without a CA, and known
    /// to the other side by their thumbprints, so that no certificate store is
    /// written to.
    /// </remarks>
    [TestFixture]
    public class WebSocketUpgradeRequestTests
    {

        #region Data

        private X509Certificate2?  serverCertificate;
        private X509Certificate2?  clientCertificate;

        #endregion

        #region OneTimeSetUp() / OneTimeTearDown()

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            serverCertificate = SelfSigned("CN=localhost", "1.3.6.1.5.5.7.3.1");
            clientCertificate = SelfSigned("CN=client",    "1.3.6.1.5.5.7.3.2");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {

            serverCertificate?.Dispose();
            clientCertificate?.Dispose();

            serverCertificate = null;
            clientCertificate = null;

        }

        #endregion


        #region TheRequestHasTheSocketsOfItsConnection()

        [Test]
        public async Task TheRequestHasTheSocketsOfItsConnection()
        {

            HTTPRequest? request = null;

            await using var server = new WebSocketServer(
                                         HTTPPort:               IPPort.Zero,
                                         RequireAuthentication:  false,
                                         AutoStart:              true
                                     );

            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {
                request = connection.HTTPRequest;
                return Task.FromResult<HTTPResponse?>(null);
            };

            using var tcp      = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, server.IPPort.ToUInt16());

            var clientPort     = ((IPEndPoint) tcp.Client.LocalEndPoint!).Port;
            var statusCode     = await Upgrade(tcp.GetStream());

            Assert.Multiple(() => {
                Assert.That(statusCode,                                  Is.EqualTo(101));
                Assert.That(request,                                     Is.Not.Null);
                Assert.That(request?.RemoteSocket.Port.ToUInt16(),       Is.EqualTo(clientPort),
                            "The request does not say where it came from.");
                Assert.That(request?.LocalSocket. Port.ToUInt16(),       Is.EqualTo(server.IPPort.ToUInt16()),
                            "The request does not say where it arrived.");
            });

        }

        #endregion

        #region TheRequestHasTheClientCertificateOfItsConnection()

        [Test]
        public async Task TheRequestHasTheClientCertificateOfItsConnection()
        {

            HTTPRequest?               request     = null;
            WebSocketServerConnection? connection  = null;

            await using var server = new WebSocketServer(
                                         HTTPPort:                    IPPort.Zero,
                                         RequireAuthentication:       false,
                                         ServerCertificateSelector:   (tcpServer, tcpClient) => serverCertificate!,
                                         ClientCertificateValidator:  (sender, certificate, chain, tlsServer, policyErrors) =>
                                                                          certificate?.GetCertHashString() == clientCertificate!.GetCertHashString()
                                                                              ? TLSValidationResult.Success()
                                                                              : TLSValidationResult.Failed("Not the client certificate of this test!"),
                                         ClientCertificateRequired:   true,
                                         AutoStart:                   true
                                     );

            server.OnValidateWebSocketConnection += (timestamp, webSocketServer, webSocketConnection, eventTrackingId, cancellationToken) => {
                connection  = webSocketConnection;
                request     = webSocketConnection.HTTPRequest;
                return Task.FromResult<HTTPResponse?>(null);
            };

            using var tcp  = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, server.IPPort.ToUInt16());

            using var tls  = new SslStream(tcp.GetStream());

            await tls.AuthenticateAsClientAsync(
                      new SslClientAuthenticationOptions {
                          TargetHost                           = "localhost",
                          ClientCertificates                   = [ clientCertificate! ],
                          LocalCertificateSelectionCallback    = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => clientCertificate!,
                          RemoteCertificateValidationCallback  = (sender, certificate, chain, policyErrors) => certificate?.GetCertHashString() == serverCertificate!.GetCertHashString()
                      }
                  );

            var statusCode = await Upgrade(tls);

            Assert.Multiple(() => {
                Assert.That(statusCode,                                                 Is.EqualTo(101));
                Assert.That(connection?.ClientCertificate?.GetCertHashString(),         Is.EqualTo(clientCertificate!.GetCertHashString()),
                            "The connection does not have the client certificate.");
                Assert.That(request?.   ClientCertificate?.GetCertHashString(),         Is.EqualTo(clientCertificate!.GetCertHashString()),
                            "The request does not have the client certificate its connection has.");
            });

        }

        #endregion

        #region TheRequestOnAnHTTPPathHasTheSocketsOfItsConnection()

        /// <summary>
        /// A WebSocket server lent a connection by an HTTP server for one of its
        /// paths reads the request that asked for the upgrade once more, as it
        /// would have read it off a socket of its own - and said localhost:443
        /// there too.
        /// </summary>
        [Test]
        public async Task TheRequestOnAnHTTPPathHasTheSocketsOfItsConnection()
        {

            HTTPRequest? request = null;

            await using var httpServer       = await HTTPServer.StartNew();

            // Not started: the HTTP server borrows its protocol for one path.
            await using var webSocketServer  = new WebSocketServer(
                                                   RequireAuthentication:  false,
                                                   AutoStart:              false
                                               );

            webSocketServer.OnValidateWebSocketConnection += (timestamp, server, connection, eventTrackingId, cancellationToken) => {
                request = connection.HTTPRequest;
                return Task.FromResult<HTTPResponse?>(null);
            };

            httpServer.AddHTTPAPI().AddHandler(
                HTTPMethod.GET,
                HTTPPath.Parse("/path"),
                HTTPDelegate: WebSocketUpgrade.For(webSocketServer)
            );

            using var tcp   = new TcpClient();
            await tcp.ConnectAsync(System.Net.IPAddress.Loopback, httpServer.TCPPort.ToUInt16());

            var clientPort  = ((IPEndPoint) tcp.Client.LocalEndPoint!).Port;
            var statusCode  = await Upgrade(tcp.GetStream());

            Assert.Multiple(() => {
                Assert.That(statusCode,                                  Is.EqualTo(101));
                Assert.That(request,                                     Is.Not.Null);
                Assert.That(request?.RemoteSocket.Port.ToUInt16(),       Is.EqualTo(clientPort),
                            "The request does not say where it came from.");
                Assert.That(request?.LocalSocket. Port.ToUInt16(),       Is.EqualTo(httpServer.TCPPort.ToUInt16()),
                            "The request does not say where it arrived.");
            });

        }

        #endregion


        #region (private static) Upgrade(Stream)

        /// <summary>
        /// Ask for an upgrade on the given stream, and read the status code of the
        /// answer's first line.
        /// </summary>
        private static async Task<Int32> Upgrade(Stream Stream)
        {

            var request = "GET /path HTTP/1.1\r\n" +
                          "Host: localhost\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Connection: Upgrade\r\n" +
                         $"Sec-WebSocket-Key: {Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))}\r\n" +
                          "Sec-WebSocket-Version: 13\r\n" +
                          "\r\n";

            await Stream.WriteAsync(Encoding.ASCII.GetBytes(request));

            using var timeout  = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var       head     = new StringBuilder();
            var       octet    = new Byte[1];

            // One octet at a time, up to the end of the first line.
            while (!head.ToString().EndsWith("\r\n") &&
                   await Stream.ReadAsync(octet, timeout.Token) == 1)
            {
                head.Append((Char) octet[0]);
            }

            var statusLine = head.ToString().Split(' ');

            return statusLine.Length > 1 && Int32.TryParse(statusLine[1], out var statusCode)
                       ? statusCode
                       : 0;

        }

        #endregion

        #region (private static) SelfSigned(Subject, ExtendedKeyUsage)

        /// <summary>
        /// A self-signed certificate for the given subject and extended key usage,
        /// with a private key SChannel can use: on Windows a key held in memory
        /// alone is turned down, so the certificate goes out to PKCS#12 and comes
        /// back in.
        /// </summary>
        private static X509Certificate2 SelfSigned(String  Subject,
                                                   String  ExtendedKeyUsage)
        {

            using var key     = RSA.Create(2048);

            var request       = new CertificateRequest(Subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new Oid(ExtendedKeyUsage) ], false));

            using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));

            return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);

        }

        #endregion

    }

}
