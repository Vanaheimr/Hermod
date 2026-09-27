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

using System.Text;
using System.Collections.Concurrent;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    [TestFixture]
    public class WebSocketSecurityTests
    {

        private static async Task<HTTPResponse> Connect(WebSocketServer     server,
                                                        IHTTPAuthentication? authentication = null)
        {
            var client = new WebSocketClient(
                             URL.Parse($"ws://127.0.0.1:{server.IPPort}"),
                             HTTPAuthentication: authentication
                         );

            try
            {
                return (await client.Connect()).Item2;
            }
            finally
            {
                client.Disconnect();
            }
        }

        // The Login of every connection the server accepts. Accepted comes
        // before the 101 goes out, so it is in here once Connect has its answer.
        private static ConcurrentQueue<String?> AcceptedLogins(AWebSocketServer server)
        {
            var logins = new ConcurrentQueue<String?>();

            server.OnWebSocketConnectionAccepted += (timestamp, webSocketServer, connection, sharedSubprotocols,
                                                     selectedSubprotocol, eventTrackingId, cancellationToken) => {
                logins.Enqueue(connection.Login);
                return Task.CompletedTask;
            };

            return logins;
        }

        [Test]
        public async Task AuthenticationIsRequiredByDefault()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);

            try
            {
                Assert.That((await Connect(server)).HTTPStatusCode.Code, Is.EqualTo(401));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task BasicAuthenticationVerifiesStoredPassword()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);
            server.AddOrUpdateHTTPBasicAuth("alice", "correct-password");

            try
            {
                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("alice", "wrong-password"))).HTTPStatusCode.Code,
                            Is.EqualTo(401));

                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("alice", "correct-password"))).HTTPStatusCode.Code,
                            Is.EqualTo(101));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AnonymousModeRemainsAvailableWhenExplicitlySelected()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero,
                                             RequireAuthentication: false,
                                             AutoStart: true);

            try
            {
                Assert.That((await Connect(server)).HTTPStatusCode.Code, Is.EqualTo(101));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task RawTOTPIsAcceptedOnlyWithMatchingConfiguration()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);
            var secret = "abcdefghijklmnop";
            server.ClientTOTPConfig["station"] = new TOTPConfig(secret, UseTLSExporterMaterial: false);

            try
            {
                var token = TOTPGenerator.GenerateTOTP(secret).Current;

                Assert.That((await Connect(server,
                                           HTTPTOTPAuthentication.Create("station", token,
                                                                         TOTPHTTPHeaderType.RAW))).HTTPStatusCode.Code,
                            Is.EqualTo(101));

                Assert.That((await Connect(server,
                                           HTTPTOTPAuthentication.Create("station", token,
                                                                         TOTPHTTPHeaderType.TLSChannelBinding))).HTTPStatusCode.Code,
                            Is.EqualTo(401));

                server.ClientTOTPConfig["station"] = new TOTPConfig(secret, UseTLSExporterMaterial: true);
                Assert.That((await Connect(server,
                                           HTTPTOTPAuthentication.Create("station", token,
                                                                         TOTPHTTPHeaderType.RAW))).HTTPStatusCode.Code,
                            Is.EqualTo(401));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AWrongTOTPIsRefused()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);
            var secret = "abcdefghijklmnop";
            server.ClientTOTPConfig["station"] = new TOTPConfig(secret, UseTLSExporterMaterial: false);

            try
            {
                // Of the right length and alphabet, for the right login, raw -
                // and generated from another secret.
                var wrong = TOTPGenerator.GenerateTOTP("qrstuvwxyzabcdef").Current;

                Assert.That((await Connect(server,
                                           HTTPTOTPAuthentication.Create("station", wrong,
                                                                         TOTPHTTPHeaderType.RAW))).HTTPStatusCode.Code,
                            Is.EqualTo(401));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AnAnonymousConnectionHasNoLogin()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero,
                                             RequireAuthentication: false,
                                             AutoStart: true);
            var logins = AcceptedLogins(server);

            try
            {
                // Credentials sent along are nobody's word for anything here.
                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("alice", "never-checked"))).HTTPStatusCode.Code,
                            Is.EqualTo(101));

                Assert.That(logins, Is.EqualTo(new String?[] { null }));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AnAuthenticatedConnectionCarriesItsLogin()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero, AutoStart: true);
            server.AddOrUpdateHTTPBasicAuth("alice", "correct-password");
            var logins = AcceptedLogins(server);

            try
            {
                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("alice", "correct-password"))).HTTPStatusCode.Code,
                            Is.EqualTo(101));

                Assert.That(logins, Is.EqualTo(new String?[] { "alice" }));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AnOverrideKnowsClientsOfItsOwnAndAsksTheBaseForTheRest()
        {
            var server = new StationServer();
            server.Stations["cs01"] = "station-secret";
            server.AddOrUpdateHTTPBasicAuth("alice", "correct-password");
            var logins = AcceptedLogins(server);

            try
            {
                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("cs01", "station-secret"))).HTTPStatusCode.Code,
                            Is.EqualTo(101));

                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("alice", "correct-password"))).HTTPStatusCode.Code,
                            Is.EqualTo(101));

                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("cs01", "wrong-secret"))).HTTPStatusCode.Code,
                            Is.EqualTo(401));

                Assert.That(logins, Is.EqualTo(new String?[] { "station cs01", "alice" }));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AnOverrideThatThrowsRefusesWithAServerError()
        {
            var server = new ThrowingServer();
            server.AddOrUpdateHTTPBasicAuth("alice", "correct-password");

            try
            {
                Assert.That((await Connect(server,
                                           HTTPBasicAuthentication.Create("alice", "correct-password"))).HTTPStatusCode.Code,
                            Is.EqualTo(500));
            }
            finally
            {
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public void InboundMessagesHaveFiniteDefaults()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero);
            var client = new WebSocketClient(URL.Parse("ws://127.0.0.1:12345"));

            Assert.That(server.MaxTextMessageSizeIn,   Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
            Assert.That(server.MaxBinaryMessageSizeIn, Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
            Assert.That(client.MaxTextMessageSizeIn,   Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
            Assert.That(client.MaxBinaryMessageSizeIn, Is.EqualTo(WebSocketFrame.DefaultMaxPayloadSize));
        }

        [Test]
        public async Task FragmentedMessageExceedingConfiguredLimitIsClosed()
        {
            var server = new WebSocketServer(HTTPPort: IPPort.Zero,
                                             RequireAuthentication: false,
                                             AutoStart: true) {
                             MaxTextMessageSizeIn = 5
                         };

            var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}"));
            var close  = new TaskCompletionSource<WebSocketFrame.ClosingStatusCode>(
                             TaskCreationOptions.RunContinuationsAsynchronously
                         );

            client.OnCloseMessageReceived += (timestamp, sender, connection, frame,
                                              eventTrackingId, statusCode, reason, cancellationToken) => {
                close.TrySetResult(statusCode);
                return Task.CompletedTask;
            };

            try
            {
                Assert.That((await client.Connect()).Item2.HTTPStatusCode.Code, Is.EqualTo(101));

                await client.SendWebSocketFrame(WebSocketFrame.Text("abc", WebSocketFrame.Fin.More,
                                                                   Mask: WebSocketFrame.MaskStatus.On,
                                                                   MaskingKey: [1, 2, 3, 4]));

                await client.SendWebSocketFrame(WebSocketFrame.Continuation(Encoding.UTF8.GetBytes("def"),
                                                                           Mask: WebSocketFrame.MaskStatus.On,
                                                                           MaskingKey: [5, 6, 7, 8]));

                Assert.That(await close.Task.WaitAsync(TimeSpan.FromSeconds(5)),
                            Is.EqualTo(WebSocketFrame.ClosingStatusCode.MessageTooBig));
            }
            finally
            {
                client.Disconnect();
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public void IncompleteFrameDoesNotAllocateAdvertisedPayload()
        {
            // Masked binary frame advertising 64 MiB, without its payload.
            Byte[] header = [0x82, 0xff, 0, 0, 0, 0, 4, 0, 0, 0, 1, 2, 3, 4];

            var before = GC.GetAllocatedBytesForCurrentThread();
            var result = WebSocketFrame.Parse(header,
                                              out _, out _, out _, out _);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(result, Is.EqualTo(WebSocketFrame.ParseResult.IncompleteData));
            Assert.That(allocated, Is.LessThan(1024 * 1024));
        }

        [Test]
        public void DecompressionHonorsMessageLimit()
        {
            var extension  = new WebSocketPerMessageDeflate();
            var plainText  = Encoding.UTF8.GetBytes(new String('a', 4096));
            var compressed = extension.Compress(plainText);

            Assert.That(extension.TryDecompress(compressed, out _, out _, out var exceeded,
                                                MessageSizeLimit: 128), Is.False);
            Assert.That(exceeded, Is.True);

            Assert.That(extension.TryDecompress(compressed, out var decompressed, out _, out exceeded,
                                                MessageSizeLimit: 4096), Is.True);
            Assert.That(exceeded, Is.False);
            Assert.That(decompressed, Is.EqualTo(plainText));
        }

        [Test]
        public async Task AServerRefusesACompressedMessageThatInflatesOverItsLimit()
        {
            // 4096 times the same letter compresses to a few bytes: far under the
            // limit on the wire, far over it once inflated.
            var server = new WebSocketServer(HTTPPort: IPPort.Zero,
                                             RequireAuthentication: false,
                                             AutoStart: true) {
                             EnablePerMessageDeflate  = true,
                             MaxTextMessageSizeIn     = 100
                         };

            var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}")) {
                             EnablePerMessageDeflate  = true
                         };

            var close  = new TaskCompletionSource<WebSocketFrame.ClosingStatusCode>(
                             TaskCreationOptions.RunContinuationsAsynchronously
                         );

            client.OnCloseMessageReceived += (timestamp, sender, connection, frame,
                                              eventTrackingId, statusCode, reason, cancellationToken) => {
                close.TrySetResult(statusCode);
                return Task.CompletedTask;
            };

            try
            {
                var response = (await client.Connect()).Item2;

                Assert.That(response.HTTPStatusCode.Code,                        Is.EqualTo(101));
                Assert.That(response.GetHeaderField("Sec-WebSocket-Extensions"), Does.StartWith("permessage-deflate"));

                await client.SendTextMessage(new String('a', 4096));

                Assert.That(await close.Task.WaitAsync(TimeSpan.FromSeconds(5)),
                            Is.EqualTo(WebSocketFrame.ClosingStatusCode.MessageTooBig));
            }
            finally
            {
                client.Disconnect();
                await server.Shutdown(Wait: true);
            }
        }

        [Test]
        public async Task AClientRefusesABinaryMessageOverItsLimit()
        {
            // The mirror sends back what it gets, as many bytes as it got: six out
            // are six in, and it is the client's limit that is asked, not the server's.
            var server = new WebSocketMirrorServer(HTTPPort: IPPort.Zero,
                                                   RequireAuthentication: false,
                                                   AutoStart: true);

            var client = new WebSocketClient(URL.Parse($"ws://127.0.0.1:{server.IPPort}")) {
                             MaxBinaryMessageSizeIn  = 5
                         };

            var close  = new TaskCompletionSource<WebSocketFrame.ClosingStatusCode>(
                             TaskCreationOptions.RunContinuationsAsynchronously
                         );

            server.OnCloseMessageReceived += (timestamp, webSocketServer, connection, frame,
                                              eventTrackingId, statusCode, reason, cancellationToken) => {
                close.TrySetResult(statusCode);
                return Task.CompletedTask;
            };

            try
            {
                Assert.That((await client.Connect()).Item2.HTTPStatusCode.Code, Is.EqualTo(101));

                await client.SendBinaryMessage([1, 2, 3, 4, 5, 6]);

                Assert.That(await close.Task.WaitAsync(TimeSpan.FromSeconds(5)),
                            Is.EqualTo(WebSocketFrame.ClosingStatusCode.MessageTooBig));
            }
            finally
            {
                client.Disconnect();
                await server.Shutdown(Wait: true);
            }
        }

        /// <summary>
        /// Knows its stations from a store of its own, the way the WWCP servers
        /// do, and everybody else from the base.
        /// </summary>
        private sealed class StationServer : WebSocketServer
        {

            public ConcurrentDictionary<String, String> Stations { get; } = [];

            public StationServer()
                : base(HTTPPort: IPPort.Zero, AutoStart: true)
            { }

            protected override Task<String?> AuthenticateAsync(WebSocketServerConnection  Connection,
                                                               HTTPRequest                Request,
                                                               CancellationToken          CancellationToken)

                => Request.Authorization is HTTPBasicAuthentication basicAuthentication &&
                   Stations.TryGetValue(basicAuthentication.Username, out var secret) &&
                   secret == basicAuthentication.Password

                       ? Task.FromResult<String?>($"station {basicAuthentication.Username}")
                       : base.AuthenticateAsync(Connection, Request, CancellationToken);

        }

        /// <summary>
        /// An override failing the way somebody else's code does.
        /// </summary>
        private sealed class ThrowingServer : WebSocketServer
        {

            public ThrowingServer()
                : base(HTTPPort: IPPort.Zero, AutoStart: true)
            { }

            protected override Task<String?> AuthenticateAsync(WebSocketServerConnection  Connection,
                                                               HTTPRequest                Request,
                                                               CancellationToken          CancellationToken)

                => throw new InvalidOperationException("The store of logins is not there.");

        }

    }

}
