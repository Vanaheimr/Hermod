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

using System.Runtime.CompilerServices;

using org.GraphDefined.Vanaheimr.Illias;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.DNS;
using org.GraphDefined.Vanaheimr.Hermod.Sockets.TCP;
using System.Security.Authentication;
using org.GraphDefined.Vanaheimr.Hermod.Sockets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod
{

    public delegate Task OnHTTPRequestDelegate (HTTPRequest        Request,
                                                Stream             Stream,
                                                CancellationToken  CancellationToken);

    public delegate Task OnHTTPResponseDelegate(HTTPResponse       Response,
                                                Stream             Stream,
                                                CancellationToken  CancellationToken);


    /// <summary>
    /// A simple HTTP test server that listens for incoming TCP connections and processes HTTP requests, supporting pipelining.
    /// </summary>
    /// <param name="IPAddress">The IP address to listen on. If null, the loopback address will be used.</param>
    /// <param name="TCPPort">The TCP port to listen on. If 0, a random TCP port will be assigned.</param>
    /// <param name="HTTPServerName">An optional HTTP server name. If null or empty, the default HTTP server name will be used.</param>
    /// <param name="BufferSize">An optional buffer size for the TCP stream. If null, the default buffer size will be used.</param>
    /// <param name="ReceiveTimeout">An optional receive timeout for the TCP stream. If null, the default receive timeout will be used.</param>
    /// <param name="SendTimeout">An optional send timeout for the TCP stream. If null, the default send timeout will be used.</param>
    /// <param name="LoggingHandler">An optional logging handler that will be called for each log message.</param>
    public class HTTPTestServer(IIPAddress?                                               IPAddress                    = null,
                                IPPort?                                                   TCPPort                      = null,
                                String?                                                   HTTPServerName               = null,
                                UInt32?                                                   BufferSize                   = null,
                                TimeSpan?                                                 ReceiveTimeout               = null,
                                TimeSpan?                                                 SendTimeout                  = null,
                                TCPEchoLoggingDelegate?                                   LoggingHandler               = null,

                                ServerCertificateSelectorDelegate?                        ServerCertificateSelector    = null,
                                RemoteTLSClientCertificateValidationHandler<ITCPServer>?  ClientCertificateValidator   = null,
                                LocalCertificateSelectionHandler?                         LocalCertificateSelector     = null,
                                SslProtocols?                                             AllowedTLSProtocols          = null,
                                Boolean?                                                  ClientCertificateRequired    = null,
                                Boolean?                                                  CheckCertificateRevocation   = null,

                                ConnectionIdBuilder?                                      ConnectionIdBuilder          = null,
                                UInt32?                                                   MaxClientConnections         = null,
                                IDNSClient?                                               DNSClient                    = null,

                                Boolean?                                                  DisableMaintenanceTasks      = false,
                                TimeSpan?                                                 MaintenanceInitialDelay      = null,
                                TimeSpan?                                                 MaintenanceEvery             = null,

                                Boolean?                                                  DisableWardenTasks           = false,
                                TimeSpan?                                                 WardenInitialDelay           = null,
                                TimeSpan?                                                 WardenCheckEvery             = null,

                                Boolean?                                                  AutoStart                    = false)

        : AHTTPTestServer(

              IPAddress,
              TCPPort,
              HTTPServerName,
              BufferSize,
              ReceiveTimeout,
              SendTimeout,
              LoggingHandler,

              ServerCertificateSelector,
              ClientCertificateValidator,
              LocalCertificateSelector,
              AllowedTLSProtocols,
              ClientCertificateRequired,
              CheckCertificateRevocation,

              ConnectionIdBuilder,
              MaxClientConnections,
              DNSClient,

              DisableMaintenanceTasks,
              MaintenanceInitialDelay,
              MaintenanceEvery,

              DisableWardenTasks,
              WardenInitialDelay,
              WardenCheckEvery,

              AutoStart

          )

    {

        #region Events

        /// <summary>
        /// An event fired whenever an HTTP request was received.
        /// </summary>
        public event OnHTTPRequestDelegate?   OnHTTPRequest;

        /// <summary>
        /// An event fired whenever an HTTP request shall be processed.
        /// </summary>
        public event HTTPDelegate?            ProcessHTTP;

        /// <summary>
        /// An event fired whenever an HTTP response was sent.
        /// </summary>
        public event OnHTTPResponseDelegate?  OnHTTPResponse;

        #endregion


        #region StartNew(...)

        public static async Task<HTTPTestServer>

            StartNew(IIPAddress?              IPAddress        = null,
                     IPPort?                  TCPPort          = null,
                     String?                  HTTPServerName   = null,
                     UInt32?                  BufferSize       = null,
                     TimeSpan?                ReceiveTimeout   = null,
                     TimeSpan?                SendTimeout      = null,
                     TCPEchoLoggingDelegate?  LoggingHandler   = null)

        {

            var server = new HTTPTestServer(
                             IPAddress,
                             TCPPort,
                             HTTPServerName,
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

        protected override async Task<HTTPResponse>

            ProcessHTTPRequest(HTTPRequest        Request,
                               Stream             Stream,
                               CancellationToken  CancellationToken   = default)
        {

            await LogEvent(
                      OnHTTPRequest,
                      loggingDelegate => loggingDelegate.Invoke(
                          Request,
                          Stream,
                          CancellationToken
                      )
                  );


            HTTPResponse? response = null;

            if (ProcessHTTP is not null)
            {
                try
                {

                    response = await await Task.WhenAny(
                                   ProcessHTTP.GetInvocationList().
                                          OfType<HTTPDelegate>().
                                          Select(xxx => xxx.Invoke(Request))
                               ).ConfigureAwait(false);

                }
                catch (Exception e)
                {
                    DebugX.LogException(e, nameof(HTTPTestServer) + "." + nameof(ProcessHTTPRequest));
                }
            }

            response ??= new HTTPResponse.Builder(Request) {
                             HTTPStatusCode = HTTPStatusCode.BadRequest
                         };


            await LogEvent(
                      OnHTTPResponse,
                      loggingDelegate => loggingDelegate.Invoke(
                          response,
                          Stream,
                          CancellationToken
                      )
                  );

            return response;

        }

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
