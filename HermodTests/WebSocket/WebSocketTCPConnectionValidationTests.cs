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

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// How a WebSocket server under test comes by its connections.
    /// </summary>
    public enum WebSocketServerArrangement
    {

        /// <summary>
        /// It listens on a port of its own, and accepts them itself.
        /// </summary>
        OnAPortOfItsOwn,

        /// <summary>
        /// It is lent to a path of an HTTP server, which accepts them and hands
        /// them over.
        /// </summary>
        LentToAnHTTPPath

    }


    /// <summary>
    /// A handler of OnValidateTCPConnection that refuses a connection has it
    /// refused - and so does a handler that fails - whether the server accepted
    /// the connection on a port of its own or had it handed over by an HTTP
    /// server it is lent to.
    /// </summary>
    /// <remarks>
    /// The verdict was the first answer, compared with a Rejected() made for
    /// the comparison. ConnectionFilterResponse is a class that does not say
    /// what makes two of them equal, so that asked whether the answer was the
    /// very object just made - which it never was - and every connection was
    /// let in, whatever its handlers said. A handler that threw was logged,
    /// and the connection let in as well: a firewall that fails open.
    ///
    /// Now the first refusal among all answers decides, as it does for
    /// OnValidateWebSocketConnection, and a handler that throws has refused.
    /// The servers here ask nobody for credentials, so that the handlers under
    /// test are the only thing that can turn a client away.
    ///
    /// Every test runs twice, once for each arrangement. A server lent to an
    /// HTTP path accepts no connection itself, and its handlers used not to be
    /// asked at all; now they are asked when the HTTP server hands a connection
    /// over, and the same verdict has to come out of the same answers.
    /// </remarks>
    [TestFixture(WebSocketServerArrangement.OnAPortOfItsOwn)]
    [TestFixture(WebSocketServerArrangement.LentToAnHTTPPath)]
    public class WebSocketTCPConnectionValidationTests
    {

        #region Data

        private readonly WebSocketServerArrangement  arrangement;

        private HTTPServer?                          httpServer;
        private WebSocketMirrorServer?               server;
        private WebSocketClient?                     client;
        private TaskCompletionSource<String>?        refusal;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// The tests, for a server in the given arrangement.
        /// </summary>
        /// <param name="Arrangement">How the server under test comes by its connections.</param>
        public WebSocketTCPConnectionValidationTests(WebSocketServerArrangement Arrangement)
        {
            this.arrangement = Arrangement;
        }

        #endregion

        #region TearDown()

        [TearDown]
        public async Task TearDown()
        {

            if (client is not null)
                await client.Close();

            if (server is not null)
                await server.Shutdown(Wait: true);

            if (httpServer is not null)
                await httpServer.Stop();

            client      = null;
            server      = null;
            httpServer  = null;
            refusal     = null;

        }

        #endregion


        #region (private) StartServer(params Validators)

        /// <summary>
        /// Start a WebSocket server that asks the given handlers whether a new
        /// TCP connection may come in, and remember why it refused one.
        /// </summary>
        /// <param name="Validators">The handlers of OnValidateTCPConnection, in the order they are added.</param>
        private async Task StartServer(params OnValidateTCPConnectionDelegate[] Validators)
        {

            if (arrangement == WebSocketServerArrangement.OnAPortOfItsOwn)
                server      = new WebSocketMirrorServer(
                                  HTTPPort:               IPPort.Zero,
                                  RequireAuthentication:  false,
                                  AutoStart:              true
                              );

            else
            {

                httpServer  = await HTTPServer.StartNew();

                // Not started: it accepts nothing, and every connection it
                // speaks on is one the HTTP server accepted and hands over.
                server      = new WebSocketMirrorServer(
                                  RequireAuthentication:  false,
                                  AutoStart:              false
                              );

                httpServer.AddHTTPAPI().AddHandler(
                    HTTPMethod.GET,
                    HTTPPath.Parse("/lent"),
                    HTTPDelegate: WebSocketUpgrade.For(server)
                );

            }

            refusal  = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            foreach (var validator in Validators)
                server.OnValidateTCPConnection += validator;

            server.OnNewTCPConnectionRejected += (tcpServer, timestamp, eventTrackingId, remoteSocket, connectionId, reason) => {
                refusal.TrySetResult(reason.FirstText());
                return Task.CompletedTask;
            };

        }

        #endregion

        #region (private) Connect()

        /// <summary>
        /// Ask the server for a WebSocket connection, and return its answer.
        /// </summary>
        private async Task<HTTPResponse> Connect()
        {

            client = new WebSocketClient(URL.Parse(
                         arrangement == WebSocketServerArrangement.OnAPortOfItsOwn
                             ? $"ws://127.0.0.1:{server!.IPPort}"
                             : $"ws://127.0.0.1:{httpServer!.TCPPort}/lent"
                     ));

            var (_, httpResponse) = await client.Connect();

            return httpResponse;

        }

        #endregion

        #region (private) Refusal()

        /// <summary>
        /// The reason the server gave when it refused the connection - or a
        /// TimeoutException, where it never did.
        /// </summary>
        private Task<String> Refusal()

            => refusal!.Task.WaitAsync(TimeSpan.FromSeconds(5));

        #endregion

        #region (private static) Answer(Response)

        /// <summary>
        /// A handler of OnValidateTCPConnection that answers the given response.
        /// </summary>
        /// <param name="Response">The answer to every connection.</param>
        private static OnValidateTCPConnectionDelegate Answer(ConnectionFilterResponse Response)

            => (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => Task.FromResult(Response);

        #endregion


        #region AHandlerThatSaysNoRefusesTheConnection()

        /// <summary>
        /// A handler that answers Rejected: the client is not upgraded, and the
        /// server refuses the connection for the handler's reason.
        /// </summary>
        [Test]
        public async Task AHandlerThatSaysNoRefusesTheConnection()
        {

            await StartServer(Answer(ConnectionFilterResponse.Rejected("Not from this network.")));

            var httpResponse = await Connect();

            Assert.That(httpResponse.HTTPStatusCode,  Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols),  "The connection was let in.");
            Assert.That(await Refusal(),              Is.EqualTo("Not from this network."));

        }

        #endregion

        #region AHandlerThatFailsRefusesTheConnection(Asynchronously)

        /// <summary>
        /// A handler that throws - before it has returned a task at all, or
        /// through the task it returned - has not said that the connection may
        /// come in, so it does not. The reason says what went wrong: the
        /// server's logger is optional, and this may be all there is.
        /// </summary>
        /// <param name="Asynchronously">Whether the handler fails in the task it returned, rather than before returning one.</param>
        [TestCase(false, TestName = "AHandlerThatFailsRefusesTheConnection(before it answers)")]
        [TestCase(true,  TestName = "AHandlerThatFailsRefusesTheConnection(while it answers)")]
        public async Task AHandlerThatFailsRefusesTheConnection(Boolean Asynchronously)
        {

            OnValidateTCPConnectionDelegate fails = Asynchronously

                ? async (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) => {
                      await Task.Yield();
                      throw new InvalidOperationException("The allow-list could not be read.");
                  }

                : (timestamp, webSocketServer, connection, eventTrackingId, cancellationToken) =>
                      throw new InvalidOperationException("The allow-list could not be read.");

            await StartServer(fails);

            var httpResponse = await Connect();

            Assert.That(httpResponse.HTTPStatusCode,  Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols),  "The connection was let in.");
            Assert.That(await Refusal(),              Does.Contain("The allow-list could not be read."));

        }

        #endregion

        #region AYesDoesNotOutvoteANoAfterIt()

        /// <summary>
        /// The first refusal, and not the first answer: a handler that says yes
        /// does not outvote one after it that says no.
        /// </summary>
        [Test]
        public async Task AYesDoesNotOutvoteANoAfterIt()
        {

            await StartServer(Answer(ConnectionFilterResponse.Accepted()),
                              Answer(ConnectionFilterResponse.Rejected("Not from this network.")));

            var httpResponse = await Connect();

            Assert.That(httpResponse.HTTPStatusCode,  Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols),  "The connection was let in.");
            Assert.That(await Refusal(),              Is.EqualTo("Not from this network."));

        }

        #endregion

        #region WhereNobodySaysNoTheConnectionIsLetIn()

        /// <summary>
        /// And where nobody refuses, the connection is let in - a handler with
        /// nothing to say about it, NoOperation, has not refused it either.
        /// </summary>
        [Test]
        public async Task WhereNobodySaysNoTheConnectionIsLetIn()
        {

            await StartServer(Answer(ConnectionFilterResponse.Accepted()),
                              Answer(ConnectionFilterResponse.NoOperation()));

            var httpResponse = await Connect();

            Assert.Multiple(() => {
                Assert.That(httpResponse.HTTPStatusCode,  Is.EqualTo(HTTPStatusCode.SwitchingProtocols),  httpResponse.EntirePDU);
                Assert.That(refusal!.Task.IsCompleted,    Is.False,                                       "The connection was refused.");
            });

        }

        #endregion

    }

}
