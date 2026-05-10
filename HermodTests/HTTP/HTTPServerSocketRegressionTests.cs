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
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using NUnit.Framework;

using Org.BouncyCastle.Crypto;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.PKI;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP
{

    /// <summary>
    /// Socket-level regression tests for the Hermod HTTP test server.
    /// </summary>
    [TestFixture]
    public class HTTPServerSocketRegressionTests
    {

        #region TLS handshake timeout does not block accept loop

        [Test]
        public async Task Slow_TLS_Handshake_Does_Not_Block_Following_Accepts()
        {

            var server        = CreateHTTPSServer(TimeSpan.FromSeconds(2));
            var slowClient    = new TcpClient(AddressFamily.InterNetwork);
            var anotherClient = new TcpClient(AddressFamily.InterNetwork);

            try
            {

                await slowClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients > 0,
                        TimeSpan.FromSeconds(1)
                    ), Is.True, "The first slow TLS client was never accepted.");

                await anotherClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients >= 2,
                        TimeSpan.FromMilliseconds(500)
                    ), Is.True, "A half-open TLS client must not block later HTTPS accepts.");

            }
            finally
            {
                slowClient.Dispose();
                anotherClient.Dispose();
                await server.Stop();
            }

        }

        #endregion

        #region TLS handshake timeout cleans active clients

        [Test]
        public async Task Timed_Out_TLS_Handshake_Removes_Active_Client()
        {

            var server     = CreateHTTPSServer(TimeSpan.FromMilliseconds(750));
            var slowClient = new TcpClient(AddressFamily.InterNetwork);

            try
            {

                await slowClient.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients > 0,
                        TimeSpan.FromSeconds(1)
                    ), Is.True, "The slow TLS client was never observed as active.");

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients == 0,
                        TimeSpan.FromSeconds(4)
                    ), Is.True, "A timed-out TLS handshake must be removed from activeClients.");

            }
            finally
            {
                slowClient.Dispose();
                await server.Stop();
            }

        }

        #endregion

        #region IPv4 and IPv6 accept loops

        [Test]
        public async Task AcceptLoops_Handle_IPv4_And_IPv6_Clients()
        {

            var server = CreateHTTPServer(IPvXAddress.Localhost);

            try
            {

                var ipv4Response = await SendRawHTTPRequest(
                                        System.Net.IPAddress.Loopback,
                                        server.TCPPort,
                                        "localhost"
                                    );

                Assert.That(ipv4Response.Contains("200 OK"), Is.True, ipv4Response);

                try
                {

                    var ipv6Response = await SendRawHTTPRequest(
                                            System.Net.IPAddress.IPv6Loopback,
                                            server.TCPPort,
                                            "localhost"
                                        );

                    Assert.That(ipv6Response.Contains("200 OK"), Is.True, ipv6Response);

                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressFamilyNotSupported ||
                                                e.SocketErrorCode == SocketError.NetworkUnreachable       ||
                                                e.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    Assert.Inconclusive($"IPv6 loopback is not available on this host: {e.SocketErrorCode}");
                }

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion

        #region HTTP activeClients cleanup

        [Test]
        public async Task Completed_HTTP_Requests_Remove_HTTPConnection_ActiveClient()
        {

            var server = CreateHTTPServer(IPv4Address.Localhost);

            try
            {

                using var httpClient = new HttpClient(
                                           new HttpClientHandler {
                                               UseProxy = false
                                           }
                                       ) {
                                           Timeout = TimeSpan.FromSeconds(3)
                                       };

                for (var i = 0; i < 3; i++)
                {
                    var responseBody = await httpClient.GetStringAsync($"http://127.0.0.1:{server.TCPPort}/");
                    Assert.That(responseBody, Is.EqualTo("Hello World!"));
                }

                Assert.That(await WaitUntilAsync(
                        () => server.NumberOfConnectedClients == 0,
                        TimeSpan.FromSeconds(2)
                    ), Is.True, "Completed HTTP requests must not leave HTTPConnection entries in activeClients.");

            }
            finally
            {
                await server.Stop();
            }

        }

        #endregion


        #region Helpers

        private static HTTPTestServerX CreateHTTPServer(IIPAddress? ListenAddress = null)
        {

            var server = new HTTPTestServerX(
                             IPAddress:  ListenAddress,
                             TCPPort:    IPPort.Zero,
                             AutoStart:  true
                         );

            RegisterRootHandler(new HTTPAPI(server));

            return server;

        }

        private static HTTPTestServerX CreateHTTPSServer(TimeSpan ReceiveTimeout)
        {

            var serverCertificate = CreateServerCertificate();
            var server            = new HTTPTestServerX(
                                        TCPPort:                    IPPort.Zero,
                                        ReceiveTimeout:             ReceiveTimeout,
                                        ServerCertificateSelector:  (tcpServer, tcpClient) => serverCertificate,
                                        AutoStart:                  true
                                    );

            RegisterRootHandler(new HTTPAPI(server));

            return server;

        }

        private static void RegisterRootHandler(HTTPAPI HTTPAPI)
        {

            HTTPAPI.AddHandler(HTTPPath.Root,
                               HTTPMethod:   HTTPMethod.GET,
                               HTTPDelegate: request => Task.FromResult(
                                                             new HTTPResponse.Builder(request) {
                                                                 HTTPStatusCode  = HTTPStatusCode.OK,
                                                                 Server          = "Hermod Test Server",
                                                                 Date            = Timestamp.Now,
                                                                 ContentType     = HTTPContentType.Text.PLAIN,
                                                                 Content         = "Hello World!".ToUTF8Bytes(),
                                                                 Connection      = ConnectionType.Close
                                                             }.AsImmutable));

        }

        private static X509Certificate2 CreateServerCertificate()
        {

            var rootCAKeyPair        = PKIFactory.GenerateRSAKeyPair(2048);
            var rootCACertificate    = PKIFactory.CreateRootCACertificate(
                                           "HTTPServerSocketRegressionTests Root CA",
                                           rootCAKeyPair
                                       );

            var serverKeyPair        = PKIFactory.GenerateRSAKeyPair(2048);

            return PKIFactory.SignServerCertificate(
                       "HTTPServerSocketRegressionTests Server Certificate",
                       null,
                       serverKeyPair.Public,
                       rootCAKeyPair.Private,
                       rootCACertificate
                   ).ToDotNet(serverKeyPair.Private);

        }

        private static async Task<String> SendRawHTTPRequest(System.Net.IPAddress Address,
                                                             IPPort               Port,
                                                             String               Host)
        {

            using var tcpClient = new TcpClient(Address.AddressFamily);
            await tcpClient.ConnectAsync(Address, Port.ToInt32());

            await using var stream = tcpClient.GetStream();
            var request            = Encoding.ASCII.GetBytes(
                                         $"GET / HTTP/1.1\r\nHost: {Host}\r\nConnection: close\r\n\r\n"
                                     );

            await stream.WriteAsync(request);

            using var cts          = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response     = new MemoryStream();
            var       buffer       = new Byte[4096];

            while (true)
            {

                var read = await stream.ReadAsync(buffer, cts.Token);
                if (read == 0)
                    break;

                response.Write(buffer, 0, read);

            }

            return Encoding.ASCII.GetString(response.ToArray());

        }

        private static async Task<Boolean> WaitUntilAsync(Func<Boolean> Predicate,
                                                          TimeSpan      Timeout)
        {

            var end = DateTime.UtcNow + Timeout;

            while (DateTime.UtcNow < end)
            {

                if (Predicate())
                    return true;

                await Task.Delay(25);

            }

            return Predicate();

        }

        #endregion

    }

}
