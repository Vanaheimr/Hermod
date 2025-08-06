/*
 * Copyright (c) 2010-2025 GraphDefined GmbH <achim.friedland@graphdefined.com>
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

using System.Text;
using System.Diagnostics;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple TCP echo test client that can connect to a TCP echo server,
    /// </summary>
    public class TCPTestClient : ATCPTestClient
    {

        #region Constructor(s)

        private TCPTestClient(IIPAddress               Address,
                              IPPort                   TCPPort,
                              I18NString?              Description      = null,

                              Boolean?                 PreferIPv4       = null,
                              TimeSpan?                ConnectTimeout   = null,
                              TimeSpan?                ReceiveTimeout   = null,
                              TimeSpan?                SendTimeout      = null,
                              UInt32?                  BufferSize       = null,
                              TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : base(Address,
                   TCPPort,
                   Description,

                   PreferIPv4,
                   ConnectTimeout,
                   ReceiveTimeout,
                   SendTimeout,
                   BufferSize,
                   LoggingHandler)

        { }

        #endregion


        #region ConnectNew (         TCPPort, ConnectTimeout = null, ReceiveTimeout = null, SendTimeout = null, BufferSize = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<TCPTestClient>

            ConnectNew(IPPort                   TCPPort,
                       I18NString?              Description      = null,

                       Boolean?                 PreferIPv4       = null,
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

                => await ConnectNew(
                             IPvXAddress.Localhost,
                             TCPPort,
                             Description,

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             BufferSize,
                             LoggingHandler
                         );

        #endregion

        #region ConnectNew (Address, TCPPort, ConnectTimeout = null, ReceiveTimeout = null, SendTimeout = null, BufferSize = null, LoggingHandler = null)

        /// <summary>
        /// Create a new EchoTestClient and connect to the given address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to connect to.</param>
        /// <param name="TCPPort">The TCP port to connect to.</param>
        /// <param name="ConnectTimeout">An optional timeout for the connection attempt.</param>
        /// <param name="ReceiveTimeout">An optional timeout for receiving data.</param>
        /// <param name="SendTimeout">An optional timeout for sending data.</param>
        /// <param name="BufferSize">An optional buffer size for sending and receiving data.</param>
        /// <param name="LoggingHandler">An optional logging handler to log messages.</param>
        public static async Task<TCPTestClient>

            ConnectNew(IIPAddress               IPAddress,
                       IPPort                   TCPPort,
                       I18NString?              Description      = null,

                       Boolean?                 PreferIPv4       = null,
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var client = new TCPTestClient(
                             IPAddress,
                             TCPPort,
                             Description,

                             PreferIPv4,
                             ConnectTimeout,
                             ReceiveTimeout,
                             SendTimeout,
                             BufferSize,
                             LoggingHandler
                         );

            await client.ConnectAsync();

            return client;

        }

        #endregion


        #region ReconnectAsync()

        public async Task ReconnectAsync()
        {
            await base.ReconnectAsync().ConfigureAwait(false);
        }

        #endregion

        #region ConnectAsync()

        public async Task ConnectAsync()
        {
            await base.ConnectAsync().ConfigureAwait(false);
        }

        #endregion


        #region SendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)
        {

            var response  = await SendBinary(Encoding.UTF8.GetBytes(Text));
            var text      = Encoding.UTF8.GetString(response.Item2, 0, response.Item2.Length);

            return (response.Item1,
                    text,
                    response.Item3,
                    response.Item4);

        }

        #endregion

        #region SendBinary (Bytes)

        /// <summary>
        /// Send the given bytes to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Bytes">The bytes to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public async Task<(Boolean, Byte[], String?, TimeSpan)> SendBinary(Byte[] Bytes)
        {

            if (!IsConnected || tcpClient is null)
                return (false, Array.Empty<Byte>(), "Client is not connected.", TimeSpan.Zero);

            try
            {

                var stopwatch   = Stopwatch.StartNew();
                var stream      = tcpClient.GetStream();
                clientCancellationTokenSource           ??= new CancellationTokenSource();

                // Send the data
                await stream.WriteAsync(Bytes, clientCancellationTokenSource.Token).ConfigureAwait(false);
                await stream.FlushAsync(clientCancellationTokenSource.Token).ConfigureAwait(false);

                using var responseStream = new MemoryStream();
                var buffer     = new Byte[8192];
                var bytesRead  = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, clientCancellationTokenSource.Token).ConfigureAwait(false)) > 0)
                {
                    await responseStream.WriteAsync(buffer.AsMemory(0, bytesRead), clientCancellationTokenSource.Token).ConfigureAwait(false);
                }

                stopwatch.Stop();

                return (true, responseStream.ToArray(), null, stopwatch.Elapsed);

            }
            catch (Exception ex)
            {
                await Log($"Error in SendBinary: {ex.Message}");
                return (false, Array.Empty<Byte>(), ex.Message, TimeSpan.Zero);
            }

        }

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(TCPTestClient)}: {RemoteIPAddress}:{RemotePort} (Connected: {IsConnected})";

        #endregion

    }

}
