﻿/*
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

using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple echo test server that listens for incoming TCP echo connections.
    /// </summary>
    public class TCPEchoTestServer : ATCPTestServer
    {

        #region Data

        public const Int32 DefaultBufferSize = 81920; // .NET default for CopyToAsync!

        #endregion

        #region Properties

        /// <summary>
        /// The buffer size for the TCP stream.
        /// </summary>
        public UInt32  BufferSize    { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create a new EchoTestServer that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public TCPEchoTestServer(IIPAddress?              IPAddress        = null,
                                 IPPort?                  TCPPort          = null,
                                 UInt32?                  BufferSize       = null,
                                 TimeSpan?                ReceiveTimeout   = null,
                                 TimeSpan?                SendTimeout      = null,
                                 TCPEchoLoggingDelegate?  LoggingHandler   = null)

            : base(IPAddress,
                   TCPPort,
                   ReceiveTimeout,
                   SendTimeout,
                   LoggingHandler)

        {

            this.BufferSize  = BufferSize.HasValue
                                   ? BufferSize.Value > Int32.MaxValue
                                         ? throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must not exceed Int32.MaxValue!")
                                         : (UInt32) BufferSize.Value
                                   : DefaultBufferSize;

        }

        #endregion


        #region StartNew (Address, TCPPort, BufferSize = null, ReceiveTimeout = null, SendTimeout = null, LoggingHandler = null)

        /// <summary>
        /// Start a new EchoTestServer that listens on the specified IP address and TCP port.
        /// </summary>
        /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
        /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
        /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
        /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
        /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
        /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
        public static async Task<TCPEchoTestServer>

            StartNew(IIPAddress?              IPAddress        = null,
                     IPPort?                  TCPPort          = null,
                     UInt32?                  BufferSize       = null,
                     TimeSpan?                ReceiveTimeout   = null,
                     TimeSpan?                SendTimeout      = null,
                     TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var server = new TCPEchoTestServer(
                             IPAddress,
                             TCPPort,
                             BufferSize,
                             ReceiveTimeout,
                             SendTimeout,
                             LoggingHandler
                         );

            await server.Start();

            return server;

        }

        #endregion


        protected override async Task HandleConnection(TCPConnection Connection, CancellationToken Token)
        {
            await using var stream = Connection.TCPClient.GetStream();
            await stream.CopyToAsync(stream, bufferSize: (Int32) BufferSize, Token).ConfigureAwait(false);
        }


        #region (override) ToString()

        /// <summary>
        /// Returns a text representation of this object.
        /// </summary>
        public override String ToString()

            => $"{nameof(TCPEchoTestServer)}: {IPAddress}:{TCPPort} (BufferSize: {BufferSize}, ReceiveTimeout: {ReceiveTimeout}, SendTimeout: {SendTimeout})";

        #endregion


    }

}
