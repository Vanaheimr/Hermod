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

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using org.GraphDefined.Vanaheimr.Hermod.Rendezvous;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Rendezvous
{

    /// <summary>
    /// TCP helpers for tests, all of them with a timeout, so that a broken
    /// service fails the test instead of hanging it.
    /// </summary>
    public static class TestNet
    {

        /// <summary>
        /// The default timeout of all test helpers.
        /// </summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);


        #region GetFreePorts(Count)

        /// <summary>
        /// Return the given number of distinct TCP ports that were free a moment ago.
        /// All of them are bound at the same time, so that the operating system
        /// can not hand out the same port twice.
        /// </summary>
        /// <param name="Count">The number of TCP ports.</param>
        public static IPPort[] GetFreePorts(Int32 Count)
        {

            var listeners  = new List<TcpListener>(Count);
            var ports      = new IPPort[Count];

            try
            {

                for (var i = 0; i < Count; i++)
                {

                    var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
                    listener.Start();

                    listeners.Add(listener);
                    ports[i] = IPPort.Parse(((IPEndPoint) listener.LocalEndpoint).Port);

                }

            }
            finally
            {
                foreach (var listener in listeners)
                {
                    listener.Stop();
                    listener.Dispose();
                }
            }

            return ports;

        }

        #endregion

        #region IsPortFree(Port)

        /// <summary>
        /// Whether the given TCP port can be bound again.
        /// </summary>
        /// <param name="Port">A TCP port.</param>
        public static Boolean IsPortFree(IPPort Port)
        {
            try
            {

                var listener = new TcpListener(System.Net.IPAddress.Loopback, Port.ToUInt16());

                if (OperatingSystem.IsWindows())
                    listener.ExclusiveAddressUse = true;

                listener.Start();
                listener.Stop();
                listener.Dispose();

                return true;

            }
            catch (SocketException)
            {
                return false;
            }
        }

        #endregion

        #region ConnectAsync(Port)

        /// <summary>
        /// Connect to the given TCP port on the loopback interface.
        /// </summary>
        /// <param name="Port">A TCP port.</param>
        public static async Task<TcpClient> ConnectAsync(IPPort Port)
        {

            var client = new TcpClient();

            await client.ConnectAsync(System.Net.IPAddress.Loopback, Port.ToUInt16()).
                         WaitAsync(Timeout);

            return client;

        }

        #endregion

        #region SendAsync(Client, Text)

        /// <summary>
        /// Send the given text as UTF-8.
        /// </summary>
        /// <param name="Client">A TCP client.</param>
        /// <param name="Text">The text to send.</param>
        public static Task SendAsync(TcpClient  Client,
                                     String     Text)

            => SendBytesAsync(Client, Encoding.UTF8.GetBytes(Text));

        #endregion

        #region SendBytesAsync(Client, Bytes)

        /// <summary>
        /// Send all given bytes.
        /// </summary>
        /// <param name="Client">A TCP client.</param>
        /// <param name="Bytes">The bytes to send.</param>
        public static async Task SendBytesAsync(TcpClient  Client,
                                                Byte[]     Bytes)
        {

            var sent = 0;

            // The raw socket is used on purpose: TcpClient.GetStream() refuses
            // to work as soon as one direction of the socket was shut down.
            while (sent < Bytes.Length)
                sent += await Client.Client.
                                    SendAsync(Bytes.AsMemory(sent), SocketFlags.None).
                                    AsTask().
                                    WaitAsync(Timeout);

        }

        #endregion

        #region ReceiveAsync(Client, ExpectedBytes)

        /// <summary>
        /// Receive exactly the given number of bytes and return them as UTF-8 text.
        /// </summary>
        /// <param name="Client">A TCP client.</param>
        /// <param name="ExpectedBytes">The number of expected bytes.</param>
        public static async Task<String> ReceiveAsync(TcpClient  Client,
                                                      Int32      ExpectedBytes)
        {

            var (buffer, received) = await ReceiveExactAsync(Client, ExpectedBytes);

            return Encoding.UTF8.GetString(buffer, 0, received);

        }

        #endregion

        #region ReceiveExactAsync(Client, ExpectedBytes)

        /// <summary>
        /// Receive exactly the given number of bytes, or less when the peer
        /// closed its sending side.
        /// </summary>
        /// <param name="Client">A TCP client.</param>
        /// <param name="ExpectedBytes">The number of expected bytes.</param>
        public static async Task<(Byte[] Buffer, Int32 Received)> ReceiveExactAsync(TcpClient  Client,
                                                                                    Int32      ExpectedBytes)
        {

            var buffer  = new Byte[ExpectedBytes];
            var offset  = 0;

            while (offset < ExpectedBytes)
            {

                var received = await Client.Client.
                                            ReceiveAsync(buffer.AsMemory(offset), SocketFlags.None).
                                            AsTask().
                                            WaitAsync(Timeout);

                if (received == 0)
                    break;

                offset += received;

            }

            return (buffer, offset);

        }

        #endregion

        #region ExpectEndOfStreamAsync(Client)

        /// <summary>
        /// Expect the peer to close its sending side.
        /// </summary>
        /// <param name="Client">A TCP client.</param>
        public static async Task ExpectEndOfStreamAsync(TcpClient Client)
        {

            var buffer = new Byte[1];

            try
            {

                var received = await Client.Client.
                                            ReceiveAsync(buffer.AsMemory(), SocketFlags.None).
                                            AsTask().
                                            WaitAsync(Timeout);

                Assert.That(received, Is.Zero, "Expected the end of the stream, but received data!");

            }
            catch (SocketException)
            {
                // A connection reset is a valid end of the stream as well.
            }
            catch (IOException)
            { }
            catch (ObjectDisposedException)
            { }

        }

        #endregion

        #region WaitUntilAsync(Condition, Message, Timeout = null)

        /// <summary>
        /// Wait until the given condition is met, using the real clock.
        /// </summary>
        /// <param name="Condition">A condition.</param>
        /// <param name="Message">The assertion message, when the condition was not met in time.</param>
        /// <param name="MaxWait">An optional maximum waiting time.</param>
        public static async Task WaitUntilAsync(Func<Boolean>  Condition,
                                                String         Message,
                                                TimeSpan?      MaxWait   = null)
        {

            var stopwatch  = Stopwatch.StartNew();
            var maxWait    = MaxWait ?? Timeout;

            while (stopwatch.Elapsed < maxWait)
            {

                if (Condition())
                    return;

                await Task.Delay(20);

            }

            Assert.Fail($"{Message} (waited {stopwatch.Elapsed.TotalSeconds:F1} seconds)");

        }

        #endregion

        #region ParsePorts(Response)

        /// <summary>
        /// Parse the TCP ports out of a "ConnectPorts([20000, 30000], Balanced)" response.
        /// </summary>
        /// <param name="Response">A command response.</param>
        public static IPPort[] ParsePorts(CommandResponse Response)
            => ParsePorts(Response.Text);

        /// <summary>
        /// Parse the TCP ports out of a "ConnectPorts([20000, 30000], Balanced)" response.
        /// </summary>
        /// <param name="Text">A command response.</param>
        public static IPPort[] ParsePorts(String Text)
        {

            var start = Text.IndexOf('[', StringComparison.Ordinal);
            var end   = Text.IndexOf(']', StringComparison.Ordinal);

            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"No port list within '{Text}'!");
            Assert.That(end,   Is.GreaterThan(start),      $"No port list within '{Text}'!");

            return [.. Text[(start + 1)..end].
                       Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).
                       Select(IPPort.Parse)];

        }

        #endregion

    }

}
