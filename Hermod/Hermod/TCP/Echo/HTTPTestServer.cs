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

using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using org.GraphDefined.Vanaheimr.Illias;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Tls;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
    /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
    /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
    /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
    /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
    /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
    public class HTTPTestServer(IIPAddress?              IPAddress        = null,
                                IPPort?                  TCPPort          = null,
                                UInt32?                  BufferSize       = null,
                                TimeSpan?                ReceiveTimeout   = null,
                                TimeSpan?                SendTimeout      = null,
                                TCPEchoLoggingDelegate?  LoggingHandler   = null)

        : AHTTPTestServer(
              IPAddress,
              TCPPort,
              BufferSize,
              ReceiveTimeout,
              SendTimeout,
              LoggingHandler
          )

    {

        #region Events

        /// <summary>
        /// An event fired whenever an HTTP request is ready for processing.
        /// The handler must consume the entire body stream if present to support pipelining.
        /// Use the provided NetworkStream to send the response.
        /// </summary>
        public event Func<HTTPRequest, NetworkStream, CancellationToken, Task>? OnHTTPRequest;

        #endregion


        #region StartNew(...)

        public static async Task<HTTPTestServer>

            StartNew(IIPAddress?              IPAddress        = null,
                     IPPort?                  TCPPort          = null,
                     UInt32?                  BufferSize       = null,
                     TimeSpan?                ReceiveTimeout   = null,
                     TimeSpan?                SendTimeout      = null,
                     TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var server = new HTTPTestServer(
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


        #region (override) ProcessHTTPRequest(Request, Stream, CancellationToken = default)

        protected override Task ProcessHTTPRequest(HTTPRequest        Request,
                                                   NetworkStream      Stream,
                                                   CancellationToken  CancellationToken   = default)

            => LogEvent(
                   OnHTTPRequest,
                   loggingDelegate => loggingDelegate.Invoke(
                       Request,
                       Stream,
                       CancellationToken
                   )
               );

        #endregion


        #region (private) LogEvent (Logger, LogHandler, ...)

        private Task LogEvent<TDelegate>(TDelegate?                                         Logger,
                                         Func<TDelegate, Task>                              LogHandler,
                                         [CallerArgumentExpression(nameof(Logger))] String  EventName     = "",
                                         [CallerMemberName()]                       String  OICPCommand   = "")

            where TDelegate : Delegate

            => LogEvent(
                   nameof(HTTPTestServer),
                   Logger,
                   LogHandler,
                   EventName,
                   OICPCommand
               );

        #endregion


    }

}
