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

using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.TCP
{

    /// <summary>
    /// A TCP client made with DisableLogging says nothing on OnLogs.
    /// </summary>
    /// <remarks>
    /// ATCPClient took DisableLogging, documented it as "Disable logging of
    /// connection events and errors." and kept it in a property, but nothing
    /// read that property: Log() raised OnLogs whatever it said. A connect
    /// that succeeds logs "Client connected!", and CloseConnection() logs
    /// "TCP connection closed!" - both reach the handler here.
    /// </remarks>
    [TestFixture]
    public class DisableLoggingTests
    {

        #region Data

        /// <summary>
        /// How long a test waits for a connect or a close.
        /// </summary>
        private static readonly TimeSpan  Patience  = TimeSpan.FromSeconds(10);

        #endregion

        #region (private static) ConnectAndClose(DisableLogging)

        /// <summary>
        /// Connect a TCP client to a listener on 127.0.0.1, close the
        /// connection again, and return what the client said on OnLogs.
        /// </summary>
        private static async Task<String[]> ConnectAndClose(Boolean? DisableLogging)
        {

            var listener   = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();

            try
            {

                // Lent to the client: one it makes itself outlives it.
                await using var dnsClient  = new DNSClient(ManualDNSServers: []);

                await using var client     = new TCPClient(URL.Parse($"tcp://127.0.0.1:{((IPEndPoint) listener.LocalEndpoint).Port}"),
                                                           DisableLogging:  DisableLogging,
                                                           DNSClient:       dnsClient);

                var messages = new ConcurrentQueue<String>();

                client.OnLogs += message => {
                    messages.Enqueue(message);
                    return Task.CompletedTask;
                };

                var result = await client.ConnectAsync().WaitAsync(Patience);

                Assert.That(result.IsSuccess, Is.True, "whether the client connected");

                using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(Patience);

                await client.CloseConnection().WaitAsync(Patience);

                return [.. messages];

            }
            finally
            {
                listener.Stop();
            }

        }

        #endregion


        #region AClientWithLoggingDisabledSaysNothing()

        [Test]
        public async Task AClientWithLoggingDisabledSaysNothing()
        {

            var messages = await ConnectAndClose(DisableLogging: true);

            Assert.That(messages, Is.Empty);

        }

        #endregion

        #region AClientWithLoggingEnabledSaysWhatHappened()

        /// <summary>
        /// The same connect and close with logging left on, the default: what
        /// the test above expects not to hear is said at all.
        /// </summary>
        [Test]
        public async Task AClientWithLoggingEnabledSaysWhatHappened()
        {

            var messages = await ConnectAndClose(DisableLogging: null);

            Assert.That(messages, Is.EqualTo(new[] { "Client connected!", "TCP connection closed!" }));

        }

        #endregion

    }

}
