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

using System.Net;
using System.Net.Sockets;

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
                              TimeSpan?                ConnectTimeout   = null,
                              TimeSpan?                ReceiveTimeout   = null,
                              TimeSpan?                SendTimeout      = null,
                              UInt32?                  BufferSize       = null,
                              TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : base(Address,
                   TCPPort,
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
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

                => await ConnectNew(
                             IPvXAddress.Localhost,
                             TCPPort,
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
                       TimeSpan?                ConnectTimeout   = null,
                       TimeSpan?                ReceiveTimeout   = null,
                       TimeSpan?                SendTimeout      = null,
                       UInt32?                  BufferSize       = null,
                       TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var client = new TCPTestClient(
                             IPAddress,
                             TCPPort,
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
            await reconnectAsync().ConfigureAwait(false);
        }

        #endregion

        #region ConnectAsync()

        public async Task ConnectAsync()
        {
            await connectAsync().ConfigureAwait(false);
        }

        #endregion


        #region SendText   (Text)

        /// <summary>
        /// Send the given message to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Text">The text message to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public Task<(Boolean, String, String?, TimeSpan)> SendText(String Text)

            => sendText(Text);

        #endregion

        #region SendBinary (Bytes)

        /// <summary>
        /// Send the given bytes to the echo server and receive the echoed response.
        /// </summary>
        /// <param name="Bytes">The bytes to send and echo.</param>
        /// <returns>Whether the echo was successful, the echoed response, an optional error response, and the time taken to send and receive it.</returns>
        public Task<(Boolean, Byte[], String?, TimeSpan)> SendBinary(Byte[] Bytes)

            => sendBinary(Bytes);

        #endregion


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override string ToString()

            => $"{nameof(TCPTestClient)}: {RemoteIPAddress}:{RemoteTCPPort} (Connected: {IsConnected})";

        #endregion

    }

}
