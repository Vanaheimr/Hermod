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

using System.Net.Sockets;
using System.Text;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// The TCP server's Warden and a connection whose handler has not reached its
    /// first await yet.
    ///
    /// The Warden reaps a connection when its handler task has completed. A new
    /// connection is entered before its handler starts, so that a handler which
    /// finishes at once finds its entry to remove, and the real task takes the
    /// entry's place only when HandleNewTCPClientAsync returns - at its first
    /// await that does not complete at once. Whatever stands in the entry until
    /// then must not look finished, or the Warden closes a connection under a
    /// handler that is still starting: accepting TLS, validating, or on a fresh
    /// process being compiled on its first request.
    ///
    /// How long a handler takes to start is not up to a test, so this one makes
    /// it long on purpose: ValidateConnection holds the start until the Warden
    /// has looked, which it does once, a second after the server was made.
    /// </summary>
    [TestFixture]
    public class WardenTests
    {

        #region (class) SlowStartingEchoServer

        /// <summary>
        /// An echo server whose connections are validated slowly, and
        /// synchronously - before the handler's first await - and whose Warden
        /// looks once, soon.
        /// </summary>
        private sealed class SlowStartingEchoServer : ATCPServer
        {

            private readonly Action hold;

            public SlowStartingEchoServer(TimeSpan                WardenInitialDelay,
                                          TCPEchoLoggingDelegate  LoggingHandler,
                                          Action                  Hold)

                : base(TCPPort:             IPPort.Parse(0),
                       LoggingHandler:      LoggingHandler,
                       WardenInitialDelay:  WardenInitialDelay)

            {
                this.hold = Hold;
            }

            public override Task<ConnectionFilterResponse> ValidateConnection(DateTimeOffset     Timestamp,
                                                                              ITCPServer         Server,
                                                                              TCPConnection      Connection,
                                                                              EventTracking_Id   EventTrackingId,
                                                                              CancellationToken  CancellationToken)
            {
                hold();
                return Task.FromResult(ConnectionFilterResponse.Accepted());
            }

            protected override async Task HandleConnection(TCPConnection Connection, CancellationToken Token)
            {
                await using var stream = Connection.TCPClient.GetStream();
                await stream.CopyToAsync(stream, bufferSize: 4096, Token).ConfigureAwait(false);
            }

        }

        #endregion


        #region AConnectionWhoseHandlerIsStillStartingIsNotReaped()

        [Test]
        public async Task AConnectionWhoseHandlerIsStillStartingIsNotReaped()
        {

            // What the Warden says as it begins to look, with the number of
            // connections it is looking at.
            var looked = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            var server = new SlowStartingEchoServer(
                             WardenInitialDelay:  TimeSpan.FromSeconds(1),
                             LoggingHandler:      message => {
                                                      if (message.Contains("Warden: Checking active TCP clients"))
                                                          looked.TrySetResult(message);
                                                      return Task.CompletedTask;
                                                  },
                             // Until the Warden has looked, and a moment longer for it
                             // to finish looking: it reaps in the same pass.
                             Hold:                () => {
                                                      looked.Task.Wait(TimeSpan.FromSeconds(10));
                                                      Thread.Sleep(200);
                                                  }
                         );

            await server.Start();

            using var peer = new TcpClient();
            await peer.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToUInt16());

            var stream  = peer.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("ping"));

            var echo    = new Byte[4];
            var read    = 0;

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                try
                {
                    while (read < echo.Length)
                    {

                        var justRead = await stream.ReadAsync(echo.AsMemory(read), timeout.Token);

                        if (justRead == 0)
                            break;

                        read += justRead;

                    }
                }
                catch (IOException)
                {
                    // Closed under the peer: what this test is about, and said below.
                }
            }

            await server.Stop();

            Assert.Multiple(() => {

                // Looking too early - before the connection was entered - would
                // let this test pass without having tested anything.
                Assert.That(
                    looked.Task.IsCompletedSuccessfully ? looked.Task.Result : "",
                    Does.Contain("(1)"),
                    "the Warden looked while the connection's handler was still starting"
                );

                Assert.That(
                    Encoding.ASCII.GetString(echo, 0, read),
                    Is.EqualTo("ping"),
                    "and the connection was still there afterwards"
                );

            });

        }

        #endregion

    }

}
