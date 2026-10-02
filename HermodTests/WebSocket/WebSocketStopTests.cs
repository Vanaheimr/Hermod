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

using System.Net.WebSockets;

using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// A WebSocket server that is stopped, rather than shut down, hangs up on
    /// its clients - and does not tell any of them that it broke the protocol.
    /// </summary>
    /// <remarks>
    /// Stop() closes every connection before it cancels the server's token, and
    /// that order is what this holds. A connection loop that ends after its 101
    /// closes with 1002, protocol error. A loop that the token ends while its
    /// connection is still open has a socket to send that frame on, and its
    /// client is told that it did something wrong when the server was only
    /// stopping. The token has to be cancelled before Stop() waits for the
    /// handlers, because an event stream waits for nothing else (see
    /// SSEServerStopTests). These tests check the other half: the connections
    /// are closed before that.
    ///
    /// Three clients, because the first is no proof. A Stop() that cancels and
    /// then closes the first connection at once usually closes its socket
    /// before that loop gets to its close frame. Where it waits for each
    /// handler before it closes the next connection, the others wait their
    /// turn, and their loops do get there: two of three clients were told
    /// 1002, in 5 of 5 runs. A Stop() that cancels and then closes all of them
    /// at once won that race for every connection, in 5 of 5 runs, and passes
    /// this test - for that order, the remarks on Stop() are the guard.
    /// </remarks>
    [TestFixture]
    public class WebSocketStopTests
    {

        #region AStoppedServerBlamesNoClient()

        [Test]
        public async Task AStoppedServerBlamesNoClient()
        {

            await using var server  = new WebSocketMirrorServer(HTTPPort: IPPort.Zero, RequireAuthentication: false, AutoStart: true);

            var clients             = new List<ClientWebSocket>();

            try
            {

                for (var i = 0; i < 3; i++)
                    clients.Add(await Connect($"ws://127.0.0.1:{server.IPPort}/"));

                var told = clients.Select(ReceiveClose).ToArray();

                await server.Stop();

                var results = await Task.WhenAll(told);

                Assert.Multiple(() => {
                    foreach (var (ended, status, problem) in results)
                    {
                        Assert.That(ended,   Is.True,                                               "the client found its connection ended");
                        Assert.That(status,  Is.Not.EqualTo(WebSocketCloseStatus.ProtocolError),  $"what the client was told: {problem}");
                    }
                });

            }
            finally
            {
                foreach (var client in clients)
                    client.Dispose();
            }

        }

        #endregion


        #region (private static) Connect(URL)

        private static async Task<ClientWebSocket> Connect(String URL)
        {

            var client = new ClientWebSocket();

            await client.ConnectAsync(new Uri(URL), CancellationToken.None);

            return client;

        }

        #endregion

        #region (private static) ReceiveClose(Client)

        /// <summary>
        /// How the client's connection ended: whether it did within ten seconds,
        /// the status of the close frame it was sent, if any, and in words what
        /// it was sent.
        /// </summary>
        private static async Task<(Boolean Ended, WebSocketCloseStatus? Status, String Problem)> ReceiveClose(ClientWebSocket Client)
        {

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {

                var result = await Client.ReceiveAsync(new Byte[1024], timeout.Token);

                return result.MessageType == WebSocketMessageType.Close
                           ? (true, result.CloseStatus, $"a close frame, {result.CloseStatus} '{result.CloseStatusDescription}'")
                           : (true, null,               $"a {result.MessageType} frame");

            }
            catch (OperationCanceledException)
            {
                return (false, null, "nothing within ten seconds");
            }
            catch (Exception e)
            {
                return (true, null, $"no close frame: {e.Message}");
            }

        }

        #endregion

    }

}
