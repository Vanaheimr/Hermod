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

using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket client that is given a time-based one-time password sends
    /// it - the current one, at every attempt - and refuses to send one that
    /// would be weaker than it was configured to be.
    /// </summary>
    /// <remarks>
    /// The client kept its TOTPConfig and sent nothing of it: a connection
    /// configured with nothing but a one-time password went out without any
    /// credentials, and was answered 401. With a TOTP login, the token goes in
    /// "Authorization: TOTP", which is what the WebSocket server verifies;
    /// without one, in the "TOTP" request header, as the HTTP client sends it.
    /// </remarks>
    [TestFixture]
    public class WebSocketClientTOTPTests
    {

        #region Data

        private const String Secret = "abcdefghijklmnop";

        private static TOTPConfig Raw(TimeSpan? Validity = null)
            => new (Secret, Validity, UseTLSExporterMaterial: false);

        private static TOTPConfig BoundToTLS
            => new (Secret, UseTLSExporterMaterial: true);

        /// <summary>
        /// Reconnect attempts close together, so that a test does not wait on
        /// the default second.
        /// </summary>
        private static WebSocketClientReconnectPolicy Quickly
            => new (InitialDelay: TimeSpan.FromMilliseconds(200),
                    MaxDelay:     TimeSpan.FromMilliseconds(500));

        private WebSocketServer?  server;
        private WebSocketClient?  client;

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.DisposeAsync();

            if (server is not null)
            {
                await server.Shutdown(Wait: true);
                await server.DisposeAsync();
            }

            client  = null;
            server  = null;

        }

        #endregion

        #region (private static) AcceptedLogins(Server)

        // The Login of every connection the server accepts. Accepted comes
        // before the 101 goes out, so it is in here once Connect has its answer.
        private static ConcurrentQueue<String?> AcceptedLogins(AWebSocketServer Server)
        {

            var logins = new ConcurrentQueue<String?>();

            Server.OnWebSocketConnectionAccepted += (timestamp, webSocketServer, connection, sharedSubprotocols,
                                                     selectedSubprotocol, eventTrackingId, cancellationToken) => {
                logins.Enqueue(connection.Login);
                return Task.CompletedTask;
            };

            return logins;

        }

        #endregion

        #region (private static) Matches(Token, Validity)

        /// <summary>
        /// Whether the token is one a verifier takes now: the previous, the
        /// current or the next of the secret.
        /// </summary>
        private static Boolean Matches(String Token, TimeSpan? Validity = null)
        {

            var (previous, current, next, _, _) = TOTPGenerator.GenerateTOTPs(Secret, Validity);

            return Token == previous || Token == current || Token == next;

        }

        #endregion


        #region AClientWithATOTPLoginIsLetIn()

        /// <summary>
        /// Login and one-time password, and nothing else: the server that knows
        /// the login lets the client in under it.
        /// </summary>
        [Test]
        public async Task AClientWithATOTPLoginIsLetIn()
        {

            server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);
            server.ClientTOTPConfig["station"] = Raw();

            var logins = AcceptedLogins(server);

            client = new WebSocketClient(
                         URL.Parse($"ws://127.0.0.1:{server.IPPort}"),
                         TOTPConfig:  Raw(),
                         TOTPLogin:   "station"
                     );

            var (_, response) = await client.Connect();

            Assert.Multiple(() => {

                Assert.That(response.HTTPStatusCode.Code, Is.EqualTo(101),
                            "A client with a TOTP login and a TOTP configuration was not let in.");

                Assert.That(logins, Is.EqualTo(new String?[] { "station" }),
                            "The server did not take the client for the login it named.");

            });

        }

        #endregion

        #region AClientThatComesBackLaterIsLetInAgain()

        /// <summary>
        /// A reconnect long after the first connection sends a token of its own
        /// moment, not the one the client started with.
        /// </summary>
        /// <remarks>
        /// With a validity of one second, a token is taken for at most three:
        /// the server accepts the previous, the current and the next one. The
        /// server is away for longer than that, so a client that sent its first
        /// token again would be answered 401 - which is final, and it would not
        /// come back at all.
        /// </remarks>
        [Test]
        public async Task AClientThatComesBackLaterIsLetInAgain()
        {

            var validity = TimeSpan.FromSeconds(1);

            using var port = new ClosedPort();

            port.HandOver();

            server = new WebSocketServer(HTTPPort: port.Number, AutoStart: true);
            server.ClientTOTPConfig["station"] = Raw(validity);

            client = new WebSocketClient(
                         URL.Parse($"ws://127.0.0.1:{port}"),
                         TOTPConfig:  Raw(validity),
                         TOTPLogin:   "station"
                     ) {
                         ReconnectPolicy = Quickly
                     };

            var (_, first) = await client.Connect();

            Assert.That(first.HTTPStatusCode.Code, Is.EqualTo(101),
                        "The client was not let in to begin with.");

            await server.Shutdown("Back in a few seconds.", Wait: true);
            await server.DisposeAsync();

            port.TakeBack();

            // Longer than the three seconds the first token could be taken for.
            await Task.Delay(TimeSpan.FromSeconds(4));

            port.HandOver();

            server = new WebSocketServer(HTTPPort: port.Number, AutoStart: true);
            server.ClientTOTPConfig["station"] = Raw(validity);

            var logins = AcceptedLogins(server);
            var giveUp = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);

            while (DateTimeOffset.UtcNow < giveUp && logins.IsEmpty)
                await Task.Delay(50);

            Assert.That(logins, Is.EqualTo(new String?[] { "station" }),
                        "The client came back with a token that was no longer current, or did not come back.");

        }

        #endregion

        #region AClientWithoutATOTPLoginSendsTheTOTPHeader()

        /// <summary>
        /// A one-time password without a login goes in the "TOTP" request
        /// header, as the HTTP client sends it: for a server that knows who is
        /// calling by other means - the path, a client certificate.
        /// </summary>
        [Test]
        public async Task AClientWithoutATOTPLoginSendsTheTOTPHeader()
        {

            server = new WebSocketServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            HTTPRequest? sent = null;

            client = new WebSocketClient(
                         URL.Parse($"ws://127.0.0.1:{server.IPPort}"),
                         TOTPConfig: Raw()
                     );

            client.RequestLogDelegate += (timestamp, webSocketClient, request) => {
                sent = request;
                return Task.CompletedTask;
            };

            var (_, response) = await client.Connect();

            Assert.Multiple(() => {

                Assert.That(response.HTTPStatusCode.Code, Is.EqualTo(101));

                Assert.That(sent?.TOTP, Is.Not.Null,
                            "The upgrade request carried no TOTP header.");

                Assert.That(sent?.TOTP?.Type, Is.EqualTo(TOTPHTTPHeaderType.RAW));

                Assert.That(Matches(sent?.TOTP?.Value ?? ""), Is.True,
                            "The TOTP header did not carry a current token of the secret.");

                Assert.That(sent?.Authorization, Is.Null,
                            "Without a TOTP login, nothing should have gone into the Authorization header.");

            });

        }

        #endregion


        #region ATOTPBoundToTLSIsRefusedBeforeAnythingIsSent()

        /// <summary>
        /// Asked to bind the token to the TLS session, the client cannot: its TLS
        /// exports no keying material. It sends nothing rather than a token that
        /// is weaker than it was configured to be - neither under a login nor in
        /// the TOTP header.
        /// </summary>
        [Test]
        public async Task ATOTPBoundToTLSIsRefusedBeforeAnythingIsSent([Values] Boolean WithALogin)
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port      = ((IPEndPoint) listener.LocalEndpoint).Port;
            var accepted  = listener.AcceptTcpClientAsync();

            client = new WebSocketClient(
                         URL.Parse($"ws://127.0.0.1:{port}"),
                         TOTPConfig:  BoundToTLS,
                         TOTPLogin:   WithALogin ? "station" : null
                     );

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.Connect(),
                                                                         "A client asked to bind its TOTP to TLS connected anyway.");

            await Task.Delay(200);

            Assert.Multiple(() => {

                Assert.That(refused?.Message, Does.Contain("TLS channel binding"),
                            "The refusal does not say what was asked for.");

                // The client may have been going to open its WebSocket over
                // HTTP/3, where there is no SslStream to blame.
                Assert.That(refused?.Message, Does.Contain("HTTP/1.1 and HTTP/2").And.Contain("HTTP/3"),
                            "The refusal speaks of one TLS only, and the client has two.");

                Assert.That(accepted.IsCompleted, Is.False,
                            "The client opened a connection before refusing.");

            });

        }

        #endregion

        #region ATOTPLoginWithoutATOTPConfigIsRefused()

        /// <summary>
        /// A login with nothing to make a password from: not sent as though it
        /// were a client without credentials.
        /// </summary>
        [Test]
        public async Task ATOTPLoginWithoutATOTPConfigIsRefused()
        {

            client = new WebSocketClient(
                         URL.Parse("ws://127.0.0.1:9"),
                         TOTPLogin: "station"
                     );

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.Connect());

            Assert.That(refused?.Message, Does.Contain("TOTP login"));

        }

        #endregion

        #region ATOTPLoginBesideAnotherAuthenticationIsRefused()

        /// <summary>
        /// An upgrade request has one Authorization header. Two ways to fill it
        /// is a configuration that does not say which one it means.
        /// </summary>
        [Test]
        public async Task ATOTPLoginBesideAnotherAuthenticationIsRefused()
        {

            client = new WebSocketClient(
                         URL.Parse("ws://127.0.0.1:9"),
                         HTTPAuthentication:  HTTPBasicAuthentication.Create("station", "a-password"),
                         TOTPConfig:          Raw(),
                         TOTPLogin:           "station"
                     );

            var refused = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.Connect());

            Assert.That(refused?.Message, Does.Contain("Authorization"));

        }

        #endregion


        #region AnHTTPClientRefusesATOTPBoundToTLS()

        /// <summary>
        /// The HTTP client the same: it used to compute the token without
        /// keying material and send it labelled as bound to the TLS session.
        /// </summary>
        [Test]
        public async Task AnHTTPClientRefusesATOTPBoundToTLS()
        {

            using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port      = ((IPEndPoint) listener.LocalEndpoint).Port;
            var accepted  = listener.AcceptTcpClientAsync();

            using var httpClient = new HTTPClient(URL.Parse($"http://127.0.0.1:{port}")) {
                                       TOTPConfig = BoundToTLS
                                   };

            var response = await httpClient.GET(HTTPPath.Root, RequestTimeout: TimeSpan.FromSeconds(5));

            await Task.Delay(200);

            Assert.Multiple(() => {

                Assert.That(response.HTTPStatusCode, Is.EqualTo(HTTPStatusCode.ClientError));

                Assert.That(response.HTTPBodyAsUTF8String, Does.Contain("TLS channel binding"),
                            "The refusal does not say what was asked for.");

                Assert.That(accepted.IsCompleted, Is.False,
                            "The HTTP client sent the request before refusing.");

            });

        }

        #endregion

    }

}
