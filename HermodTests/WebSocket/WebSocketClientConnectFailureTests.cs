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

using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.WebSocket;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP.WebSockets
{

    /// <summary>
    /// What a WebSocket client does when the server accepts the TCP connection but never
    /// answers the upgrade request: the request it sent is a proper HTTP request (CRLF line
    /// endings on every platform), and Connect() returns as soon as the connection attempt
    /// has ended, with the failure of that attempt, instead of waiting for the whole request
    /// timeout and reporting a synthetic timeout.
    /// </summary>
    [TestFixture]
    public class WebSocketClientConnectFailureTests
    {

        #region Connect_SendsCRLFLineEndings_AndReturnsWhenTheServerClosesWithoutAResponse()

        [Test]
        public async Task Connect_SendsCRLFLineEndings_AndReturnsWhenTheServerClosesWithoutAResponse()
        {

            var listener  = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            var port      = ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
            var received  = new TaskCompletionSource<String>(TaskCreationOptions.RunContinuationsAsynchronously);

            // A "server" reading the upgrade request and closing the connection without any response.
            _ = Task.Run(async () => {
                try
                {

                    using var socket  = await listener.AcceptSocketAsync();
                    var buffer        = new Byte[16 * 1024];
                    var total         = 0;

                    while (total < buffer.Length)
                    {

                        var read = await socket.ReceiveAsync(buffer.AsMemory(total), SocketFlags.None);

                        if (read <= 0)
                            break;

                        total += read;

                        if (Encoding.ASCII.GetString(buffer, 0, total).Contains("\n\n") ||
                            Encoding.ASCII.GetString(buffer, 0, total).Contains("\r\n\r\n"))
                            break;

                    }

                    received.TrySetResult(Encoding.ASCII.GetString(buffer, 0, total));

                }
                catch (Exception e)
                {
                    received.TrySetException(e);
                }
            });

            try
            {

                var webSocketClient  = new WebSocketClient(
                                           URL.Parse($"ws://127.0.0.1:{port}/"),
                                           RequestTimeout:  TimeSpan.FromSeconds(60)
                                       );

                var stopwatch        = Stopwatch.StartNew();
                var (_, response)    = await webSocketClient.Connect();
                stopwatch.Stop();

                var request          = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Multiple(() => {
                    Assert.That(request,                                     Does.StartWith("GET / HTTP/1.1\r\n"),                       "the request line ends with CRLF");
                    Assert.That(Regex.IsMatch(request, @"(?<!\r)\n"),        Is.False,                                                   "no bare line feed in the request header: " + request.Replace("\r", "\\r").Replace("\n", "\\n"));
                    Assert.That(response,                                    Is.Not.Null);
                    Assert.That(response.HTTPStatusCode,                     Is.Not.EqualTo(HTTPStatusCode.SwitchingProtocols));
                    Assert.That(stopwatch.Elapsed,                           Is.LessThan(TimeSpan.FromSeconds(30)),                       "Connect() returns when the connection attempt has ended, not after the request timeout");
                    Assert.That(response.HTTPBodyAsUTF8String ?? "",         Does.Not.Contain("Timeout of 60 seconds"),                   "the failure of the attempt is reported, not a synthetic timeout: " + response.HTTPBodyAsUTF8String);
                });

                await webSocketClient.Close();

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion

    }

}
